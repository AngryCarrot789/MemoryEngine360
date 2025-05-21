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
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.Services.Connectivity;

/// <summary>
/// The view for the Connect to Console window
/// </summary>
public partial class ConnectToConsoleView : UserControl {
    public static readonly ModelControlRegistry<UserConnectionInfo, Control> Registry;

    public IMemEngineUI EngineUI { get; set; }

    public string? FocusedTypeId { get; set; }

    private readonly AsyncRelayCommand connectCommand;
    private CancellationTokenSource? currCts;
    private bool isConnecting;

    public ConnectToConsoleView() {
        this.InitializeComponent();
        this.PART_ListBox.SelectionMode = SelectionMode.Single;
        this.PART_ListBox.SelectionChanged += this.PART_ListBoxOnSelectionChanged;

        this.PART_CancelButton.Click += (sender, args) => {
            if (TopLevel.GetTopLevel(this) is DesktopWindow window) {
                window.Close();
            }
        };

        this.PART_ConfirmButton.Command = this.connectCommand = new AsyncRelayCommand(async () => {
            this.isConnecting = true;
            this.UpdateConnectButton();

            ConsoleTypeListBoxItem? selection = ((ConsoleTypeListBoxItem?) this.PART_ListBox.SelectedItem);
            if (selection != null && selection.RegisteredConsoleType != null) {
                bool fireConnectionChanging = false;
                if (this.EngineUI!.MemoryEngine360.Connection != null) {
                    // Someone's naughty plugin set the connection after the window has opened.
                    // We could disconnect it but it would be sort of unsafe so just ask the user if they're sure
                    if (await IMessageDialogService.Instance.ShowMessage(
                            "Connection still valid", 
                            "Still connected to a console, somehow. Are you sure you want to continue?", 
                            MessageBoxButton.OKCancel) != MessageBoxResult.OK) {
                        return;
                    }

                    fireConnectionChanging = true;
                }
                
                this.currCts = new CancellationTokenSource();
                using IDisposable? token = await this.EngineUI.MemoryEngine360.BeginBusyOperationActivityAsync("Connect to console", cancellationTokenSource: this.currCts);
                if (token != null) {
                    IConsoleConnection? connection;
                    try {
                        connection = await selection.RegisteredConsoleType.OpenConnection(this.EngineUI.MemoryEngine360, selection.UserConnectionInfo, this.currCts);
                    }
                    catch (Exception e) {
                        await IMessageDialogService.Instance.ShowMessage("Error", "An unhandled exception occurred while opening connection", e.GetToString());
                        connection = null;
                    }

                    if (connection != null) {
                        if (fireConnectionChanging)
                            await this.EngineUI.MemoryEngine360.BroadcastConnectionAboutToChange(EmptyActivityProgress.Instance);
                        
                        this.EngineUI.MemoryEngine360.SetConnection(token, connection, ConnectionChangeCause.User);
                        this.EngineUI.Activity = "Connected to console";

                        ContextEntryGroup entry = this.EngineUI.RemoteCommandsContextEntry;
                        foreach (IContextObject en in connection.ConsoleType.GetRemoteContextOptions()) {
                            entry.Items.Add(en);
                        }

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

    static ConnectToConsoleView() {
        Registry = new ModelControlRegistry<UserConnectionInfo, Control>();
    }

    internal void OnWindowOpened() {
        ConsoleTypeListBoxItem? selected = null;
        ConsoleConnectionManager service = ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionManager>();
        foreach (RegisteredConsoleType type in service.RegisteredConsoleTypes) {
            ConsoleTypeListBoxItem item = new ConsoleTypeListBoxItem() {
                Engine = this.EngineUI.MemoryEngine360,
                RegisteredConsoleType = type
            };
            
            if (selected == null && this.FocusedTypeId != null && type.RegisteredId == this.FocusedTypeId)
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