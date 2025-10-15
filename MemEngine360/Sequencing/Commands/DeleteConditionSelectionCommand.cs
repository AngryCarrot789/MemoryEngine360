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
using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Commands;

public class DeleteConditionSelectionCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!TaskSequenceManager.DataKey.TryGetContext(e.ContextData, out TaskSequenceManager? manager)) {
            return Executability.Invalid;
        }

        return TaskSequenceManagerViewState.GetInstance(manager).PrimarySelectedSequence == null ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        // There are two ways to figuring out who to delete conditions from.
        //   1 - look at the TaskSequenceManagerViewState.ConditionHost
        //       since this is what the user will see
        //   2 - Access the contextual condition via BaseSequenceCondition.DataKey
        //       and look at its Owner property
        // And for which is better, who knows.
        // Maybe option 1 for shortcuts, option 2 for context menu commands?
        
        if (!TaskSequenceManager.DataKey.TryGetContext(e.ContextData, out TaskSequenceManager? manager)) {
            return;
        }

        TaskSequenceManagerViewState viewState = TaskSequenceManagerViewState.GetInstance(manager);
        if (viewState.ConditionHost == null) {
            Debug.Assert(!BaseSequenceCondition.DataKey.IsPresent(e.ContextData));
            return;
        }

        TaskSequence? rootSequence = viewState.ConditionHost.TaskSequence;
        if (rootSequence == null) {
            return;
        }

        if (rootSequence.IsRunning) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Cannot delete conditions", "The sequence is still running, conditions cannot be removed. Do you want to stop them and then delete?", MessageBoxButtons.OKCancel, MessageBoxResult.OK);
            if (result != MessageBoxResult.OK) {
                return;
            }

            rootSequence.RequestCancellation();
            await rootSequence.WaitForCompletion();

            Debug.Assert(!rootSequence.IsRunning);
        }

        ListSelectionModel<BaseSequenceCondition>? selectionModel = viewState.SelectedConditionsFromHost;
        if (selectionModel == null) {
            return; // ConditionHost somehow changed
        }
        
        List<LongRange> selection = selectionModel.ToLongRangeUnion().ToList();
        selectionModel.Clear();
        for (int i = selection.Count - 1; i >= 0; i--) {
            selectionModel.SourceList.RemoveRange((int) selection[i].Start, (int) selection[i].Length);
        }
    }
}