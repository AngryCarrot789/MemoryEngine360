using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Commands;

public class EditSavedAddressValueCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!SavedAddressViewModel.DataKey.TryGetContext(e.ContextData, out SavedAddressViewModel? result)) {
            return Executability.Invalid;
        }

        if (result.ScanningProcessor.IsScanning)
            return Executability.ValidButCannotExecute;

        IConsoleConnection? connection = result.ScanningProcessor.MemoryEngine360.Connection;
        if (connection == null || connection.IsBusy)
            return Executability.ValidButCannotExecute;

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!SavedAddressViewModel.DataKey.TryGetContext(e.ContextData, out SavedAddressViewModel? result)) {
            return;
        }

        IConsoleConnection? connection = result.ScanningProcessor.MemoryEngine360.Connection;
        if (connection == null) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Not connected to a console");
            return;
        }

        if (result.ScanningProcessor.MemoryEngine360.IsConnectionBusy) {
            string desc = result.ScanningProcessor.IsScanning ? "The connection is busy scanning the xbox memory. Cancel to modify values" : "Connection is currently busy somewhere";
            await IMessageDialogService.Instance.ShowMessage("Busy", "Connection is busy. Concurrent operations dangerous", desc);
            return;
        }

        SingleUserInputInfo input = new SingleUserInputInfo("Change value at 0x" + result.Address.ToString("X8"), "Immediately change the value at this address", "Value", result.Value);
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
                    if (args.Input.Length != result.Value.Length)
                        args.Errors.Add("New length must match the string length");
                break;
                default: throw new ArgumentOutOfRangeException();
            }
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input) == true) {
            MemoryEngine360 engine = result.ScanningProcessor.MemoryEngine360;
            IDisposable? token = engine.BeginBusyOperation();
            if (token == null) {
                using CancellationTokenSource cts = new CancellationTokenSource();
                ActivityTask task = ActivityManager.Instance.RunTask(async () => {
                    ActivityTask task = ActivityManager.Instance.CurrentTask;
                    task.Progress.Caption = "Busy";
                    task.Progress.Text = "Waiting for busy operations...";

                    do {
                        await Task.Delay(100, task.CancellationToken);
                    } while ((token = engine.BeginBusyOperation()) == null);
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
                if (engine.Connection != null) {
                    await MemoryEngine360.WriteAsText(engine.Connection, result.Address, result.DataType, result.NumericDisplayType, input.Text, (uint) result.Value.Length);
                    result.Value = await MemoryEngine360.ReadAsText(engine.Connection, result.Address, result.DataType, result.NumericDisplayType, (uint) result.Value.Length);
                }
                else {
                    result.Value = input.Text;
                }
            }
            finally {
                token.Dispose();
            }
        }
    }
}