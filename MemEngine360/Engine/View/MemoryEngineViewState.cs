using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.Utils.Events;

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
    /// Returns the selection model for the saved address table
    /// </summary>
    public TreeSelectionModel<BaseAddressTableEntry> AddressTableSelectionManager { get; }

    /// <summary>
    /// Gets or sets if the activity list is visible or not
    /// </summary>
    public bool IsActivityListVisible {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsActivityListVisibleChanged);
    }

    /// <summary>
    /// Fired when someone requests for the engine window to be focused
    /// </summary>
    public event EventHandler? RequestWindowFocus;

    /// <summary>
    /// Requests a saved address to be focused in the UI. This will also select the item
    /// </summary>
    public event EventHandler<BaseAddressTableEntry>? RequestFocusOnSavedAddress;

    public event EventHandler? IsActivityListVisibleChanged;
    
    private MemoryEngineViewState(MemoryEngine engine) {
        this.Engine = engine;
        this.SelectedScanResults = new ListSelectionModel<ScanResultViewModel>(this.Engine.ScanningProcessor.ScanResults);

        AddressTableManager atm = engine.AddressTableManager;
        this.AddressTableSelectionManager = new TreeSelectionModel<BaseAddressTableEntry>(
            atm.RootEntry,
            static arg => arg.AddressTableManager != null,
            static arg => arg.Parent,
            static arg => arg is AddressTableGroupEntry g ? g.Items : null);
    }

    public void RaiseRequestWindowFocus() => this.RequestWindowFocus?.Invoke(this, EventArgs.Empty);

    public void RaiseRequestFocusOnSavedAddress(BaseAddressTableEntry entry) {
        ArgumentNullException.ThrowIfNull(entry);
        this.RequestFocusOnSavedAddress?.Invoke(this, entry);
    }

    public static MemoryEngineViewState GetInstance(MemoryEngine engine) {
        return ((IComponentManager) engine).GetOrCreateComponent((t) => new MemoryEngineViewState((MemoryEngine) t));
    }
}