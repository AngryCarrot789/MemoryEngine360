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
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Commands;

public class EditScanResultValueCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        ScanningProcessor processor;
        if (!ScanResultViewModel.DataKey.TryGetContext(e.ContextData, out ScanResultViewModel? result)) {
            if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
                return Executability.Invalid;
            }

            processor = ui.MemoryEngine360.ScanningProcessor;
        }
        else {
            processor = result.ScanningProcessor;
        }

        if (processor.IsScanning)
            return Executability.ValidButCannotExecute;

        IConsoleConnection? connection = processor.MemoryEngine360.Connection;
        if (connection == null || connection.IsBusy)
            return Executability.ValidButCannotExecute;

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        MemoryEngine360? memoryEngine360 = null;
        List<ScanResultViewModel> scanResults = new List<ScanResultViewModel>();
        if (IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
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

        IConsoleConnection? connection = memoryEngine360.Connection;
        if (connection == null) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Not connected to a console");
            return;
        }

        if (memoryEngine360.IsConnectionBusy) {
            string desc = memoryEngine360.ScanningProcessor.IsScanning ? "The connection is busy scanning the xbox memory. Cancel to modify values" : "Connection is currently busy somewhere";
            await IMessageDialogService.Instance.ShowMessage("Busy", "Connection is busy. Concurrent operations dangerous", desc);
            return;
        }

        SingleUserInputInfo input;
        if (scanResults.Count == 1) {
            input = new SingleUserInputInfo("Change value at 0x" + scanResults[0].Address.ToString("X8"), "Immediately change the value at this address", "Value", scanResults[0].CurrentValue);
            input.Validate = (args) => {
                switch (scanResults[0].DataType) {
                    case DataType.Byte:
                        if (!byte.TryParse(args.Input, out _))
                            args.Errors.Add("Invalid Byte");
                    break;
                    case DataType.Int16:
                        if (!short.TryParse(args.Input, out _))
                            args.Errors.Add("Invalid Int16");
                    break;
                    case DataType.Int32:
                        if (!int.TryParse(args.Input, out _))
                            args.Errors.Add("Invalid Int32");
                    break;
                    case DataType.Int64:
                        if (!long.TryParse(args.Input, out _))
                            args.Errors.Add("Invalid Int64");
                    break;
                    case DataType.Float:
                        if (!float.TryParse(args.Input, out _))
                            args.Errors.Add("Invalid float");
                    break;
                    case DataType.Double:
                        if (!double.TryParse(args.Input, out _))
                            args.Errors.Add("Invalid double");
                    break;
                    case DataType.String:
                        if (args.Input.Length > scanResults[0].FirstValue.Length)
                            args.Errors.Add("Length must not exceed the first value's length, otherwise, you'd be writing into an unknown area");
                    break;
                    default: throw new ArgumentOutOfRangeException();
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
                switch (scanResults[0].DataType) {
                    case DataType.Byte:
                        if (!byte.TryParse(args.Input, out _))
                            args.Errors.Add("Invalid Byte");
                    break;
                    case DataType.Int16:
                        if (!short.TryParse(args.Input, out _))
                            args.Errors.Add("Invalid Int16");
                    break;
                    case DataType.Int32:
                        if (!int.TryParse(args.Input, out _))
                            args.Errors.Add("Invalid Int32");
                    break;
                    case DataType.Int64:
                        if (!long.TryParse(args.Input, out _))
                            args.Errors.Add("Invalid Int64");
                    break;
                    case DataType.Float:
                        if (!float.TryParse(args.Input, out _))
                            args.Errors.Add("Invalid float");
                    break;
                    case DataType.Double:
                        if (!double.TryParse(args.Input, out _))
                            args.Errors.Add("Invalid double");
                    break;
                    default: throw new ArgumentOutOfRangeException();
                }
            };
        }

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input) != true) {
            return;
        }

        IDisposable? token = memoryEngine360.BeginBusyOperation();
        if (token == null) {
            using CancellationTokenSource cts = new CancellationTokenSource();
            ActivityTask task = ActivityManager.Instance.RunTask(async () => {
                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = "Busy";
                task.Progress.Text = "Waiting for busy operations...";

                do {
                    await Task.Delay(100, task.CancellationToken);
                } while ((token = memoryEngine360.BeginBusyOperation()) == null);
            }, cts);

            await task;

            // I'm pretty sure token can never be null at this point if cancelled, since if we
            // get the lock when not busy then we get the token and the task completes successfully.
            if (task.IsCancelled || token == null) {
                token?.Dispose();
                return;
            }
        }

        try {
            foreach (ScanResultViewModel scanResult in scanResults) {
                scanResult.PreviousValue = scanResult.CurrentValue;
                if (memoryEngine360.Connection != null) {
                    await MemoryEngine360.WriteAsText(memoryEngine360.Connection, scanResult.Address, scanResult.DataType, scanResult.NumericDisplayType, input.Text, (uint) scanResult.FirstValue.Length);
                    scanResult.CurrentValue = await MemoryEngine360.ReadAsText(memoryEngine360.Connection, scanResult.Address, scanResult.DataType, scanResult.NumericDisplayType, (uint) scanResult.FirstValue.Length);
                }
                else {
                    scanResult.CurrentValue = input.Text;
                }
            }
        }
        finally {
            token.Dispose();
        }
    }
}