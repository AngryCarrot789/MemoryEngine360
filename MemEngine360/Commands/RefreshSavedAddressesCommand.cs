using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands;

public class RefreshSavedAddressesCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine)) {
            return Executability.Invalid;
        }

        if (engine.Connection == null || engine.IsConnectionBusy)
            return Executability.ValidButCannotExecute;

        return Executability.Valid;
    }

    protected override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine) || engine.Connection == null) {
            return Task.CompletedTask;
        }

        if (engine.IsConnectionBusy) {
            return IMessageDialogService.Instance.ShowMessage("Busy", "Connection is currently busy elsewhere");
        }
        
        engine.ScanningProcessor.RefreshSavedAddresses();
        return Task.CompletedTask;
    }
}