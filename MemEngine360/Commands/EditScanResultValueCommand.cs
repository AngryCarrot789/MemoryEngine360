using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Commands;

public class EditScanResultValueCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ScanResultViewModel.DataKey.TryGetContext(e.ContextData, out ScanResultViewModel? result)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ScanResultViewModel.DataKey.TryGetContext(e.ContextData, out ScanResultViewModel? result)) {
            return;
        }

        IConsoleConnection? connection = result.ScanningProcessor.MemoryEngine360.Connection;
        if (connection == null) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Not connected to a console");
            return;
        }

        if (connection.IsBusy) {
            string desc = result.ScanningProcessor.IsScanning ? "The connection is busy scanning the xbox memory. Cancel to modify values" : "Connection is currently busy somewhere";
            await IMessageDialogService.Instance.ShowMessage("Busy", "Connection is busy. Concurrent operations dangerous", desc);
            return;
        }

        SingleUserInputInfo input = new SingleUserInputInfo("Change value at 0x" + result.Address.ToString("X8"), "Immediately change the value at this address", "Value", result.CurrentValue);
        input.Validate = (args) => {
            switch (result.DataType) {
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
                    if (args.Input.Length > result.FirstValue.Length)
                        args.Errors.Add("Length must not exceed the first value's length, otherwise, you'd be writing into an unknown area");
                break;
                default: throw new ArgumentOutOfRangeException();
            }
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input) == true) {
            MemoryEngine360 engine = result.ScanningProcessor.MemoryEngine360;
            result.PreviousValue = result.CurrentValue;
            if (engine.Connection != null) {
                await MemoryEngine360.WriteAsText(engine.Connection, result.Address, result.DataType, MemoryEngine360.NumericDisplayType.Normal, input.Text, (uint) result.FirstValue.Length);
                result.CurrentValue = await MemoryEngine360.ReadAsText(engine.Connection, result.Address, result.DataType, MemoryEngine360.NumericDisplayType.Normal, (uint) result.FirstValue.Length);
            }
            else {
                result.CurrentValue = input.Text;
            }
        }
    }
}