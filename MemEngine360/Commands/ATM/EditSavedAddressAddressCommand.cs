// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using System.Globalization;
using MemEngine360.Engine;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Commands.ATM;

public class EditSavedAddressAddressCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (IAddressTableEntryUI.DataKey.TryGetContext(e.ContextData, out IAddressTableEntryUI? theResult)) {
            return Executability.Valid;
        }
        
        if (IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            return Executability.Valid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        MemoryEngine360? memoryEngine360 = null;
        List<BaseAddressTableEntry> savedList = new List<BaseAddressTableEntry>();
        if (IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            savedList.AddRange(ui.AddressTableSelectionManager.SelectedItems.Select(x => x.Entry));
            memoryEngine360 = ui.MemoryEngine360;
        }

        if (IAddressTableEntryUI.DataKey.TryGetContext(e.ContextData, out IAddressTableEntryUI? theResult)) {
            memoryEngine360 ??= theResult.Entry.AddressTableManager!.MemoryEngine360;
            if (!savedList.Contains(theResult.Entry)) {
                savedList.Add(theResult.Entry);
            }
        }

        if (memoryEngine360 == null || savedList.Count < 1) {
            return;
        }

        uint initialAddress = savedList.Count == 1 ? (savedList[0] is AddressTableEntry ? ((AddressTableEntry) savedList[0]).Address : ((AddressTableGroupEntry) savedList[0]).GroupAddress) : 0;
        SingleUserInputInfo input = new SingleUserInputInfo(initialAddress.ToString("X8")) {
            Caption = "Dump memory region",
            Message = "Change the address of this saved address table entry",
            DefaultButton = true,
            Label = "Address (hex)",
            Validate = (a) => {
                if (!uint.TryParse(a.Input, NumberStyles.HexNumber, null, out _)) {
                    if (ulong.TryParse(a.Input, NumberStyles.HexNumber, null, out _)) {
                        a.Errors.Add("Value is too big. Maximum is 0xFFFFFFFF");
                    }
                    else {
                        a.Errors.Add("Invalid UInt32.");
                    }
                }
            }
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input) == true) {
            uint newAddr = uint.Parse(input.Text, NumberStyles.HexNumber);
            foreach (BaseAddressTableEntry entry in savedList) {
                if (entry is AddressTableEntry) {
                    ((AddressTableEntry) entry).Address = newAddr;
                }
                else {
                    ((AddressTableGroupEntry) entry).GroupAddress = newAddr;
                }
                
                memoryEngine360.ScanningProcessor.RefreshSavedAddressesLater();   
            }
        }
    }
}