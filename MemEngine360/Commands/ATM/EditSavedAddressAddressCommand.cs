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

using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Commands.ATM;

public class EditSavedAddressAddressCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IAddressTableEntryUI.DataKey.TryGetContext(e.ContextData, out IAddressTableEntryUI? theResult)) {
            return Executability.Invalid;
        }

        return theResult.Entry is AddressTableEntry ? Executability.Valid : Executability.Invalid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IAddressTableEntryUI.DataKey.TryGetContext(e.ContextData, out IAddressTableEntryUI? theResult)) {
            return;
        }

        if (!(theResult.Entry is AddressTableEntry entry)) {
            return;
        }

        SingleUserInputInfo input = new SingleUserInputInfo(entry.MemoryAddress.ToString()) {
            Caption = "Edit address",
            Message = "Change the address of this saved address table entry",
            DefaultButton = true,
            Label = "Address (hex)",
            Validate = (a) => {
                if (!MemoryAddressUtils.TryParse(a.Input, out _, out string? err))
                    a.Errors.Add(err!);
            },
            Footer = "Pointer format is Base->Offset 1->Offset N"
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input) == true) {
            _ = MemoryAddressUtils.TryParse(input.Text, out IMemoryAddress? memoryAddress);
            entry.MemoryAddress = memoryAddress!;
            entry.AddressTableManager?.MemoryEngine.ScanningProcessor.RefreshSavedAddressesLater();
        }
    }
}