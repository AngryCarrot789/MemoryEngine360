using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity.Selections;

namespace MemEngine360.Engine.View;

public sealed class MemoryEngineViewState {
    public MemoryEngine Engine { get; }

    /// <summary>
    /// Fired when someone requests for the engine window to be foucsed
    /// </summary>
    public event EventHandler? RequestWindowFocus;

    public ListSelectionModel<ScanResultViewModel> SelectedScanResults { get; }

    private MemoryEngineViewState(MemoryEngine engine) {
        this.Engine = engine;
        this.SelectedScanResults = new ListSelectionModel<ScanResultViewModel>(this.Engine.ScanningProcessor.ScanResults);
    }

    public void RaiseRequestWindowFocus() => this.RequestWindowFocus?.Invoke(this, EventArgs.Empty);

    public static MemoryEngineViewState GetInstance(MemoryEngine engine) {
        return ((IComponentManager) engine).GetOrCreateComponent((t) => new MemoryEngineViewState((MemoryEngine) t));
    }
}