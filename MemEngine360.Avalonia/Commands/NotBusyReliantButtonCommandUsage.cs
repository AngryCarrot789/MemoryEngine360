using MemEngine360.Engine;

namespace MemEngine360.Avalonia.Commands;

public abstract class NotBusyReliantButtonCommandUsage : MemEngineButtonCommandUsage {
    protected NotBusyReliantButtonCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnEngineChanged(MemoryEngine360? oldEngine, MemoryEngine360? newEngine) {
        base.OnEngineChanged(oldEngine, newEngine);
        if (oldEngine != null)
            oldEngine.IsBusyChanged -= this.OnIsBusyChanged;
        if (newEngine != null)
            newEngine.IsBusyChanged += this.OnIsBusyChanged;
    }

    private void OnIsBusyChanged(MemoryEngine360 sender) {
        this.UpdateCanExecuteLater();
    }
}