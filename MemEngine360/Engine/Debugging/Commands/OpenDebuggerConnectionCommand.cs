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
using MemEngine360.Commands;
using MemEngine360.Connections;
using MemEngine360.XboxBase;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Engine.Debugging.Commands;

public class OpenDebuggerConnectionCommand : BaseDebuggerCommand {
    private IOpenConnectionView? myDialog;

    protected override async Task ExecuteCommandAsync(ConsoleDebugger debugger, CommandEventArgs e) {
        if (this.myDialog != null && !this.myDialog.IsClosed) {
            this.myDialog.Activate();
            return;
        }

        if (debugger.Connection != null) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to a console. Close existing connection first?", MessageBoxButton.OKCancel, MessageBoxResult.OK, persistentDialogName: OpenConsoleConnectionDialogCommand.AlreadyOpenDialogName);
            if (result != MessageBoxResult.OK) {
                return;
            }

            if (!await DisconnectInActivity(debugger)) {
                return;
            }
        }

        this.myDialog = await ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionManager>().ShowOpenConnectionView(debugger.Engine);
        if (this.myDialog != null) {
            IDisposable? token = null;
            try {
                IConsoleConnection? connection = await this.myDialog.WaitForClose();
                if (connection != null) {
                    // When returned token is null, close the connection since we can't
                    // do anything else with the connection since the user cancelled the operation
                    if ((token = await SetConnectionAndHandleProblemsAsync(debugger, connection)) == null) {
                        connection.Close();
                    }
                }
            }
            finally {
                this.myDialog = null;
                token?.Dispose();
            }
        }

        await debugger.UpdateAllThreads(CancellationToken.None);
    }

    public static async Task<bool> DisconnectInActivity(ConsoleDebugger debugger) {
        using CancellationTokenSource cts = new CancellationTokenSource();
        bool isOperationCancelled = await ActivityManager.Instance.RunTask(async () => {
            ActivityTask task = ActivityManager.Instance.CurrentTask;
            task.Progress.Caption = "Disconnect from connection";
            task.Progress.Text = "Stopping all tasks...";

            // Fake signalling the window is closed, so that any users of the busy token can stop their tasks as
            // soon as possible. Basically a lazy version of the engine's BroadcastConnectionAboutToChange
            bool oldIsActive = debugger.IsWindowVisible;
            debugger.IsWindowVisible = false;

            task.Progress.Text = "Waiting for busy operations...";
            using IDisposable? token = await debugger.BusyLock.BeginBusyOperationAsync(task.CancellationToken);
            if (token != null) {
                IConsoleConnection? oldConnection = debugger.Connection;
                if (oldConnection != null) {
                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                        debugger.SetConnection(token, null);
                    }, token: CancellationToken.None);

                    try {
                        oldConnection.Close();
                    }
                    catch (Exception) {
                        // ignored
                    }
                }

                debugger.IsWindowVisible = oldIsActive;
                return true;
            }

            debugger.IsWindowVisible = oldIsActive;

            // Doesn't matter if the connection became null in the meantime
            return false;
        }, cts);
        return isOperationCancelled;
    }

    public static async Task<IDisposable?> SetConnectionAndHandleProblemsAsync(ConsoleDebugger debugger, IConsoleConnection newConnection) {
        ArgumentNullException.ThrowIfNull(debugger);
        ArgumentNullException.ThrowIfNull(newConnection);

        IDisposable? token = await debugger.BusyLock.BeginBusyOperationActivityAsync("Change connection");
        if (token == null) {
            return null;
        }

        IConsoleConnection? oldConnection = debugger.Connection;
        Debug.Assert(oldConnection != newConnection);

        if (oldConnection != null) {
            // Somehow a connection was set before we got here and user doesn't want to overwrite it.
            // Maybe they opened two windows for some reason? Perhaps via the task sequencer and main window.

            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to a console. Close existing connection first?", MessageBoxButton.OKCancel, MessageBoxResult.OK, persistentDialogName: OpenConsoleConnectionDialogCommand.AlreadyOpenDialogName);
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

        if (!(newConnection is IHaveXboxDebugFeatures)) {
            token.Dispose();
            await IMessageDialogService.Instance.ShowMessage("Incompatible connection", "Connection does not support debug features", MessageBoxButton.OK, MessageBoxResult.OK);
            return null;
        }

        debugger.SetConnection(token, newConnection);
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