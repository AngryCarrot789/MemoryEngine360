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

public class DeleteConditionSelectionCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!TaskSequenceManager.DataKey.TryGetContext(e.ContextData, out TaskSequenceManager? manager)) {
            return Executability.Invalid;
        }

        return TaskSequenceManagerViewState.GetInstance(manager).PrimarySelectedSequence == null ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!TaskSequenceManager.DataKey.TryGetContext(e.ContextData, out TaskSequenceManager? manager)) {
            return;
        }

        TaskSequenceManagerViewState state = TaskSequenceManagerViewState.GetInstance(manager);
        TaskSequence? sequence = state.PrimarySelectedSequence;
        if (sequence == null) {
            return;
        }
        
        if (sequence.IsRunning) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Cannot delete conditions", "The sequence is still running, conditions cannot be removed. Do you want to stop them and then delete?", MessageBoxButton.OKCancel, MessageBoxResult.OK);
            if (result != MessageBoxResult.OK) {
                return;
            }

            sequence.RequestCancellation();
            await sequence.WaitForCompletion();

            Debug.Assert(!sequence.IsRunning);
        }

        List<BaseSequenceCondition> items = state.SelectedConditions!.ToList();
        state.SelectedConditions!.Clear();
        foreach (BaseSequenceCondition item in items) {
            bool removed = sequence.Conditions.Remove(item);
            Debug.Assert(removed);
        }
    }
}