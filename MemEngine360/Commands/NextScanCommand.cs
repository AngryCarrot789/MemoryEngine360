using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public class NextScanCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        return !engine.ScanningProcessor.CanPerformNextScan ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        if (engine.ScanningProcessor.CanPerformNextScan) {
            return engine.ScanningProcessor.ScanNext();
        }

        return Task.CompletedTask;
    }
}