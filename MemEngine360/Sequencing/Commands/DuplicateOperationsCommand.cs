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

using MemEngine360.Sequencing.Operations;
using MemEngine360.Sequencing.View;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Commands;

public class DuplicateOperationsCommand : Command {
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

        // Create list of clones, ordered by their index in the sequence list
        ListSelectionModel<BaseSequenceOperation> selection = TaskSequenceViewState.GetInstance(sequence).SelectedOperations;
        List<(BaseSequenceOperation Op, int Idx)> clones = selection.SelectedItems.Select(x => (Op: CreateNormalClone(x), Idx: x.TaskSequence!.Operations.IndexOf(x))).OrderBy(x => x.Idx).ToList();
        int offset = 1; // +1 to add after the existing item
        foreach ((BaseSequenceOperation Op, int Idx) item in clones) {
            sequence.Operations.Insert(offset + item.Idx, item.Op);
            offset++;
        }

        // virtualization of task sequence list box items not implemented yet, and there's no reason
        // to do it since I doubt anyone will use enough to where it makes a difference
        selection.SetSelection(clones.Select(x => x.Op));
        return Task.CompletedTask;
    }

    private static BaseSequenceOperation CreateNormalClone(BaseSequenceOperation operation) {
        BaseSequenceOperation cloned = operation.CreateClone();
        if (cloned is LabelOperation clonedLabel && !string.IsNullOrWhiteSpace(clonedLabel.LabelName) && operation.TaskSequence != null) {
            List<LabelOperation> labels = operation.TaskSequence.Operations.OfType<LabelOperation>().ToList();
            labels.Remove((LabelOperation) operation);
            if (labels.Count < 1 || !TextIncrement.GetIncrementableString(
                    name => labels.All(op => op.LabelName == null || !name.Equals(op.LabelName, StringComparison.OrdinalIgnoreCase)),
                    clonedLabel.LabelName,
                    out string? newLabelName,
                    canUseInitialInput: false /* current instance already uses this.labelName */)) {
                newLabelName = null;
            }
            
            clonedLabel.LabelName = newLabelName;
        }

        return cloned;
    }
}