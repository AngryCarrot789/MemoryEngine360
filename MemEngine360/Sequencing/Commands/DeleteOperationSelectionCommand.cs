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
using MemEngine360.Sequencing.View;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Sequencing.Commands;

public class DeleteOperationSelectionCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!TaskSequenceManagerViewState.DataKey.TryGetContext(e.ContextData, out TaskSequenceManagerViewState? manager)) {
            return Executability.Invalid;
        }

        return manager.PrimarySelectedSequence == null ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!TaskSequenceManagerViewState.DataKey.TryGetContext(e.ContextData, out TaskSequenceManagerViewState? manager)) {
            return;
        }

        TaskSequence? task = manager.PrimarySelectedSequence;
        if (task == null) {
            return;
        }

        if (task.IsRunning) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Cannot delete operations", "The sequence is still running, operations cannot be removed. Do you want to stop them and then delete?", MessageBoxButtons.OKCancel, MessageBoxResult.OK);
            if (result != MessageBoxResult.OK) {
                return;
            }

            task.RequestCancellation();
            await task.WaitForCompletion();
        }

        List<BaseSequenceOperation> items = manager.SelectedOperations!.SelectedItems.ToList();
        Debug.Assert(items.All(x => x.TaskSequence == task));
        manager.SelectedOperations!.DeselectAll();
        
        foreach (BaseSequenceOperation item in items) {
            bool removed = task.Operations.Remove(item);
            Debug.Assert(removed);
        }
    }
}