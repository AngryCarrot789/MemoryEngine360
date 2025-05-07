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
using MemEngine360.Engine.Scanners;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Commands;

public class EditScanResultValueCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        ScanningProcessor processor;
        if (!ScanResultViewModel.DataKey.TryGetContext(e.ContextData, out ScanResultViewModel? result)) {
            if (!IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
                return Executability.Invalid;
            }

            processor = ui.MemoryEngine360.ScanningProcessor;
        }
        else {
            processor = result.ScanningProcessor;
        }

        if (processor.IsScanning)
            return Executability.ValidButCannotExecute;

        if (processor.MemoryEngine360.Connection == null || processor.MemoryEngine360.IsConnectionBusy) {
            return Executability.ValidButCannotExecute;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        MemoryEngine360? memoryEngine360 = null;
        List<ScanResultViewModel> scanResults = new List<ScanResultViewModel>();
        if (IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            scanResults.AddRange(ui.ScanResultSelectionManager.SelectedItems);
            memoryEngine360 = ui.MemoryEngine360;
        }

        if (ScanResultViewModel.DataKey.TryGetContext(e.ContextData, out ScanResultViewModel? theResult)) {
            memoryEngine360 ??= theResult.ScanningProcessor.MemoryEngine360;
            if (!scanResults.Contains(theResult))
                scanResults.Add(theResult);
        }

        if (memoryEngine360 == null || scanResults.Count < 1) {
            return;
        }

        if (memoryEngine360.Connection == null) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Not connected to a console");
            return;
        }

        SingleUserInputInfo input;
        if (scanResults.Count == 1) {
            input = new SingleUserInputInfo("Change value at 0x" + scanResults[0].Address.ToString("X8"), "Immediately change the value at this address", "Value", scanResults[0].CurrentValue);
            input.Validate = (args) => {
                if (scanResults[0].DataType.IsNumeric()) {
                    MemoryEngine360.CanParseTextAsNumber(args, scanResults[0].DataType, scanResults[0].NumericDisplayType);
                }
                else if (args.Input.Length > scanResults[0].FirstValue.Length) {
                    args.Errors.Add("Length must not exceed the first value's length, otherwise, you'd be writing into an unknown area");
                }
            };
        }
        else {
            DataType dataType = scanResults[0].DataType;
            for (int i = 1; i < scanResults.Count; i++) {
                if (dataType != scanResults[i].DataType) {
                    await IMessageDialogService.Instance.ShowMessage("Error", "Data types for the selected results are not all the same");
                    return;
                }
            }

            input = new SingleUserInputInfo("Change " + scanResults.Count + " values", "Immediately change the value these addresses", "Value", scanResults[scanResults.Count - 1].CurrentValue);
            input.Validate = (args) => {
                if (scanResults[0].DataType.IsNumeric()) {
                    MemoryEngine360.CanParseTextAsNumber(args, scanResults[0].DataType, scanResults[0].NumericDisplayType);
                }
            };
        }

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input) != true) {
            return;
        }

        using IDisposable? token = await memoryEngine360.BeginBusyOperationActivityAsync("Edit scan result value");
        IConsoleConnection? conn;
        if (token == null || (conn = memoryEngine360.Connection) == null) {
            return;
        }

        using CancellationTokenSource cts = new CancellationTokenSource();
        await ActivityManager.Instance.RunTask(async () => {
            ActivityManager.Instance.GetCurrentProgressOrEmpty().SetCaptionAndText("Edit value", "Editing values");
            foreach (ScanResultViewModel scanResult in scanResults) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();

                string newCurrValue;
                if (conn.IsConnected) {
                    await MemoryEngine360.WriteAsText(conn, scanResult.Address, scanResult.DataType, scanResult.NumericDisplayType, input.Text, (uint) scanResult.FirstValue.Length);
                    newCurrValue = await MemoryEngine360.ReadAsText(conn, scanResult.Address, scanResult.DataType, scanResult.NumericDisplayType, (uint) scanResult.FirstValue.Length);
                }
                else {
                    newCurrValue = input.Text;
                }

                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    scanResult.CurrentValue = newCurrValue;
                });
            }
        }, cts);
    }
}