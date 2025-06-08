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

using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Commands.ATM;

public class EditSavedAddressDescriptionCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!BaseAddressTableEntry.DataKey.TryGetContext(e.ContextData, out BaseAddressTableEntry? result)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!BaseAddressTableEntry.DataKey.TryGetContext(e.ContextData, out BaseAddressTableEntry? result)) {
            return;
        }

        SingleUserInputInfo info = new SingleUserInputInfo(result.Description) {
            Caption = "Change description",
            DefaultButton = true,
            Label = "New description"
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
            result.Description = info.Text;
        }
    }
}