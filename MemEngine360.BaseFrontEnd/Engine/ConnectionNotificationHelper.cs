// 
// Copyright (c) 2026-2026 REghZy
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

using MemEngine360.Commands;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.View;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Notifications;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.BaseFrontEnd.Engine;

public class ConnectionNotificationHelper {
    private EngineView? view;
    private TextNotification? connectionNotification;
    private LambdaNotificationAction? connectionNotificationCommandGetStarted;
    private LambdaNotificationAction? connectionNotificationCommandDisconnect;
    private LambdaNotificationAction? connectionNotificationCommandReconnect;

    public ConnectionNotificationHelper() {
    }

    public void OnAttach(EngineView newView) {
        this.view = newView;
        this.view!.MemoryEngine.ConnectionChanged += this.OnConnectionChanged;
    }

    public void OnDetach() {
        this.view!.MemoryEngine.ConnectionChanged -= this.OnConnectionChanged;
        this.view = null;
    }

    private void OnStatusBarTextInvalidated(object? sender, EventArgs e) {
        if (sender == this.view!.MemoryEngine.Connection) {
            this.view.UpdateStatusBarConnectionText((IConsoleConnection) sender!);
        }
    }

    private void OnConnectionChanged(object? o, ConnectionChangedEventArgs args) {
        TextNotification notification = this.connectionNotification ??= new TextNotification() {
            ContextData = new ContextData().Set(MemoryEngineViewState.DataKey, this.view!.ViewState).
                                            Set(MemoryEngine.DataKey, this.view!.ViewState.Engine).
                                            Set(ITopLevel.TopLevelDataKey, this.view!.myOwnerWindow_onLoaded)
        };

        if (args.OldConnection != null) {
            args.OldConnection.ConnectionType.StatusBarTextInvalidated -= this.OnStatusBarTextInvalidated;
        }

        if (args.NewConnection != null) {
            args.NewConnection.ConnectionType.StatusBarTextInvalidated += this.OnStatusBarTextInvalidated;

            notification.Caption = "Connected";
            notification.Text = $"Connected to '{args.NewConnection.ConnectionType.DisplayName}'";
            notification.Actions.Clear();
            notification.Actions.Add(this.connectionNotificationCommandGetStarted ??= new LambdaNotificationAction("Get Started", static async (c) => {
                ITopLevel topLevel = ITopLevel.TopLevelDataKey.GetContext(c.ContextData!)!;
                if (topLevel.TryGetWebLauncher(out IWebLauncher? launcher)) {
                    const string url = "https://github.com/AngryCarrot789/MemoryEngine360/wiki#quick-start";
                    await launcher.LaunchUriAsync(new Uri(url));
                }
            }) { ToolTip = "Opens a link to MemoryEngine360's quick start guide on the wiki" });

            notification.Actions.Add(this.connectionNotificationCommandDisconnect ??= new LambdaNotificationAction("Disconnect", static async (c) => {
                ITopLevel topLevel = ITopLevel.TopLevelDataKey.GetContext(c.ContextData!)!;
                MemoryEngineViewState engine = MemoryEngineViewState.DataKey.GetContext(c.ContextData!)!;
                if (engine.Engine.Connection != null) {
                    ((IMutableContextData) c.ContextData!).Set(MemoryEngine.IsDisconnectFromNotification, true);
                    await OpenConsoleConnectionDialogCommand.DisconnectInActivity(topLevel, engine.Engine, 0);
                    ((IMutableContextData) c.ContextData!).Remove(MemoryEngine.IsDisconnectFromNotification);
                }

                c.Notification?.Hide();
            }) { ToolTip = "Disconnect from the connection" });

            notification.CanAutoHide = true;

            MemoryEngineManager.Instance.RaiseProvidePostConnectionActions(this.view!.MemoryEngine, args.NewConnection, notification);

            notification.Show(NotificationManager.GetInstance(this.view!.MemoryEngine));
            this.view!.PART_LatestActivity.Text = notification.Text;
        }
        else {
            notification.Text = $"Disconnected from '{args.OldConnection!.ConnectionType.DisplayName}'";
            this.view!.PART_LatestActivity.Text = notification.Text;
            if (args.Cause != ConnectionChangeCause.ClosingWindow && (!MemoryEngine.IsDisconnectFromNotification.TryGetContext(notification.ContextData!, out bool b) || !b)) {
                notification.Caption = args.Cause switch {
                    ConnectionChangeCause.LostConnection => "Lost Connection",
                    ConnectionChangeCause.ConnectionError => "Connection error",
                    _ => "Disconnected"
                };

                notification.AlertMode =
                    args.Cause == ConnectionChangeCause.LostConnection
                        ? NotificationAlertMode.UntilUserInteraction
                        : NotificationAlertMode.None;

                notification.Actions.Clear();
                if (args.Cause == ConnectionChangeCause.LostConnection || args.Cause == ConnectionChangeCause.ConnectionError) {
                    notification.CanAutoHide = false;
                    notification.Actions.Add(this.connectionNotificationCommandReconnect ??= new LambdaNotificationAction("Reconnect", static async (c) => {
                        // ContextData ensured non-null by LambdaNotificationCommand.requireContext
                        MemoryEngineViewState engineVs = MemoryEngineViewState.DataKey.GetContext(c.ContextData!)!;
                        if (engineVs.Engine.Connection != null) {
                            c.Notification?.Hide();
                            return;
                        }

                        if (engineVs.Engine.LastUserConnectionInfo != null) {
                            // oh...
                            using IBusyToken? busyToken = await engineVs.Engine.BeginBusyOperationUsingActivityAsync("Reconnect to console");
                            if (busyToken == null) {
                                return;
                            }

                            await CommandManager.Instance.RunActionAsync(async _ => {
                                RegisteredConnectionType type = engineVs.Engine.LastUserConnectionInfo.ConnectionType;

                                using CancellationTokenSource cts = new CancellationTokenSource();
                                IConsoleConnection? connection;
                                try {
                                    connection = await type.OpenConnection(engineVs.Engine.LastUserConnectionInfo, EmptyContext.Instance, cts);
                                }
                                catch (Exception e) {
                                    await IMessageDialogService.Instance.ShowMessage("Error", "An unhandled exception occurred while opening connection", e.GetToString());
                                    connection = null;
                                }

                                if (connection != null) {
                                    c.Notification?.Hide();
                                    engineVs.Engine.SetConnection(busyToken, 0, connection, ConnectionChangeCause.User, engineVs.Engine.LastUserConnectionInfo);
                                }
                            }, c.ContextData!);
                        }
                        else {
                            c.Notification?.Hide();
                            await CommandManager.Instance.Execute("commands.memengine.OpenConsoleConnectionDialogCommand", c.ContextData!, null, null);
                        }
                    }) {
                        ToolTip = "Attempt to reconnect to the console, using the same options (e.g. IP address) specified when it was opened initially." + Environment.NewLine +
                                  "If it wasn't opened by you like that, this just shows the Open Connection dialog."
                    });
                }
                else {
                    notification.CanAutoHide = true;
                }

                notification.Show(NotificationManager.GetInstance(this.view!.MemoryEngine));
            }
        }

        this.view!.UpdateStatusBarConnectionText(args.NewConnection);
    }
}