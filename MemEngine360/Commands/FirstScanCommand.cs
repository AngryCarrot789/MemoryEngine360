using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public class FirstScanCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        return !engine.ScanningProcessor.CanPerformFirstScan ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        if (engine.ScanningProcessor.CanPerformFirstScan) {
            return engine.ScanningProcessor.ScanNext();
        }
        
        return Task.CompletedTask;
    }
}