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
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Commands;

public class DuplicateSequencesCommand : Command {
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
        List<(TaskSequence Seq, int Idx)> clones = ui.SequenceSelectionManager.SelectedItemList.
                                                      Select(x => (Seq: x.TaskSequence.CreateClone(true), Idx: x.TaskSequence.Manager!.IndexOf(x.TaskSequence))).
                                                      OrderBy(x => x.Idx).ToList();
        int offset = 0;
        foreach ((TaskSequence Seq, int Idx) item in clones) {
            item.Seq.DisplayName = TextIncrement.GetNextText(item.Seq.DisplayName);
            ui.Manager.InsertSequence(offset + item.Idx + 1, item.Seq); // +1 to add after the existing item
            offset++;
        }
        
        // virtualization of task sequence list box items not implemented yet, and there's no reason
        // to do it since I doubt anyone will use enough to where it makes a difference
        ui.SequenceSelectionManager.SetSelection(clones.Select(x => ui.GetSequenceControl(x.Seq)));
    }
}