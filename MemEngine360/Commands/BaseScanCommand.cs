using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public abstract class BaseScanCommand : Command {
    protected sealed override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine))
            return Executability.Invalid;
        
        return this.CanExecuteCore(engine, e);
    }
    
    protected sealed override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine))
            return Task.CompletedTask;

        return this.ExecuteCommandAsync(engine, e);
    }
    
    protected abstract Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e);
    protected abstract Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e);
}

public class FirstScanCommand : BaseScanCommand {
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

public class NextScanCommand : BaseScanCommand {
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

public class ResetScanCommand : BaseScanCommand {
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