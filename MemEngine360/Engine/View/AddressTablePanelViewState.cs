using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Engine.View;

/// <summary>
/// Represents the persistent state of the address table
/// </summary>
public sealed class AddressTablePanelViewState {
    public MemoryEngine Engine { get; }
    
    /// <summary>
    /// Gets the list of selected address table entries
    /// </summary>
    public ObservableList<BaseAddressTableEntry> SelectedEntries { get; }
    
    public AddressTablePanelViewState(MemoryEngine engine) {
        this.Engine = engine;
    }
    
    public static AddressTablePanelViewState GetInstance(MemoryEngine engine) {
        return ((IComponentManager) engine).GetOrCreateComponent((t) => new AddressTablePanelViewState((MemoryEngine) t));
    }
}