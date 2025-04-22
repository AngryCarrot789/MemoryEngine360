using MemEngine360.Engine;
using PFXToolKitUI.Avalonia.CommandUsages;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Avalonia.Commands;

/// <summary>
/// Base class for a button command usage that needs to hook into mem engine events
/// </summary>
public abstract class MemEngineButtonCommandUsage : SimpleButtonCommandUsage {
    private MemoryEngine360? engine;

    public MemoryEngine360? Engine => this.engine;
    
    protected MemEngineButtonCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnContextChanged() {
        base.OnContextChanged();
        MemoryEngine360? oldEngine = this.engine;
        MemoryEngine360? newEngine = null;
        bool hasEngine = this.GetContextData() is IContextData data && MemoryEngine360.DataKey.TryGetContext(data, out newEngine);
        if (hasEngine && oldEngine == newEngine) {
            return;
        }

        this.engine = newEngine;
        this.OnEngineChanged(oldEngine, newEngine);
    }

    protected virtual void OnEngineChanged(MemoryEngine360? oldEngine, MemoryEngine360? newEngine) {
    }
}