using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public abstract class BaseMemoryEngineCommand : Command {
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