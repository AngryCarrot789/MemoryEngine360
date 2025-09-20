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

using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Commands;

public class RunSequenceCommand : Command {
    public RunSequenceCommand() : base(allowMultipleExecutions: true) {
    }

    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!TaskSequence.DataKey.TryGetContext(e.ContextData, out TaskSequence? seq)) {
            return Executability.Invalid;
        }

        if (seq.IsRunning || seq.Manager == null /* shouldn't be null... */) {
            return Executability.ValidButCannotExecute;
        }

        if ((seq.UseEngineConnection ? seq.Manager.MemoryEngine.Connection : seq.DedicatedConnection) == null) {
            return Executability.ValidButCannotExecute;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!TaskSequence.DataKey.TryGetContext(e.ContextData, out TaskSequence? seq)) {
            return;
        }

        if (seq.IsRunning || seq.Manager == null /* shouldn't be null... */) {
            return;
        }

        bool useEngineConnection = seq.UseEngineConnection;

        IConsoleConnection? connection = useEngineConnection ? seq.Manager.MemoryEngine.Connection : seq.DedicatedConnection;
        if (connection == null) {
            await IMessageDialogService.Instance.ShowMessage("Not connected", useEngineConnection ? "Engine is not connected to a console" : "Not connected to a console");
            return;
        }

        if (connection.IsClosed) {
            await IMessageDialogService.Instance.ShowMessage("Not connected", useEngineConnection ? "Engine connection is no longer connected. Please reconnect" : "Connection is no longer connected. Please reconnect");
            return;
        }

        IDisposable? token = null;
        if (seq.HasBusyLockPriority && (token = seq.Manager.MemoryEngine.TryBeginBusyOperation()) == null) {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Result<IDisposable?> tokenResult = await ActivityManager.Instance.RunTask(() => {
                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = $"Start '{seq.DisplayName}'";
                task.Progress.Text = "Waiting for busy operations...";
                return seq.Manager.MemoryEngine.BeginBusyOperationAsync(task.CancellationToken);
            }, seq.Progress, cts);

            // User cancelled operation so don't run the sequence, since it wants busy lock priority
            if ((token = tokenResult.HasException ? null : tokenResult.Value) == null) {
                return;
            }
        }

        try {
            connection = useEngineConnection ? seq.Manager.MemoryEngine.Connection : seq.DedicatedConnection;
            if (connection == null || connection.IsClosed) {
                await IMessageDialogService.Instance.ShowMessage("Not connected", useEngineConnection ? "Engine is not connected to a console" : "Not connected to a console");
            }
            else if (connection.IsClosed) {
                await IMessageDialogService.Instance.ShowMessage("Not connected", useEngineConnection ? "Engine connection is no longer connected. Please reconnect" : "Connection is no longer connected. Please reconnect");
            }
            else {
                await seq.Run(connection, token, !useEngineConnection);
                Exception? except = seq.LastException;
                if (except != null) {
                    if (except is IOException || except is TimeoutException) {
                        await LogExceptionHelper.ShowMessageAndPrintToLogs("Network Error", except.Message, except);
                    }
                    else {
                        await LogExceptionHelper.ShowMessageAndPrintToLogs("Unexpected Error", "An exception occured while running sequence: " + except.Message, except);
                    }
                }

                if (seq.Manager != null) {
                    ConnectionChangeCause cause = except is IOException
                        ? ConnectionChangeCause.ConnectionError
                        : ConnectionChangeCause.LostConnection; // Use LostConnection even if not TimeoutException since it's the only other option that makes sense.

                    if (token != null) {
                        seq.Manager.MemoryEngine.CheckConnection(token, cause);
                    }
                    else if (useEngineConnection) {
                        seq.Manager.MemoryEngine.CheckConnection(cause);
                    }

                    // Probably annoying to the user to force activate the window
                    // await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    //     if (useEngineConnection && sequence.Manager != null) {
                    //         IConsoleConnection? currentConnection = sequence.Manager.MemoryEngine.Connection;
                    //         if (currentConnection == null || currentConnection.IsClosed) {
                    //             MemoryEngineViewState.GetInstance(sequence.Manager.MemoryEngine).RaiseRequestWindowFocus();
                    //         }
                    //     }
                    // }, DispatchPriority.Background);
                }
            }
        }
        finally {
            token?.Dispose();
        }
    }
}