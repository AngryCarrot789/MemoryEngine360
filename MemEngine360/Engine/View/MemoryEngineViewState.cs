using System.Text;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.PropertyEditing.DataTransfer.Enums;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.View;

public sealed class MemoryEngineViewState {
    /// <summary>
    /// Gets the engine model instance
    /// </summary>
    public MemoryEngine Engine { get; }

    /// <summary>
    /// Gets the menu registry for the engine window
    /// </summary>
    public TopLevelMenuRegistry TopLevelMenuRegistry { get; } = new TopLevelMenuRegistry();

    /// <summary>
    /// Returns the selection model for the scan results
    /// </summary>
    public ListSelectionModel<ScanResultViewModel> SelectedScanResults { get; }

    /// <summary>
    /// Fired when someone requests for the engine window to be focused
    /// </summary>
    public event EventHandler? RequestWindowFocus;

    private MemoryEngineViewState(MemoryEngine engine) {
        this.Engine = engine;
        this.SelectedScanResults = new ListSelectionModel<ScanResultViewModel>(this.Engine.ScanningProcessor.ScanResults);
    }

    public void RaiseRequestWindowFocus() => this.RequestWindowFocus?.Invoke(this, EventArgs.Empty);

    public static MemoryEngineViewState GetInstance(MemoryEngine engine) {
        return ((IComponentManager) engine).GetOrCreateComponent((t) => new MemoryEngineViewState((MemoryEngine) t));
    }
}