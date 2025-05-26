// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using Avalonia.Controls;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Avalonia.Utils;
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

    public MemoryEngine360 MemoryEngine360 { get; internal set; }

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

    public event OpenConnectionViewCurrentConnectionChangedEventHandler? CurrentConnectionChanged;

    private CancellationTokenSource? currCts;
    private bool isConnecting;

    public OpenConnectionView() {
        this.InitializeComponent();
        this.PART_ListBox.SelectionMode = SelectionMode.Single;
        this.PART_ListBox.SelectionChanged += this.PART_ListBoxOnSelectionChanged;

        this.PART_CancelButton.Click += (sender, args) => {
            if (TopLevel.GetTopLevel(this) is DesktopWindow window) {
                window.Close();
            }
        };

        this.PART_ConfirmButton.Command = new AsyncRelayCommand(async () => {
            this.isConnecting = true;
            this.UpdateConnectButton();

            ConsoleTypeListBoxItem? selection = ((ConsoleTypeListBoxItem?) this.PART_ListBox.SelectedItem);
            if (selection != null && selection.RegisteredConsoleType != null) {
                this.currCts = new CancellationTokenSource();
                using IDisposable? token = await this.MemoryEngine360!.BeginBusyOperationActivityAsync("Connect to console", cancellationTokenSource: this.currCts);
                if (token != null) {
                    IConsoleConnection? connection;
                    try {
                        connection = await selection.RegisteredConsoleType.OpenConnection(this.MemoryEngine360, selection.UserConnectionInfo, this.currCts);
                    }
                    catch (Exception e) {
                        await IMessageDialogService.Instance.ShowMessage("Error", "An unhandled exception occurred while opening connection", e.GetToString());
                        connection = null;
                    }

                    if (connection != null) {
                        this.CurrentConnection = connection;
                        if (TopLevel.GetTopLevel(this) is DesktopWindow window) {
                            window.Close();
                        }
                    }
                }

                // may get disposed and set to null during window close
                this.currCts?.Dispose();
                this.currCts = null;
            }

            this.isConnecting = false;
            this.UpdateConnectButton();
        });
    }

    static OpenConnectionView() {
        Registry = new ModelControlRegistry<UserConnectionInfo, Control>();
    }

    internal void OnWindowOpened() {
        ConsoleTypeListBoxItem? selected = null;
        ConsoleConnectionManager service = ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionManager>();
        foreach (RegisteredConnectionType type in service.RegisteredConsoleTypes) {
            ConsoleTypeListBoxItem item = new ConsoleTypeListBoxItem() {
                Engine = this.MemoryEngine360,
                RegisteredConsoleType = type
            };

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

        foreach (ConsoleTypeListBoxItem itm in this.PART_ListBox.Items)
            itm!.RegisteredConsoleType = null;
        this.PART_ListBox.Items.Clear();
    }

    private void PART_ListBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        ConsoleTypeListBoxItem? selected = this.PART_ListBox.SelectedItem as ConsoleTypeListBoxItem;

        this.PART_DisplayName.Text = selected?.RegisteredConsoleType!.DisplayName;
        this.PART_Description.Text = selected?.RegisteredConsoleType!.LongDescription;

        if (this.PART_UserConnectionContent.Content is IConsoleConnectivityControl c1)
            c1.OnDisconnected();

        UserConnectionInfo? newInfo = selected?.UserConnectionInfo;
        Control? control = newInfo != null ? Registry.NewInstance(newInfo) : null;
        this.PART_UserConnectionContent.Content = control;
        if (control is IConsoleConnectivityControl c2)
            c2.OnConnected(this, newInfo!);
    }

    private void UpdateConnectButton() {
        if (!(TopLevel.GetTopLevel(this) is DesktopWindow window) || window.IsClosed) {
            return;
        }

        this.PART_ConfirmButton.Content = this.isConnecting ? "Connecting..." : "Connect";
        this.PART_ConfirmButton.Width = this.isConnecting ? 90 : 72;
    }
}