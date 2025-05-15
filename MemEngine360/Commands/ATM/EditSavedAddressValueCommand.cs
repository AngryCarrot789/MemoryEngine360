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

using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Commands.ATM;

public class EditSavedAddressValueCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        ScanningProcessor processor = null;
        if (IAddressTableEntryUI.DataKey.TryGetContext(e.ContextData, out IAddressTableEntryUI? theResult)) {
            if (!(theResult.Entry is AddressTableEntry)) {
                return Executability.Invalid;
            }
            
            processor = theResult.Entry.AddressTableManager!.MemoryEngine360.ScanningProcessor;
        }
        
        if (IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            processor = ui.MemoryEngine360.ScanningProcessor;
        }

        if (processor == null)
            return Executability.Invalid;
        
        if (processor.IsScanning)
            return Executability.ValidButCannotExecute;

        IConsoleConnection? connection = processor.MemoryEngine360.Connection;
        if (connection == null || processor.MemoryEngine360.IsConnectionBusy)
            return Executability.ValidButCannotExecute;

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        MemoryEngine360? memoryEngine360 = null;
        List<AddressTableEntry> savedList = new List<AddressTableEntry>();
        if (IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            savedList.AddRange(ui.AddressTableSelectionManager.SelectedItems.Where(x => x.Entry is AddressTableEntry).Select(x => (AddressTableEntry) x.Entry));
            memoryEngine360 = ui.MemoryEngine360;
        }

        if (IAddressTableEntryUI.DataKey.TryGetContext(e.ContextData, out IAddressTableEntryUI? theResult)) {
            memoryEngine360 ??= theResult.Entry.AddressTableManager!.MemoryEngine360;
            if (theResult.Entry is AddressTableEntry entry && !savedList.Contains(entry)) {
                savedList.Add(entry);
            }
        }

        if (memoryEngine360 == null || savedList.Count < 1) {
            return;
        }

        if (memoryEngine360.Connection == null) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Not connected to a console");
            return;
        }

        SingleUserInputInfo input;
        if (savedList.Count == 1) {
            input = new SingleUserInputInfo("Change value at 0x" + savedList[0].AbsoluteAddress.ToString("X8"), "Immediately change the value at this address", "Value", savedList[0].Value);
            input.Validate = (args) => {
                if (savedList[0].DataType.IsNumeric()) {
                    MemoryEngine360.CanParseTextAsNumber(args, savedList[0].DataType, savedList[0].NumericDisplayType);
                }
                else if (args.Input.Length != savedList[0].Value.Length) {
                    args.Errors.Add("New length must match the string length");
                }
            };
        }
        else {
            DataType dataType = savedList[0].DataType;
            for (int i = 1; i < savedList.Count; i++) {
                if (dataType != savedList[i].DataType) {
                    await IMessageDialogService.Instance.ShowMessage("Error", "Data types for the selected results are not all the same");
                    return;
                }
            }

            input = new SingleUserInputInfo("Change " + savedList.Count + " values", "Immediately change the value at these addresses", "Value", savedList[savedList.Count - 1].Value);
            input.Validate = (args) => {
                if (savedList[0].DataType.IsNumeric()) {
                    MemoryEngine360.CanParseTextAsNumber(args, savedList[0].DataType, savedList[0].NumericDisplayType);
                }
            };
        }

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input) != true) {
            return;
        }

        using IDisposable? token = await memoryEngine360.BeginBusyOperationActivityAsync("Edit saved result value");
        IConsoleConnection? conn;
        if (token == null || (conn = memoryEngine360.Connection) == null) {
            return;
        }

        using CancellationTokenSource cts = new CancellationTokenSource();
        await ActivityManager.Instance.RunTask(async () => {
            ActivityManager.Instance.GetCurrentProgressOrEmpty().SetCaptionAndText("Edit value", "Editing values");
            foreach (AddressTableEntry result in savedList) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                uint absAddress = result.AbsoluteAddress;
                await MemoryEngine360.WriteAsText(conn, absAddress, result.DataType, result.NumericDisplayType, input.Text, (uint) result.Value.Length);
                string newValue = await MemoryEngine360.ReadAsText(conn, absAddress, result.DataType, result.NumericDisplayType, (uint) result.Value.Length);

                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    result.Value = newValue;
                    if (result.DataType == DataType.String)
                        result.StringLength = (uint) newValue.Length;
                }, token:CancellationToken.None);
            }
        }, cts);
    }
}