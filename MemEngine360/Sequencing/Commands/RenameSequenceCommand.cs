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
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Sequencing.Commands;

public class RenameSequenceCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!TaskSequenceManagerViewState.DataKey.TryGetContext(e.ContextData, out TaskSequenceManagerViewState? manager))
            return Executability.Invalid;

        return manager.PrimarySelectedSequence != null ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!TaskSequenceManagerViewState.DataKey.TryGetContext(e.ContextData, out TaskSequenceManagerViewState? manager))
            return;

        TaskSequence? sequence = manager.PrimarySelectedSequence;
        if (sequence != null) {
            SingleUserInputInfo info = new SingleUserInputInfo("Rename sequence", null, "New Name", sequence.DisplayName) { MinimumDialogWidthHint = 350 };
            if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
                sequence.DisplayName = info.Text;
            }
        }
    }
}