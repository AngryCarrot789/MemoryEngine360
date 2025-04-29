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
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Commands;

public class EditSavedAddressValueCommand : Command {
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
        if (connection == null || processor.MemoryEngine360.IsConnectionBusy)
            return Executability.ValidButCannotExecute;

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        MemoryEngine360? memoryEngine360 = null;
        List<SavedAddressViewModel> savedList = new List<SavedAddressViewModel>();
        if (IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            savedList.AddRange(ui.SavedAddressesSelectionManager.SelectedItems);
            memoryEngine360 = ui.MemoryEngine360;
        }

        if (SavedAddressViewModel.DataKey.TryGetContext(e.ContextData, out SavedAddressViewModel? theResult)) {
            memoryEngine360 ??= theResult.ScanningProcessor.MemoryEngine360;
            if (!savedList.Contains(theResult))
                savedList.Add(theResult);
        }

        if (memoryEngine360 == null || savedList.Count < 1) {
            return;
        }

        if (memoryEngine360.Connection == null) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Not connected to a console");
            return;
        }

        if (memoryEngine360.IsConnectionBusy) {
            string desc = memoryEngine360.ScanningProcessor.IsScanning ? "The connection is busy scanning the xbox memory. Cancel to modify values" : "Connection is currently busy somewhere";
            await IMessageDialogService.Instance.ShowMessage("Busy", "Connection is busy. Concurrent operations dangerous", desc);
            return;
        }

        SingleUserInputInfo input;
        if (savedList.Count == 1) {
            input = new SingleUserInputInfo("Change value at 0x" + savedList[0].Address.ToString("X8"), "Immediately change the value at this address", "Value", savedList[0].Value);
            input.Validate = (args) => {
                NumericDisplayType ndt = savedList[0].NumericDisplayType;
                switch (savedList[0].DataType) {
                    case DataType.Byte:
                        if (!byte.TryParse(args.Input, ndt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer, null, out _))
                            args.Errors.Add("Invalid Byte");
                    break;
                    case DataType.Int16:
                        if (!short.TryParse(args.Input, ndt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer, null, out _))
                            args.Errors.Add("Invalid Int16");
                    break;
                    case DataType.Int32:
                        if (!int.TryParse(args.Input, ndt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer, null, out _))
                            args.Errors.Add("Invalid Int32");
                    break;
                    case DataType.Int64:
                        if (!long.TryParse(args.Input, ndt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer, null, out _))
                            args.Errors.Add("Invalid Int64");
                    break;
                    case DataType.Float:
                        if (!(ndt == NumericDisplayType.Hexadecimal ? uint.TryParse(args.Input, NumberStyles.HexNumber, null, out _) : float.TryParse(args.Input, out _)))
                            args.Errors.Add("Invalid float");
                    break;
                    case DataType.Double:
                        if (!(ndt == NumericDisplayType.Hexadecimal ? ulong.TryParse(args.Input, NumberStyles.HexNumber, null, out _) : double.TryParse(args.Input, out _)))
                            args.Errors.Add("Invalid double");
                    break;
                    case DataType.String:
                        if (args.Input.Length != savedList[0].Value.Length)
                            args.Errors.Add("New length must match the string length");
                    break;
                    default: throw new ArgumentOutOfRangeException();
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

            input = new SingleUserInputInfo("Change " + savedList.Count + " values", "Immediately change the value these addresses", "Value", savedList[savedList.Count - 1].Value);
            input.Validate = (args) => {
                NumericDisplayType ndt = savedList[0].NumericDisplayType;
                switch (savedList[0].DataType) {
                    case DataType.Byte:
                        if (!byte.TryParse(args.Input, ndt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer, null, out _))
                            args.Errors.Add("Invalid Byte");
                    break;
                    case DataType.Int16:
                        if (!short.TryParse(args.Input, ndt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer, null, out _))
                            args.Errors.Add("Invalid Int16");
                    break;
                    case DataType.Int32:
                        if (!int.TryParse(args.Input, ndt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer, null, out _))
                            args.Errors.Add("Invalid Int32");
                    break;
                    case DataType.Int64:
                        if (!long.TryParse(args.Input, ndt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer, null, out _))
                            args.Errors.Add("Invalid Int64");
                    break;
                    case DataType.Float:
                        if (!(ndt == NumericDisplayType.Hexadecimal ? uint.TryParse(args.Input, NumberStyles.HexNumber, null, out _) : float.TryParse(args.Input, out _)))
                            args.Errors.Add("Invalid float");
                    break;
                    case DataType.Double:
                        if (!(ndt == NumericDisplayType.Hexadecimal ? ulong.TryParse(args.Input, NumberStyles.HexNumber, null, out _) : double.TryParse(args.Input, out _)))
                            args.Errors.Add("Invalid double");
                    break;
                    case DataType.String: break;
                    default:              throw new ArgumentOutOfRangeException();
                }
            };
        }

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input) != true) {
            return;
        }

        using IDisposable? token = await memoryEngine360.BeginBusyOperationActivityAsync();
        IConsoleConnection? conn;
        if (token == null || (conn = memoryEngine360.Connection) == null) {
            return;
        }

        using CancellationTokenSource cts = new CancellationTokenSource();
        await ActivityManager.Instance.RunTask(async () => {
            foreach (SavedAddressViewModel result in savedList) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                await MemoryEngine360.WriteAsText(conn, result.Address, result.DataType, result.NumericDisplayType, input.Text, (uint) result.Value.Length);
                string newValue = await MemoryEngine360.ReadAsText(conn, result.Address, result.DataType, result.NumericDisplayType, (uint) result.Value.Length);

                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    result.Value = newValue;
                    if (result.DataType == DataType.String)
                        result.StringLength = (uint) newValue.Length;
                });
            }
        }, cts);
    }
}