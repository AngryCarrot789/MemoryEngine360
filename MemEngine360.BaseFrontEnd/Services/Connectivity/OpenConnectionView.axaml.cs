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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using MemEngine360.Configs;
using MemEngine360.Connections;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Dialogs;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.Services.Connectivity;

public delegate void OpenConnectionViewConnectionChangedEventHandler(OpenConnectionView sender, IConsoleConnection? oldConnection, IConsoleConnection? newConnection);

/// <summary>
/// The view used to present a means of connecting to a console
/// </summary>
public partial class OpenConnectionView : UserControl, IOpenConnectionView {
    /// <summary>
    /// Gets the registry that maps <see cref="UserConnectionInfo"/> models to a control to present in this view.
    /// When a model is not registered, the application crashes. Register a blank function that just returns new Control()
    /// if you don't need a control but still want to use the UCInfo for some reason
    /// </summary>
    public static readonly ModelControlRegistry<UserConnectionInfo, Control> Registry;

    public static readonly StyledProperty<OpenConnectionInfo?> OpenConnectionInfoProperty = AvaloniaProperty.Register<OpenConnectionView, OpenConnectionInfo?>(nameof(OpenConnectionInfo));

    private readonly IBinder<OpenConnectionInfo> selectedItemBinder = new AvaloniaPropertyToEventPropertyBinder<OpenConnectionInfo>(SelectingItemsControl.SelectedItemProperty, nameof(OpenConnectionInfo.SelectedConnectionTypeChanged), (b) => ((ConsoleTypeListBox) b.Control).SelectedModel = b.Model.SelectedConnectionType, (b) => b.Model.SelectedConnectionType = ((ConsoleTypeListBox) b.Control).SelectedModel);

    internal bool isConnecting;
    private ConnectionTypeEntry? currentSelection;
    private UserConnectionInfo? currentUCInfo;
    private CancellationTokenSource? ctsConnect;
    private bool isCloseRequested;

    /// <summary>
    /// Gets the dialog transaction we used for controlling the dialog
    /// </summary>
    public IDialogOperation<ConnectionResult> DialogOperation { get; internal set; } = null!;

    /// <summary>
    /// Gets or sets the information we used to present the available connections
    /// </summary>
    public OpenConnectionInfo? OpenConnectionInfo {
        get => this.GetValue(OpenConnectionInfoProperty);
        set => this.SetValue(OpenConnectionInfoProperty, value);
    }

    public OpenConnectionView() {
        this.InitializeComponent();
        this.PART_ListBox.SelectionMode = SelectionMode.Single;
        this.selectedItemBinder.AttachControl(this.PART_ListBox);

        this.PART_CancelButton.Command = new AsyncRelayCommand(() => {
            if (!this.DialogOperation.IsCompleted)
                this.DialogOperation.SetCancelled();
            
            return Task.CompletedTask;
        });

        this.PART_ConfirmButton.Command = new AsyncRelayCommand(() => CommandManager.Instance.RunActionAsync(this.TryConnectToConsoleAsCommandAsync, DataManager.GetFullContextData(this)));
    }

    static OpenConnectionView() {
        Registry = new ModelControlRegistry<UserConnectionInfo, Control>();
        OpenConnectionInfoProperty.Changed.AddClassHandler<OpenConnectionView, OpenConnectionInfo?>((s, e) => s.OnOpenConnectionInfoChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnOpenConnectionInfoChanged(OpenConnectionInfo? oldValue, OpenConnectionInfo? newValue) {
        this.PART_ListBox.SetItemsSource(newValue?.ConnectionTypes);
        this.selectedItemBinder.SwitchModel(newValue);

        if (oldValue != null)
            oldValue.SelectedConnectionTypeChanged -= this.OnSelectedConnectionTypeChanged;
        if (newValue != null)
            newValue.SelectedConnectionTypeChanged += this.OnSelectedConnectionTypeChanged;
        this.OnSelectionChanged(newValue?.SelectedConnectionType);
    }

    private void OnSelectedConnectionTypeChanged(OpenConnectionInfo sender, ConnectionTypeEntry? oldSelection, ConnectionTypeEntry? newSelection) {
        this.OnSelectionChanged(newSelection);
    }

    private async Task TryConnectToConsoleAsCommandAsync(CommandEventArgs args) {
        if (this.OpenConnectionInfo == null || this.DialogOperation.IsCompleted)
            return;

        this.isConnecting = true;
        this.PART_ListBox.IsEnabled = false;
        this.UpdateConnectButton();

        ConnectionTypeEntry? selection = this.OpenConnectionInfo.SelectedConnectionType;
        if (selection != null) {
            this.ctsConnect = new CancellationTokenSource();
            try {
                ContextData context = new ContextData().Set(IOpenConnectionView.IsConnectingFromViewDataKey, true);
                UserConnectionInfo? info = selection.GetOrAssignConnectionInfo();

                IConsoleConnection? connection;
                try {
                    connection = await selection.Type.OpenConnection(info, context, this.ctsConnect);
                }
                catch (Exception e) {
                    connection = null;
                    await LogExceptionHelper.ShowMessageAndPrintToLogs("Error", "An unhandled exception occurred while opening connection", e);
                }

                if (!this.DialogOperation.IsCompleted) {
                    if (connection != null) {
                        this.DialogOperation.SetResult(new ConnectionResult(connection, info));
                    }
                    else if (this.isCloseRequested) {
                        this.DialogOperation.SetCancelled();
                    }
                }
                else {
                    if (connection != null) {
                        try {
                            connection.Close();
                        }
                        catch {
                            // ignored
                        }
                    }
                }
            }
            finally {
                this.ctsConnect.Dispose();
                this.ctsConnect = null;
            }
        }

        this.isConnecting = false;
        this.PART_ListBox.IsEnabled = true;
        this.UpdateConnectButton();
    }

    internal void OnDialogOpened() {
        Debug.Assert(this.OpenConnectionInfo != null && this.DialogOperation != null);
        this.PART_ConfirmButton.Focus();
    }

    internal void OnDialogTryingToClose(ref bool cancel) {
        // If the user is trying to close the dialog, but we're still trying to connect,
        // then cancel the connection operation and stop the window closing.
        // But we mark ourselves as trying to close so we close as soon as the connect operation finishes
        if (this.ctsConnect != null) {
            this.isCloseRequested = true;
            
            this.ctsConnect.Cancel();
            cancel = true;
        }
    }

    internal void OnDialogClosed() {
        this.ctsConnect?.Cancel();
        Optional<ConnectionResult> result = this.DialogOperation.Result;
        if (result.HasValue) {
            BasicApplicationConfiguration.Instance.LastConnectionTypeUsed = result.Value.Connection.ConnectionType.RegisteredId;
        }

        this.OpenConnectionInfo = null;
    }

    private void OnSelectionChanged(ConnectionTypeEntry? selection) {
        if (ReferenceEquals(this.currentSelection, selection)) {
            return;
        }

        if (this.currentSelection != null) {
            this.currentSelection.InfoChanged -= this.OnCurrentSelectionUserConnectionInfoChanged;
            if (this.currentSelection.Info != null) {
                UserConnectionInfo.InternalHide(this.currentSelection.Info);
            }
        }

        this.currentSelection = selection;
        if (selection != null) {
            selection.InfoChanged += this.OnCurrentSelectionUserConnectionInfoChanged;
            UserConnectionInfo? info = selection.GetOrAssignConnectionInfo();
            if (info != null) {
                UserConnectionInfo.InternalShow(info);
            }
        }

        this.OnActiveUserConnectionInfoChanged(selection?.Info);
    }

    private void OnCurrentSelectionUserConnectionInfoChanged(ConnectionTypeEntry sender, UserConnectionInfo? oldinfo, UserConnectionInfo? newinfo) {
        this.OnActiveUserConnectionInfoChanged(newinfo);
    }

    private void OnActiveUserConnectionInfoChanged(UserConnectionInfo? info) {
        if (ReferenceEquals(this.currentUCInfo, info)) {
            return;
        }

        (this.PART_UserConnectionContent.Content as IConsoleConnectivityControl)?.OnDisconnected();

        this.currentUCInfo = info;
        this.PART_DisplayName.Text = info != null ? info.ConnectionType.DisplayName : "No console selected";
        this.PART_Description.Text = info != null ? info.ConnectionType.LongDescription : "No console selected";

        Control? control = info != null ? Registry.NewInstance(info) : null;
        this.PART_UserConnectionContent.Content = control;

        // newInfo cannot be null since it must be non-null to create the control
        (control as IConsoleConnectivityControl)?.OnConnected(this, info!);
    }

    private void UpdateConnectButton() {
        this.PART_ConfirmButton.Content = this.isConnecting ? "Connecting..." : "Connect";
        this.PART_ConfirmButton.Width = this.isConnecting ? 90 : 72;
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) {
            this.DialogOperation.SetCancelled();
        }
    }
}