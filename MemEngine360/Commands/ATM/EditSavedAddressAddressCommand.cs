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
        
        if (IEngineUI.EngineUIDataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
            return Executability.Valid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!BaseAddressTableEntry.DataKey.TryGetContext(e.ContextData, out BaseAddressTableEntry? entry)) {
            return;
        }

        uint initialAddress = (entry as AddressTableEntry)?.Address ?? ((AddressTableGroupEntry) entry).GroupAddress;
        bool isAbsolute = (entry as AddressTableEntry)?.IsAddressAbsolute ?? ((AddressTableGroupEntry) entry).IsAddressAbsolute;
        SingleUserInputInfo input = new SingleUserInputInfo((isAbsolute ? "" : "+") + initialAddress.ToString(isAbsolute ? "X8" : "X")) {
            Caption = "Dump memory region",
            Message = "Change the address of this saved address table entry",
            DefaultButton = true,
            Label = "Address (hex)",
            Validate = (a) => {
                string text = a.Input.StartsWith('+') ? a.Input.Substring(1) : a.Input;
                if (!uint.TryParse(text, NumberStyles.HexNumber, null, out _)) {
                    if (ulong.TryParse(text, NumberStyles.HexNumber, null, out _)) {
                        a.Errors.Add("Value is too big. Maximum is 0xFFFFFFFF");
                    }
                    else {
                        a.Errors.Add("Invalid UInt32.");
                    }
                }
            }
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input) == true) {
            bool isUsingOffset = input.Text.StartsWith('+');
            uint newAddr = uint.Parse(isUsingOffset ? input.Text.Substring(1) : input.Text, NumberStyles.HexNumber);
            if (entry is AddressTableEntry) {
                ((AddressTableEntry) entry).SetAddress(newAddr, !isUsingOffset);
            }
            else {
                ((AddressTableGroupEntry) entry).SetAddress(newAddr, !isUsingOffset);
            }

            entry.AddressTableManager?.MemoryEngine.ScanningProcessor.RefreshSavedAddressesLater();
        }
    }
}