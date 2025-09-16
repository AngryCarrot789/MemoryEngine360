using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.View;

public delegate void MemoryEngineViewStateEventHandler(MemoryEngineViewState sender);

public delegate void FocusSavedAddressEventHandler(MemoryEngineViewState viewState, BaseAddressTableEntry entry);

public sealed class MemoryEngineViewState {
    private bool isActivityListVisible;
    
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
        get => this.isActivityListVisible;
        set => PropertyHelper.SetAndRaiseINE(ref this.isActivityListVisible, value, this, static t => t.IsActivityListVisibleChanged?.Invoke(t));
    }

    /// <summary>
    /// Fired when someone requests for the engine window to be focused
    /// </summary>
    public event EventHandler? RequestWindowFocus;

    /// <summary>
    /// Requests a saved address to be focused in the UI. This will also select the item
    /// </summary>
    public event FocusSavedAddressEventHandler? RequestFocusOnSavedAddress;

    public event MemoryEngineViewStateEventHandler? IsActivityListVisibleChanged;
    
    private MemoryEngineViewState(MemoryEngine engine) {
        this.Engine = engine;
        this.SelectedScanResults = new ListSelectionModel<ScanResultViewModel>(this.Engine.ScanningProcessor.ScanResults);

        AddressTableManager atm = engine.AddressTableManager;
        this.AddressTableSelectionManager = new TreeSelectionModel<BaseAddressTableEntry>(
            atm.RootEntry.Items,
            static arg => arg.Parent?.Parent == null ? null : arg.Parent,
            static arg => arg is AddressTableGroupEntry g ? g.Items : Enumerable.Empty<BaseAddressTableEntry>());
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