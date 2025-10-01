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
using PFXToolKitUI.Activities;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;
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
        if (!TaskSequence.DataKey.TryGetContext(e.ContextData, out TaskSequence? sequence))
            return;
        if (sequence.IsRunning || sequence.Manager == null /* shouldn't be null... */)
            return;

        bool useEngineConnection = sequence.UseEngineConnection;
        IConsoleConnection? connection = useEngineConnection ? sequence.Manager.MemoryEngine.Connection : sequence.DedicatedConnection;
        if (await HandleConnectionErrors(connection, useEngineConnection)) {
            return;
        }

        IDisposable? token = null;
        if (sequence.HasEngineConnectionPriority && (token = sequence.Manager.MemoryEngine.TryBeginBusyOperation()) == null) {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Result<IDisposable?> tokenResult = await ActivityManager.Instance.RunTask(() => {
                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = $"Start '{sequence.DisplayName}'";
                return sequence.Manager.MemoryEngine.BusyLocker.BeginBusyOperationFromActivityAsync(CancellationToken.None);
            }, sequence.Progress, cts);

            // User cancelled operation so don't run the sequence, since it wants busy lock priority
            if ((token = tokenResult.GetValueOrDefault()) == null) {
                return;
            }
        }

        connection = useEngineConnection ? sequence.Manager.MemoryEngine.Connection : sequence.DedicatedConnection;
        if (await HandleConnectionErrors(connection, useEngineConnection)) {
            return;
        }

        Task runTask;
        bool disposeToken = true;
        try {
            runTask = sequence.Run(connection!, token, !useEngineConnection);
            if (runTask.IsCompleted) {
                await runTask; // will most likely throw
                return;
            }

            disposeToken = false;
        }
        finally {
            if (disposeToken)
                token?.Dispose();
        }

        _ = runTask.ContinueWith(async t => {
            try {
                Exception? except = sequence.LastException;
                TaskSequenceManager? manager = sequence.Manager;
                if (except != null) {
                    // When the task sequencer window is shown, the window is placed inside UserContext
                    ITopLevel? topLevel = manager?.UserContext != null ? ITopLevel.FromContext(manager.UserContext) : null;
                    using (CommandManager.LocalContextManager.PushGlobalContext(new ContextData().Set(ITopLevel.TopLevelDataKey, topLevel))) {
                        if (except is IOException || except is TimeoutException) {
                            await LogExceptionHelper.ShowMessageAndPrintToLogs("Task Sequencer",
                                useEngineConnection
                                    ? $"'{sequence.DisplayName}' engine connection timed out while running sequence"
                                    : $"'{sequence.DisplayName}' connection timed out while running sequence"
                                , except);
                        }
                        else {
                            await LogExceptionHelper.ShowMessageAndPrintToLogs("Task Sequencer", $"'{sequence.DisplayName}': An exception occured while running sequence", except);
                        }
                    }
                }

                if (sequence.Manager != null) {
                    ConnectionChangeCause cause = except is IOException
                        ? ConnectionChangeCause.ConnectionError
                        : ConnectionChangeCause.LostConnection; // Use LostConnection even if not TimeoutException since it's the only other option that makes sense.

                    if (token != null) {
                        sequence.Manager.MemoryEngine.CheckConnection(token, cause);
                    }
                    else if (useEngineConnection) {
                        sequence.Manager.MemoryEngine.CheckConnection(cause);
                    }
                }
            }
            finally {
                token?.Dispose();
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private static async Task<bool> HandleConnectionErrors(IConsoleConnection? connection, bool useEngineConnection) {
        if (connection == null) {
            await IMessageDialogService.Instance.ShowMessage("Not connected", useEngineConnection ? "Engine is not connected to a console" : "Not connected to a console");
            return true;
        }

        if (connection.IsClosed) {
            await IMessageDialogService.Instance.ShowMessage("Not connected", useEngineConnection ? "Engine connection is no longer connected. Please reconnect" : "Connection is no longer connected. Please reconnect");
            return true;
        }

        return false;
    }
}