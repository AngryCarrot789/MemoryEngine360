using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Engine;

public interface IMemEngineUI {
    public static readonly DataKey<IMemEngineUI> DataKey = DataKey<IMemEngineUI>.Create("IMemEngineUI");

    /// <summary>
    /// Gets the memory engine
    /// </summary>
    MemoryEngine360 MemoryEngine360 { get; }

    /// <summary>
    /// Gets the scan result list selection manager
    /// </summary>
    IListSelectionManager<ScanResultViewModel> ScanResultSelectionManager { get; }
    
    /// <summary>
    /// Gets the saved address list selection manager
    /// </summary>
    IListSelectionManager<SavedAddressViewModel> SavedAddressesSelectionManager { get; }
}