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

using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Commands;

public class OpenConsoleConnectionDialogCommand : Command {
    private IOpenConnectionView? myDialog;

    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? memUi)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (this.myDialog != null && !this.myDialog.IsClosed) {
            this.myDialog.Activate();
            return;
        }

        if (!IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? memUi)) {
            return;
        }

        ulong frame = memUi.MemoryEngine360.GetNextConnectionChangeFrame();

        if (memUi.MemoryEngine360.Connection != null) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to a console. Close existing connection first?", MessageBoxButton.OKCancel, MessageBoxResult.OK);
            if (result != MessageBoxResult.OK) {
                return;
            }

            using CancellationTokenSource cts = new CancellationTokenSource();
            bool isConnectionClearSuccess = await ActivityManager.Instance.RunTask(async () => {
                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = "Disconnect from connection";
                task.Progress.Text = "Stopping all tasks...";

                // ConnectionAboutToChange can be called at any time even if the connection isn't
                // about to change. It's purely just to signal tasks to stop
                await memUi.MemoryEngine360.BroadcastConnectionAboutToChange(frame);

                task.Progress.Text = "Waiting for busy operations...";
                using IDisposable? token = await memUi.MemoryEngine360.BeginBusyOperationAsync(task.CancellationToken);
                if (token == null) {
                    return false;
                }

                // Doesn't matter if the connection became null in the meantime
                IConsoleConnection? existingConnection = memUi.MemoryEngine360.GetConnection(token);
                if (existingConnection == null) {
                    return true;
                }

                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    memUi.MemoryEngine360.SetConnection(token, frame, null, ConnectionChangeCause.User);
                });

                try {
                    await existingConnection.Close();
                }
                catch (Exception) {
                    // ignored
                }

                if (ILatestActivityView.LatestActivityDataKey.TryGetContext(e.ContextData, out ILatestActivityView? view))
                    view.Activity = "Disconnected from console";

                return true;
            }, cts);

            if (!isConnectionClearSuccess) {
                return;
            }
        }

        this.myDialog = await ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionManager>().ShowOpenConnectionView(memUi.MemoryEngine360);
        if (this.myDialog != null) {
            IDisposable? token = null;
            try {
                IConsoleConnection? connection = await this.myDialog.WaitForClose();
                if (connection != null) {
                    // When returned token is null, close the connection since we can't
                    // do anything else with the connection since the user cancelled the operation
                    if ((token = await SetEngineConnectionAndHandleProblemsAsync(memUi.MemoryEngine360, connection, frame)) == null) {
                        await connection.Close();
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
    /// Acquires the busy token, and attempts to set the connection to the given value (on the given frame).
    /// If a connection already exists, we ask the user if they want to overwrite the existing one.
    /// If they say yes, we close the existing and use the new one. Otherwise, we close the new one and nothing else happens.
    /// <para>
    /// If the busy token could not be acquired, we do nothing (the new connection remains open)
    /// </para>
    /// </summary>
    /// <param name="engine">The memory engine</param>
    /// <param name="connection">The new connection</param>
    /// <param name="frame">The connection changing frame</param>
    /// <returns>The token</returns>
    public static async Task<IDisposable?> SetEngineConnectionAndHandleProblemsAsync(MemoryEngine360 engine, IConsoleConnection connection, ulong frame) {
        IDisposable? token = await engine.BeginBusyOperationActivityAsync("Change connection");
        if (token == null) {
            return null;
        }

        if (engine.Connection != null) {
            // Somehow a connection was set before we got here and user doesn't want to overwrite it.
            // Maybe they opened two windows for some reason? Perhaps via the task sequencer and main window.
            
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to a console. Close existing connection first?", MessageBoxButton.OKCancel, MessageBoxResult.OK);
            if (result != MessageBoxResult.OK) {
                await connection.Close();
            }
        }

        engine.SetConnection(token, frame, connection, ConnectionChangeCause.User);
        return token;
    }
}