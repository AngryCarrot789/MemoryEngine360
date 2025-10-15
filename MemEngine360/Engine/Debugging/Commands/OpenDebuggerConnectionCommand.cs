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
using MemEngine360.Connections.Features;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.Debugging.Commands;

public class OpenDebuggerConnectionCommand : BaseDebuggerCommand {
    private IOpenConnectionView? myDialog;

    protected override async Task ExecuteCommandAsync(ConsoleDebugger debugger, CommandEventArgs e) {
        if (this.myDialog != null && this.myDialog.IsWindowOpen) {
            this.myDialog.Activate();
            return;
        }

        if (debugger.Connection != null && !debugger.Connection.IsClosed) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(
                "Already Connected",
                "Already connected to a console. Close existing connection first?",
                MessageBoxButtons.OKCancel, MessageBoxResult.OK,
                persistentDialogName: OpenConsoleConnectionDialogCommand.AlreadyOpenDialogName);
            if (result != MessageBoxResult.OK) {
                return;
            }

            if (!await TryDisconnectInActivity(debugger)) {
                return;
            }
        }

        this.myDialog = await ApplicationPFX.GetComponent<ConsoleConnectionManager>().ShowOpenConnectionView();
        if (this.myDialog != null) {
            try {
                IConsoleConnection? connection = await this.myDialog.WaitForConnection();
                if (connection != null) {
                    // When returned token is null, close the connection since we can't
                    // do anything else with the connection since the user cancelled the operation
                    if (!await TrySetConnectionAndHandleProblems(debugger, connection)) {
                        connection.Close();
                    }
                }
            }
            finally {
                this.myDialog = null;
            }
        }

        await debugger.UpdateAllThreads(CancellationToken.None);
    }

    /// <summary>
    /// Attempt to disconnect the debugger's connection
    /// </summary>
    /// <param name="debugger"></param>
    /// <returns>True when disconnected, false when failed to disconnect (could not obtain token)</returns>
    public static async Task<bool> TryDisconnectInActivity(ConsoleDebugger debugger) {
        using CancellationTokenSource cts = new CancellationTokenSource();
        Result<bool> result = await ActivityManager.Instance.RunTask(async () => {
            ActivityTask task = ActivityManager.Instance.CurrentTask;
            task.Progress.Caption = "Disconnect from connection";
            task.Progress.Text = "Stopping all tasks...";

            // Fake signalling the window is closed, so that any users of the busy token can stop their tasks as
            // soon as possible. Basically a lazy version of the engine's BroadcastConnectionAboutToChange
            bool oldIsActive = debugger.IsWindowVisible;
            debugger.IsWindowVisible = false;

            using IBusyToken? token = await debugger.BusyLock.BeginBusyOperationFromActivity(CancellationToken.None);
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
            return debugger.Connection == null;
        }, cts);

        return result.GetValueOrDefault();
    }

    /// <summary>
    /// Attempt to set connection to new connection. Returns token on success, returns null on error (could not obtain token, already connected to a console or new connection is unsupported)
    /// </summary>
    /// <param name="debugger"></param>
    /// <param name="newConnection"></param>
    /// <returns></returns>
    public static async Task<bool> TrySetConnectionAndHandleProblems(ConsoleDebugger debugger, IConsoleConnection newConnection) {
        ArgumentNullException.ThrowIfNull(debugger);
        ArgumentNullException.ThrowIfNull(newConnection);

        using IBusyToken? token = await debugger.BusyLock.BeginBusyOperationUsingActivity("Change connection");
        if (token == null) {
            return false;
        }

        IConsoleConnection? oldConnection = debugger.Connection;
        Debug.Assert(oldConnection != newConnection);

        if (!newConnection.TryGetFeature(out IFeatureXboxDebugging? debugging)) {
            await IMessageDialogService.Instance.ShowMessage("Incompatible connection", "Connection does not support debug features", MessageBoxButtons.OK, MessageBoxResult.OK);
            return false;
        }

        if (oldConnection != null && !oldConnection.IsClosed) {
            // Somehow a connection was set before we got here and user doesn't want to overwrite it.
            // Maybe they opened two windows for some reason? Perhaps via the task sequencer and main window.

            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to a console. Close existing connection first?", MessageBoxButtons.OKCancel, MessageBoxResult.OK, persistentDialogName: OpenConsoleConnectionDialogCommand.AlreadyOpenDialogName);
            if (result != MessageBoxResult.OK) {
                return false;
            }
        }

        XboxExecutionState exec;
        try {
            exec = await debugging.GetExecutionState();
        }
        catch (Exception e) when (e is IOException || e is TimeoutException) {
            await IMessageDialogService.Instance.ShowMessage("Network error", "Error querying current execution state", e.Message, MessageBoxButtons.OK, MessageBoxResult.OK);
            return false;
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

        debugger.SetCurrentState(exec);
        return true;
    }
}