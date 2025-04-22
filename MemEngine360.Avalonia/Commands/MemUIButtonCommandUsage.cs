using MemEngine360.Engine;
using PFXToolKitUI.Avalonia.CommandUsages;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Avalonia.Commands;

public class MemUIButtonCommandUsage : SimpleButtonCommandUsage {
    public IMemEngineUI? UI { get; private set; }

    protected MemUIButtonCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnContextChanged() {
        base.OnContextChanged();
        IMemEngineUI? oldEngine = this.UI;
        IMemEngineUI? newEngine = null;
        bool hasEngine = this.GetContextData() is IContextData data && IMemEngineUI.DataKey.TryGetContext(data, out newEngine);
        if (hasEngine && oldEngine == newEngine) {
            return;
        }

        this.UI = newEngine;
        this.OnEngineChanged(oldEngine, newEngine);
    }

    protected virtual void OnEngineChanged(IMemEngineUI? oldUI, IMemEngineUI? newUI) {
    }
}