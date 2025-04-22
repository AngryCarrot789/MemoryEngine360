using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public class ResetScanCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        return !engine.ScanningProcessor.CanPerformReset ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        if (engine.ScanningProcessor.CanPerformReset) {
            engine.ScanningProcessor.ResetScan();
        }

        return Task.CompletedTask;
    }
}