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

using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Sequencing.Commands;

public class DuplicateConditionsCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ITaskSequencerUI.TaskSequencerUIDataKey.TryGetContext(e.ContextData, out ITaskSequencerUI? ui)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ITaskSequencerUI.TaskSequencerUIDataKey.TryGetContext(e.ContextData, out ITaskSequencerUI? ui)) {
            return;
        }

        // Create list of clones, ordered by their index in the sequence list
        ITaskSequenceEntryUI? sequence = ui.PrimarySelectedSequence;
        if (sequence == null) {
            return;
        }

        List<(BaseSequenceCondition Cond, int Idx)> clones = ui.ConditionSelectionManager.SelectedItemList.
                                                                Select(x => (Cond: x.Condition.CreateClone(), Idx: x.Condition.TaskSequence!.Conditions.IndexOf(x.Condition))).
                                                                OrderBy(x => x.Idx).
                                                                ToList();

        int offset = 0;
        foreach ((BaseSequenceCondition Cond, int Idx) in clones) {
            sequence.TaskSequence.Conditions.Insert(offset + Idx + 1, Cond);
            offset++;
        }

        ui.ConditionSelectionManager.SetSelection(clones.Select(x => ui.GetConditionControl(x.Cond)));
    }
}