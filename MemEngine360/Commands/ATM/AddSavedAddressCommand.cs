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
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Commands.ATM;

public class AddSavedAddressCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        return e.ContextData.ContainsKey(MemoryEngine.EngineDataKey) ? Executability.Valid : Executability.Invalid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine.EngineDataKey.TryGetContext(e.ContextData, out MemoryEngine? engine)) {
            return;
        }

        uint initialAddress = 0;
        AddressTableGroupEntry? targetParent = null;
        if (IEngineUI.DataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
            IList<ScanResultViewModel> list = ui.ScanResultSelectionManager.SelectedItemList;
            if (list.Count > 0)
                initialAddress = list[list.Count - 1].Address;

            if (ui.AddressTableSelectionManager.Count == 1) {
                BaseAddressTableEntry entry = ui.AddressTableSelectionManager.SelectedItemList[0].Entry;
                if (entry is AddressTableGroupEntry) {
                    targetParent = (AddressTableGroupEntry) entry;
                }
                else {
                    targetParent = entry.Parent;
                }
            }
        }
        
        targetParent ??= engine.AddressTableManager.RootEntry;
        DoubleUserInputInfo addrDescInfo = new DoubleUserInputInfo() {
            Caption = "Add address",
            LabelA = "Memory address (hex)",
            LabelB = "Description (optional)",
            TextA = initialAddress.ToString("X"),
            ValidateA = (args) => {
                if (!uint.TryParse(args.Input, NumberStyles.HexNumber, null, out _))
                    args.Errors.Add("Invalid memory address");
            }
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(addrDescInfo) == true) {
            AddressTableEntry result = new AddressTableEntry(uint.Parse(addrDescInfo.TextA, NumberStyles.HexNumber, null)) {
                Description = addrDescInfo.TextB
            };

            SavedResultDataTypeUserInputInfo dataTypeInfo = new SavedResultDataTypeUserInputInfo(result) {
                Caption = "Modify data type"
            };

            if (await IUserInputDialogService.Instance.ShowInputDialogAsync(dataTypeInfo) == true) {
                if (dataTypeInfo.DataType.IsNumeric()) {
                    if (dataTypeInfo.DisplayAsHex) {
                        result.NumericDisplayType = NumericDisplayType.Hexadecimal;
                    }
                    else if (dataTypeInfo.DisplayAsUnsigned) {
                        result.NumericDisplayType = NumericDisplayType.Unsigned;
                    }
                    else {
                        result.NumericDisplayType = NumericDisplayType.Normal;
                    }
                }
                else {
                    result.NumericDisplayType = NumericDisplayType.Normal;
                }

                result.StringType = dataTypeInfo.StringScanOption;
                result.StringLength = dataTypeInfo.StringLength;
                result.ArrayLength = dataTypeInfo.ArrayLength;
                result.DataType = dataTypeInfo.DataType;
                targetParent.AddEntry(result);
                engine.ScanningProcessor.RefreshSavedAddressesLater();
            }
        }
    }
}