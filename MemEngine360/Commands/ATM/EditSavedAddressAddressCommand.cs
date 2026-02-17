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

using MemEngine360.Engine;
using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Commands.ATM;

public class EditSavedAddressAddressCommand : BaseSavedAddressSelectionCommand {
    public EditSavedAddressAddressCommand() {
    }

    protected override Executability CanExecuteOverride(List<BaseAddressTableEntry> entries, MemoryEngine engine, CommandEventArgs e) {
        return entries.Any(x => x is AddressTableEntry) ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(List<BaseAddressTableEntry> entries, MemoryEngine engine, CommandEventArgs e) {
        entries = entries.Where(x => x is AddressTableEntry).ToList();
        if (entries.Count < 1) {
            return;
        }

        SingleUserInputInfo input = new SingleUserInputInfo(((AddressTableEntry) entries[0]).MemoryAddress.ToString()) {
            Caption = "Edit address",
            Message = "Change the address of this saved address table entry",
            DefaultButton = UserInputInfo.ButtonType.Confirm,
            Label = "Address (hex)", Prefix = "0x",
            Validate = (a) => {
                if (!MemoryAddressUtils.TryParse(a.Input, out _, out string? err))
                    a.Errors.Add(err!);
            },
            Footer = "Pointer format is Base->Offset 1->Offset N. All offsets are hexadecimal."
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input) == true) {
            _ = MemoryAddressUtils.TryParse(input.Text, out IMemoryAddress? memoryAddress);
            foreach (BaseAddressTableEntry ui in entries) {
                AddressTableEntry entry = (AddressTableEntry) ui;
                
                entry.MemoryAddress = memoryAddress!;
                entry.AddressTableManager?.MemoryEngine.ScanningProcessor.RefreshSavedAddressesLater();   
            }
        }
    }
}