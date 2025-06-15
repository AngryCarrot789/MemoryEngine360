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

public class StopSequenceCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ITaskSequencerUI.TaskSequencerUIDataKey.TryGetContext(e.ContextData, out ITaskSequencerUI? ui))
            return Executability.Invalid;
        
        if (ui.PrimarySelectedSequence != null)
            return ui.PrimarySelectedSequence.TaskSequence.IsRunning ? Executability.Valid : Executability.ValidButCannotExecute;

        return Executability.Valid;
    }
    
    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ITaskSequencerUI.TaskSequencerUIDataKey.TryGetContext(e.ContextData, out ITaskSequencerUI? ui))
            return;

        List<TaskSequence> sequences = new List<TaskSequence>();
        sequences.AddRange(ui.SequenceSelectionManager.SelectedItemList.Select(x => x.TaskSequence));
        if (ui.PrimarySelectedSequence != null && !sequences.Contains(ui.PrimarySelectedSequence.TaskSequence))
            sequences.Add(ui.PrimarySelectedSequence.TaskSequence); // wtf why is it not in the list?

        foreach (TaskSequence seq in sequences) {
            seq.RequestCancellation();
        }
        
        await Task.WhenAll(sequences.Select(x => x.WaitForCompletion()));
    }
}