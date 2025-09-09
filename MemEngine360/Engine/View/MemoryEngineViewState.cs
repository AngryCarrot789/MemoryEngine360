using PFXToolKitUI.Composition;

namespace MemEngine360.Engine.View;

public sealed class MemoryEngineViewState {
    public MemoryEngine Engine { get; }

    /// <summary>
    /// Fired when someone requests for the engine window to be foucsed
    /// </summary>
    public event EventHandler? RequestWindowFocus;

    private MemoryEngineViewState(MemoryEngine engine) {
        this.Engine = engine;
    }

    public void RaiseRequestWindowFocus() => this.RequestWindowFocus?.Invoke(this, EventArgs.Empty);

    public static MemoryEngineViewState GetInstance(MemoryEngine engine) {
        return ((IComponentManager) engine).GetOrCreateComponent((t) => new MemoryEngineViewState((MemoryEngine) t));
    }
}