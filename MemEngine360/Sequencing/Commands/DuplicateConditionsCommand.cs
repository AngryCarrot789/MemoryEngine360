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

using MemEngine360.Sequencing.View;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Selections;

namespace MemEngine360.Sequencing.Commands;

public class DuplicateConditionsCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!TaskSequenceManager.DataKey.TryGetContext(e.ContextData, out TaskSequenceManager? manager)) {
            return Executability.Invalid;
        }

        TaskSequenceManagerViewState state = TaskSequenceManagerViewState.GetInstance(manager);
        return state.PrimarySelectedSequence == null || state.PrimarySelectedSequence.IsRunning
            ? Executability.ValidButCannotExecute
            : Executability.Valid;
    }

    protected override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!TaskSequenceManager.DataKey.TryGetContext(e.ContextData, out TaskSequenceManager? manager)) {
            return Task.CompletedTask;
        }

        TaskSequenceManagerViewState state = TaskSequenceManagerViewState.GetInstance(manager);

        TaskSequence? sequence = state.PrimarySelectedSequence;
        if (sequence == null || sequence.IsRunning) {
            return Task.CompletedTask;
        }

        ListSelectionModel<BaseSequenceCondition> selection = TaskSequenceViewState.GetInstance(sequence).SelectedConditions;
        List<(BaseSequenceCondition Cond, int Idx)> clones = selection.SelectedItems.Select(x => (Cond: x.CreateClone(), Idx: x.TaskSequence!.Conditions.IndexOf(x))).OrderBy(x => x.Idx).ToList();
        int offset = 0;
        foreach ((BaseSequenceCondition Cond, int Idx) in clones) {
            sequence.Conditions.Insert(offset + Idx + 1, Cond);
            offset++;
        }

        selection.Clear();
        selection.SelectItems(clones.Select(x => x.Cond));
        return Task.CompletedTask;
    }
}