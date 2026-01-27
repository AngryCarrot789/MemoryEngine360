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
using MemEngine360.Sequencing.Conditions;
using MemEngine360.Sequencing.View;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Selections;

namespace MemEngine360.Sequencing.Commands;

public class DuplicateConditionsCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!TaskSequenceManagerViewState.DataKey.TryGetContext(e.ContextData, out TaskSequenceManagerViewState? manager)) {
            return Executability.Invalid;
        }

        return manager.PrimarySelectedSequence == null || manager.PrimarySelectedSequence.IsRunning
            ? Executability.ValidButCannotExecute
            : Executability.Valid;
    }

    protected override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!TaskSequenceManagerViewState.DataKey.TryGetContext(e.ContextData, out TaskSequenceManagerViewState? manager)) {
            return Task.CompletedTask;
        }

        IConditionsHost? host = manager.ConditionHost;
        if (host?.TaskSequence == null || host.TaskSequence.IsRunning) {
            return Task.CompletedTask;
        }

        ListSelectionModel<BaseSequenceCondition> selection = manager.SelectedConditionsFromHost!;
        Debug.Assert(selection != null, "Selection could not be null since " + nameof(manager.ConditionHost) + " is non-null");
        if (selection.Count < 1) {
            return Task.CompletedTask;
        }
        
        List<(BaseSequenceCondition Cond, int Idx)> clones = selection.SelectedItems.Select(x => (Cond: x.CreateClone(), Idx: x.Owner!.Conditions.IndexOf(x))).OrderBy(x => x.Idx).ToList();
        int offset = 0;
        foreach ((BaseSequenceCondition Cond, int Idx) in clones) {
            host.Conditions.Insert(offset + Idx + 1, Cond);
            offset++;
        }

        selection.SetSelection(clones.Select(x => x.Cond));
        return Task.CompletedTask;
    }
}