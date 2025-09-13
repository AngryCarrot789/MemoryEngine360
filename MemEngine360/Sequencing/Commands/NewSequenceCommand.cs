﻿// 
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
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Sequencing.Commands;

public class NewSequenceCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        return TaskSequenceManager.DataKey.IsPresent(e.ContextData) ? Executability.Valid : Executability.Invalid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!TaskSequenceManager.DataKey.TryGetContext(e.ContextData, out TaskSequenceManager? manager)) {
            return;
        }

        TaskSequenceManagerViewState state = TaskSequenceManagerViewState.GetInstance(manager);
        state.SelectedSequences.Clear();
        
        ObservableList<TaskSequence> sequences = manager.Sequences;
        TaskSequence sequence = new TaskSequence() {
            DisplayName = TextIncrement.GetIncrementableString(x => sequences.All(y => y.DisplayName != x), "New Sequence", out string? output, true) ? output : "New Sequence"
        };

        sequences.Add(sequence);
        state.SelectedSequences.SelectItem(sequence);
        
        SingleUserInputInfo info = new SingleUserInputInfo("New sequence", "What do you want to call it?", sequence.DisplayName);
        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
            sequence.DisplayName = info.Text;
        }
    }
}