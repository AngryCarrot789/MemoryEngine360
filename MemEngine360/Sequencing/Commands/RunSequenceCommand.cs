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
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Commands;

public class RunSequenceCommand : Command {
    public RunSequenceCommand() : base(allowMultipleExecutions: true) {
    }

    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ITaskSequencerUI.TaskSequenceDataKey.TryGetContext(e.ContextData, out TaskSequence? sequence)) {
            return Executability.Invalid;
        }

        if (sequence.IsRunning || sequence.Manager == null /* shouldn't be null... */) {
            return Executability.ValidButCannotExecute;
        }

        if ((sequence.UseEngineConnection ? sequence.Manager.MemoryEngine.Connection : sequence.DedicatedConnection) == null) {
            return Executability.ValidButCannotExecute;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ITaskSequencerUI.TaskSequenceDataKey.TryGetContext(e.ContextData, out TaskSequence? sequence)) {
            return;
        }

        if (sequence.IsRunning || sequence.Manager == null /* shouldn't be null... */) {
            return;
        }

        bool useEngineConnection = sequence.UseEngineConnection;

        IConsoleConnection? connection = useEngineConnection ? sequence.Manager.MemoryEngine.Connection : sequence.DedicatedConnection;
        if (connection == null || !connection.IsConnected) {
            await IMessageDialogService.Instance.ShowMessage("Not connected", useEngineConnection ? "Engine is not connected to a console" : "Not connected to a console");
            return;
        }

        IDisposable? token = null;
        if (sequence.HasBusyLockPriority && (token = sequence.Manager.MemoryEngine.BeginBusyOperation()) == null) {
            using CancellationTokenSource cts = new CancellationTokenSource();
            token = await ActivityManager.Instance.RunTask(() => {
                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = $"Start '{sequence.DisplayName}'";
                task.Progress.Text = "Waiting for busy operations...";
                return sequence.Manager.MemoryEngine.BeginBusyOperationAsync(task.CancellationToken);
            }, sequence.Progress, cts);

            // User cancelled operation so don't run the sequence, since it wants busy lock priority
            if (token == null) {
                return;
            }
        }

        try {
            connection = useEngineConnection ? sequence.Manager.MemoryEngine.Connection : sequence.DedicatedConnection;
            if (connection == null || !connection.IsConnected) {
                await IMessageDialogService.Instance.ShowMessage("Not connected", "Not connected to a console");
            }
            else {
                await sequence.Run(connection, token, !useEngineConnection);
                if (sequence.LastException != null) {
                    await IMessageDialogService.Instance.ShowMessage("Error encountered", "An exception occured while running sequence", sequence.LastException.GetToString());
                }

                ConnectionChangeCause cause = sequence.LastException is IOException ? ConnectionChangeCause.ConnectionError : ConnectionChangeCause.LostConnection;
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
    }
}