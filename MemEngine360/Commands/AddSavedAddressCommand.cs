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

        DoubleUserInputInfo info = new DoubleUserInputInfo() {
            Caption = "Add address",
            LabelA = "Memory address (hex)",
            LabelB = "Description (optional)",
            ValidateA = (args) => {
                if (!uint.TryParse(args.Input, NumberStyles.HexNumber, null, out uint address))
                    args.Errors.Add("Invalid memory address");
            }
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
            SavedAddressViewModel vm = new SavedAddressViewModel(engine.ScanningProcessor, uint.Parse(info.TextA, NumberStyles.HexNumber, null)) {
                Description = info.TextB
            };

            engine.ScanningProcessor.SavedAddresses.Add(vm);
            engine.ScanningProcessor.RefreshSavedAddresses();
        }
    }
}