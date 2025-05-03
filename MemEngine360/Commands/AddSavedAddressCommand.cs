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
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Commands;

public class AddSavedAddressCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        return e.ContextData.ContainsKey(MemoryEngine360.DataKey) ? Executability.Valid : Executability.Invalid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine)) {
            return;
        }

        uint initialAddress = 0;
        if (IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            IList<ScanResultViewModel> list = ui.ScanResultSelectionManager.SelectedItemList;
            if (list.Count > 0)
                initialAddress = list[list.Count - 1].Address;
        }

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
            SavedAddressViewModel result = new SavedAddressViewModel(engine.ScanningProcessor, uint.Parse(addrDescInfo.TextA, NumberStyles.HexNumber, null)) {
                Description = addrDescInfo.TextB
            };

            SavedResultDataTypeUserInputInfo dataTypeInfo = new SavedResultDataTypeUserInputInfo(result) {
                Caption = "Modify data type"
            };

            if (await IUserInputDialogService.Instance.ShowInputDialogAsync(dataTypeInfo) == true) {
                result.DisplayAsHex = dataTypeInfo.DisplayAsHex;
                result.DisplayAsUnsigned = dataTypeInfo.DisplayAsUnsigned;
                result.DataType = dataTypeInfo.DataType;
                result.StringType = dataTypeInfo.StringScanOption;
                result.StringLength = dataTypeInfo.StringLength;
                engine.ScanningProcessor.SavedAddresses.Add(result);
                engine.ScanningProcessor.RefreshSavedAddressesLater();
            }
        }
    }
}