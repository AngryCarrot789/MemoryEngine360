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
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Sequencing.Commands;

public class DeleteOperationSelectionCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ITaskSequencerUI.TaskSequencerUIDataKey.TryGetContext(e.ContextData, out ITaskSequencerUI? ui)) {
            return Executability.Invalid;
        }

        return ui.PrimarySelectedSequence == null ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ITaskSequencerUI.TaskSequencerUIDataKey.TryGetContext(e.ContextData, out ITaskSequencerUI? ui)) {
            return;
        }

        if (ui.PrimarySelectedSequence == null) {
            return;
        }

        if (ui.PrimarySelectedSequence.TaskSequence.IsRunning) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Cannot delete operations", "The sequence is still running, operations cannot be removed. Do you want to stop them and then delete?", MessageBoxButton.OKCancel, MessageBoxResult.OK);
            if (result != MessageBoxResult.OK) {
                return;
            }

            TaskSequence task = ui.PrimarySelectedSequence.TaskSequence;
            task.RequestCancellation();
            await task.WaitForCompletion();

            Debug.Assert(!task.IsRunning);
        }

        List<IOperationItemUI> items = ui.OperationSelectionManager.SelectedItems.ToList();
        ui.OperationSelectionManager.Clear();
        foreach (IOperationItemUI item in items) {
            bool removed = item.Operation.Sequence!.RemoveOperation(item.Operation);
            Debug.Assert(removed);
        }
    }
}