// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemoryEngine360.
// 
// MemoryEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemoryEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemoryEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using System.Diagnostics;
using Avalonia.Controls;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.Services.Connectivity;

public delegate void OpenConnectionViewCurrentConnectionChangedEventHandler(OpenConnectionView sender, IConsoleConnection? oldCurrentConnection, IConsoleConnection? newCurrentConnection);

/// <summary>
/// The view used to present a means of connecting to a console
/// </summary>
public partial class OpenConnectionView : UserControl {
    /// <summary>
    /// Gets the registry that maps <see cref="UserConnectionInfo"/> models to a control to present in this view.
    /// When a model is not registered, the application crashes. Register a blank function that just returns new Control()
    /// if you don't need a control but still want to use the UCInfo for some reason
    /// </summary>
    public static readonly ModelControlRegistry<UserConnectionInfo, Control> Registry;

    /// <summary>
    /// Gets or sets the memory engine reference, if necessary
    /// </summary>
    public MemoryEngine? MemoryEngine { get; internal set; }

    public string? TypeToFocusOnOpened { get; internal set; }

    private IConsoleConnection? currentConnection;

    public IConsoleConnection? CurrentConnection {
        get => this.currentConnection;
        private set {
            IConsoleConnection? oldCurrentConnection = this.currentConnection;
            if (oldCurrentConnection != value) {
                this.currentConnection = value;
                this.CurrentConnectionChanged?.Invoke(this, oldCurrentConnection, value);
            }
        }
    }

    /// <summary>
    /// Gets the <see cref="UserConnectionInfo"/> that was used to open the <see cref="CurrentConnection"/> connection
    /// </summary>
    public UserConnectionInfo? UserConnectionInfoForCurrentConnection { get; private set; }

    public event OpenConnectionViewCurrentConnectionChangedEventHandler? CurrentConnectionChanged;

    private CancellationTokenSource? currCts;
    internal bool isConnecting, isClosingWindow;
    private ConsoleTypeListBoxItem? myCurrentSelection;

    public OpenConnectionView() {
        this.InitializeComponent();
        this.PART_ListBox.SelectionMode = SelectionMode.Single;
        this.PART_ListBox.SelectionChanged += this.PART_ListBoxOnSelectionChanged;

        this.PART_CancelButton.Click += (sender, args) => {
            this.UserConnectionInfoForCurrentConnection = null;
            if (TopLevel.GetTopLevel(this) is DesktopWindow window) {
                this.isClosingWindow = true;
                window.Close();
                this.isClosingWindow = false;
            }
        };

        this.PART_ConfirmButton.Command = new AsyncRelayCommand(async () => {
            this.isConnecting = true;
            this.PART_ListBox.IsEnabled = false;
            this.UpdateConnectButton();

            ConsoleTypeListBoxItem? selection = ((ConsoleTypeListBoxItem?) this.PART_ListBox.SelectedItem);
            if (selection != null) {
                this.currCts = new CancellationTokenSource();
                IConsoleConnection? connection;
                try {
                    connection = await selection.RegisteredConsoleType.OpenConnection(selection.UserConnectionInfo, this.currCts);
                }
                catch (Exception e) {
                    await IMessageDialogService.Instance.ShowMessage("Error", "An unhandled exception occurred while opening connection", e.GetToString());
                    connection = null;
                }

                if (connection != null) {
                    this.UserConnectionInfoForCurrentConnection = selection.UserConnectionInfo;
                    this.CurrentConnection = connection;
                    if (TopLevel.GetTopLevel(this) is DesktopWindow window) {
                        this.isClosingWindow = true;
                        window.Close();
                        this.isClosingWindow = false;
                    }
                }

                // may get disposed and set to null during window close
                this.currCts?.Dispose();
                this.currCts = null;
            }

            this.isConnecting = false;
            this.PART_ListBox.IsEnabled = true;
            this.UpdateConnectButton();
        });
    }

    static OpenConnectionView() {
        Registry = new ModelControlRegistry<UserConnectionInfo, Control>();
    }

    public void SetUserInfoForConnectionType(string registeredId, UserConnectionInfo info) {
        foreach (object? lbi in this.PART_ListBox.Items) {
            ConsoleTypeListBoxItem item = (ConsoleTypeListBoxItem) lbi!;
            if (item.RegisteredConsoleType.RegisteredId == registeredId) {
                if (item.IsSelected) {
                    item.IsSelected = false;
                    item.SetUserConnectionInfo(info);
                    item.IsSelected = true;
                }
                else {
                    item.SetUserConnectionInfo(info);
                }
            }
        }
    }

    internal void OnWindowOpened() {
        IContextData context = new ContextData().Set(MemoryEngine.EngineDataKey, this.MemoryEngine);

        ConsoleTypeListBoxItem? selected = null;
        ConsoleConnectionManager service = ApplicationPFX.GetComponent<ConsoleConnectionManager>();
        foreach (RegisteredConnectionType type in service.RegisteredConsoleTypes) {
            ConsoleTypeListBoxItem item = new ConsoleTypeListBoxItem(type, context);
            if (selected == null && this.TypeToFocusOnOpened != null && type.RegisteredId == this.TypeToFocusOnOpened)
                selected = item;
            this.PART_ListBox.Items.Add(item);
        }

        this.PART_ListBox.SelectedItem = selected ?? this.PART_ListBox.Items.FirstOrDefault();
        this.PART_ConfirmButton.Focus();
    }

    internal void OnWindowClosed() {
        try {
            this.currCts?.Cancel();
            this.currCts?.Dispose();
        }
        catch {
            // ignored -- hopefully already disposed
        }

        this.currCts = null;
        this.PART_ListBox.SelectedItem = null;
        foreach (ConsoleTypeListBoxItem? itm in this.PART_ListBox.Items)
            itm!.OnRemoving();
        this.PART_ListBox.Items.Clear();
    }

    private void PART_ListBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        Debug.Assert(!this.isConnecting || this.isClosingWindow);

        ConsoleTypeListBoxItem? newSelection = this.PART_ListBox.SelectedItem as ConsoleTypeListBoxItem;
        if (ReferenceEquals(this.myCurrentSelection, newSelection)) {
            return;
        }

        if (this.myCurrentSelection?.UserConnectionInfo != null) {
            UserConnectionInfo.InternalOnHidden(this.myCurrentSelection.UserConnectionInfo);
        }

        this.myCurrentSelection = newSelection;
        if (newSelection?.UserConnectionInfo != null) {
            UserConnectionInfo.InternalOnShown(newSelection.UserConnectionInfo);
        }

        this.PART_DisplayName.Text = newSelection != null ? newSelection.RegisteredConsoleType.DisplayName : "No console selected";
        this.PART_Description.Text = newSelection != null ? newSelection.RegisteredConsoleType.LongDescription : "No console selected";

        (this.PART_UserConnectionContent.Content as IConsoleConnectivityControl)?.OnDisconnected();

        UserConnectionInfo? newInfo = newSelection?.UserConnectionInfo;
        Control? control = newInfo != null ? Registry.NewInstance(newInfo) : null;
        this.PART_UserConnectionContent.Content = control;

        // newInfo cannot be null since it must be non-null to create the control
        (control as IConsoleConnectivityControl)?.OnConnected(this, newInfo!);
    }

    private void UpdateConnectButton() {
        if (!(TopLevel.GetTopLevel(this) is DesktopWindow window) || window.IsClosed) {
            return;
        }

        this.PART_ConfirmButton.Content = this.isConnecting ? "Connecting..." : "Connect";
        this.PART_ConfirmButton.Width = this.isConnecting ? 90 : 72;
    }
}