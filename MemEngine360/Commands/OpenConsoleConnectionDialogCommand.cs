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
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Commands;

public class OpenConsoleConnectionDialogCommand : Command {
    public const string AlreadyOpenDialogName = "dialog.AlreadyConnectedToConsole";
    
    private IOpenConnectionView? myDialog;

    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IEngineUI.DataKey.TryGetContext(e.ContextData, out IEngineUI? memUi)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (this.myDialog != null && !this.myDialog.IsClosed) {
            this.myDialog.Activate();
            return;
        }

        if (!IEngineUI.DataKey.TryGetContext(e.ContextData, out IEngineUI? memUi)) {
            return;
        }

        ulong frame = memUi.MemoryEngine.GetNextConnectionChangeFrame();

        if (memUi.MemoryEngine.Connection != null) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to a console. Close existing connection first?", MessageBoxButton.OKCancel, MessageBoxResult.OK, persistentDialogName: AlreadyOpenDialogName);
            if (result != MessageBoxResult.OK) {
                return;
            }

            if (!await DisconnectInActivity(memUi, frame)) {
                return;
            }
        }

        UserConnectionInfo? lastInfo = memUi.MemoryEngine.LastUserConnectionInfo;
        string focusedTypeId = lastInfo != null ? lastInfo.ConnectionType.RegisteredId : "console.xbox360.xbdm-coreimpl";
        this.myDialog = await ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionManager>().ShowOpenConnectionView(memUi.MemoryEngine, focusedTypeId);
        if (this.myDialog != null) {
            if (lastInfo != null)
                this.myDialog.SetUserInfoForConnectionType(lastInfo.ConnectionType.RegisteredId, lastInfo);
            
            IDisposable? token = null;
            try {
                IConsoleConnection? connection = await this.myDialog.WaitForClose();
                if (connection != null) {
                    // When returned token is null, close the connection since we can't
                    // do anything else with the connection since the user cancelled the operation
                    if ((token = await SetEngineConnectionAndHandleProblemsAsync(memUi.MemoryEngine, connection, frame, this.myDialog.UserConnectionInfoForConnection)) == null) {
                        connection.Close();
                    }
                }
            }
            finally {
                this.myDialog = null;
                token?.Dispose();
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="ui"></param>
    /// <param name="frame"></param>
    /// <returns>False when token could not be acquired</returns>
    public static async Task<bool> DisconnectInActivity(IEngineUI ui, ulong frame) {
        using CancellationTokenSource cts = new CancellationTokenSource();
        bool isOperationCancelled = await ActivityManager.Instance.RunTask(async () => {
            ActivityTask task = ActivityManager.Instance.CurrentTask;
            task.Progress.Caption = "Disconnect from connection";
            task.Progress.Text = "Stopping all tasks...";

            // ConnectionAboutToChange can be called at any time even if the connection isn't
            // about to change. It's purely just to signal tasks to stop
            await ui.MemoryEngine.BroadcastConnectionAboutToChange(frame);

            task.Progress.Text = "Waiting for busy operations...";
            using IDisposable? token = await ui.MemoryEngine.BeginBusyOperationAsync(task.CancellationToken);
            if (token == null) {
                return false;
            }

            // Doesn't matter if the connection became null in the meantime
            IConsoleConnection? existingConnection = ui.MemoryEngine.GetConnection(token);
            if (existingConnection != null) {
                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    ui.MemoryEngine.SetConnection(token, frame, null, ConnectionChangeCause.User);
                });

                try {
                    existingConnection.Close();
                }
                catch (Exception) {
                    // ignored
                }
            }

            return true;
        }, cts);
        return isOperationCancelled;
    }

    /// <summary>
    /// Acquires the busy token, and attempts to set the connection to the given value (on the given frame).
    /// If a connection already exists, we ask the user if they want to overwrite the existing one.
    /// If they say yes, we close the existing and use the new one. Otherwise, we close the new one and nothing else happens.
    /// <para>
    /// If the busy token could not be acquired, we do nothing (the new connection remains open)
    /// </para>
    /// </summary>
    /// <param name="engine">The memory engine</param>
    /// <param name="newConnection">The new connection</param>
    /// <param name="frame">The connection changing frame</param>
    /// <returns>The token</returns>
    public static async Task<IDisposable?> SetEngineConnectionAndHandleProblemsAsync(MemoryEngine engine, IConsoleConnection newConnection, ulong frame, UserConnectionInfo? userConnectionInfo = null) {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(newConnection);

        IDisposable? token = await engine.BeginBusyOperationActivityAsync("Change connection");
        if (token == null) {
            return null;
        }

        IConsoleConnection? oldConnection = engine.Connection;
        Debug.Assert(oldConnection != newConnection);

        if (oldConnection != null) {
            // Somehow a connection was set before we got here and user doesn't want to overwrite it.
            // Maybe they opened two windows for some reason? Perhaps via the task sequencer and main window.

            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to a console. Close existing connection first?", MessageBoxButton.OKCancel, MessageBoxResult.OK, persistentDialogName:AlreadyOpenDialogName);
            if (result != MessageBoxResult.OK) {
                try {
                    newConnection.Close();
                }
                catch (Exception) {
                    // ignored
                }

                return token;
            }
        }

        engine.SetConnection(token, frame, newConnection, ConnectionChangeCause.User, userConnectionInfo);
        if (oldConnection != null) {
            // Always close AFTER changing, just in case a listener wants to send data or whatever
            try {
                oldConnection.Close();
            }
            catch (Exception) {
                // ignored
            }
        }

        return token;
    }
}