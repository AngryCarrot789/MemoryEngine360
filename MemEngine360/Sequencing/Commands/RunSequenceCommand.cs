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
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Commands;

public class RunSequenceCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ITaskSequencerUI.TaskSequenceDataKey.TryGetContext(e.ContextData, out TaskSequence? sequence)) {
            return Executability.Invalid;
        }

        if (sequence.IsRunning || sequence.Manager == null /* shouldn't be null... */) {
            return Executability.ValidButCannotExecute;
        }

        if (sequence.Manager.Engine.Connection == null) {
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

        IDisposable? token = null;
        if (sequence.HasBusyLockPriority && (token = sequence.Manager.Engine.BeginBusyOperation()) == null) {
            using CancellationTokenSource cts = new CancellationTokenSource();
            token = await ActivityManager.Instance.RunTask(() => {
                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = $"Start '{sequence.DisplayName}'";
                task.Progress.Text = "Waiting for busy operations...";
                return sequence.Manager.Engine.BeginBusyOperationAsync(task.CancellationToken);
            }, sequence.Progress, cts);

            // User cancelled operation so don't run the sequence, since it wants busy lock priority
            if (token == null) {
                return;
            }
        }

        try {
            IConsoleConnection? connection = sequence.Manager.Engine.Connection;
            if (connection != null && connection.IsConnected) {
                await sequence.Run(connection, token);
                if (sequence.LastException != null) {
                    await IMessageDialogService.Instance.ShowMessage("Error encountered", "An exception occured while running sequence", sequence.LastException.GetToString());
                }
            }
            else {
                sequence.Manager.Engine.CheckConnection();
                await IMessageDialogService.Instance.ShowMessage("Not connected", "Not connected to a console");
            }
        }
        finally {
            token?.Dispose();
        }
    }
}