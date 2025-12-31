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

using MemEngine360.Commands;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.Activities;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Commands;

public class RunSequenceCommand : Command, IDisabledHintProvider {
    public RunSequenceCommand() : base(allowMultipleExecutions: true) {
    }

    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!TaskSequence.DataKey.TryGetContext(e.ContextData, out TaskSequence? seq)) {
            return Executability.Invalid;
        }

        if (seq.IsRunning || seq.ctsTryingToStart != null || seq.Manager == null /* shouldn't be null... */) {
            return Executability.ValidButCannotExecute;
        }

        if ((seq.UseEngineConnection ? seq.Manager.MemoryEngine.Connection : seq.DedicatedConnection) == null) {
            return Executability.ValidButCannotExecute;
        }

        return Executability.Valid;
    }
    
    public DisabledHintInfo? ProvideDisabledHint(IContextData context, ContextRegistry? sourceContextMenu) {
        if (TaskSequence.DataKey.TryGetContext(context, out TaskSequence? seq) && seq.Manager != null /* shouldn't be null... */) {
            if (seq.ctsTryingToStart != null)
                return new SimpleDisabledHintInfo("Trying to run", "Already trying to start the sequence");
            // Don't provide insight into why it's disabled when IsRunning is true, because that should be obvious
        }

        return null;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!TaskSequence.DataKey.TryGetContext(e.ContextData, out TaskSequence? sequence))
            return;
        if (sequence.IsRunning || sequence.ctsTryingToStart != null || sequence.Manager == null /* shouldn't be null... */)
            return;

        bool disposeToken = true;
        Task runTask;
        IBusyToken? token = null;
        bool useEngineConnection = sequence.UseEngineConnection;
        IConsoleConnection? connection = useEngineConnection ? sequence.Manager.MemoryEngine.Connection : sequence.DedicatedConnection;
        
        try {
            sequence.ctsTryingToStart = new CancellationTokenSource();
            if (await HandleConnectionErrors(connection, useEngineConnection)) {
                return;
            }

            if (sequence.HasEngineConnectionPriority && (token = sequence.Manager.MemoryEngine.TryBeginBusyOperation()) == null) {
                ITopLevel? topLevel = TopLevelContextUtils.GetTopLevelFromContext();
                using IActivityProgress.State state = sequence.Progress.SaveState(BusyLock.WaitingMessage, BusyLock.WaitingMessage);
                Result<IBusyToken?> tokenResult = await ActivityManager.Instance.RunTask(() => {
                    ActivityTask task = ActivityTask.Current;
                    task.Progress.Caption = $"Start '{sequence.DisplayName}'";

                    // Cancellable via the activity itself, so no need to pass a CTS
                    return sequence.Manager.MemoryEngine.BusyLock.BeginBusyOperationFromActivity(new BusyTokenRequestFromActivity() {
                        // Show a foreground dialog if possible to let the user know why it isn't started
                        ForegroundInfo = topLevel != null ? new InForegroundInfo(topLevel) : null
                    });
                }, sequence.ctsTryingToStart);

                // User cancelled operation so don't run the sequence, since it wants busy lock priority
                if ((token = tokenResult.GetValueOrDefault()) == null) {
                    return;
                }
            }

            if (sequence.IsRunning || sequence.Manager == null /* shouldn't be null... */)
                return;

            connection = useEngineConnection ? sequence.Manager.MemoryEngine.Connection : sequence.DedicatedConnection;
            if (await HandleConnectionErrors(connection, useEngineConnection))
                return;

            // should not be null, since this command owns it
            sequence.ctsTryingToStart.Dispose();
            sequence.ctsTryingToStart = null;
            
            if ((runTask = sequence.Run(connection!, token, !useEngineConnection)).IsCompleted) {
                await runTask; // will most likely throw
                return;
            }

            disposeToken = false;
        }
        finally {
            sequence.ctsTryingToStart?.Dispose();
            sequence.ctsTryingToStart = null;
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
                    using (CommandManager.LocalContextManager.PushContext(new ContextData().Set(ITopLevel.TopLevelDataKey, topLevel))) {
                        if (except is IOException || except is TimeoutException) {
                            await IMessageDialogService.Instance.ShowExceptionMessage("Task Sequencer", useEngineConnection
                                ? $"'{sequence.DisplayName}' engine connection timed out while running sequence"
                                : $"'{sequence.DisplayName}' connection timed out while running sequence", except);
                        }
                        else {
                            await IMessageDialogService.Instance.ShowExceptionMessage("Task Sequencer", $"'{sequence.DisplayName}': An exception occured while running sequence", except);
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

    public static async Task<bool> HandleConnectionErrors(IConsoleConnection? connection, bool useEngineConnection) {
        if (connection == null) {
            await IMessageDialogService.Instance.ShowMessage(StandardEngineMessages.Caption_NoConnection, useEngineConnection ? "Engine is not connected to a console" : "Not connected to a console");
            return true;
        }

        if (connection.IsClosed) {
            await IMessageDialogService.Instance.ShowMessage(StandardEngineMessages.Caption_ConnectionClosed, useEngineConnection ? "Engine connection is no longer connected. Please reconnect" : "Connection is no longer connected. Please reconnect");
            return true;
        }

        return false;
    }
}