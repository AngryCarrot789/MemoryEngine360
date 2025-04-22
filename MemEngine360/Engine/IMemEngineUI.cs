using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Engine;

public interface IMemEngineUI {
    public static readonly DataKey<IMemEngineUI> DataKey = DataKey<IMemEngineUI>.Create("IMemEngineUI");
    
    IResultListSelectionManager<ScanResultViewModel> ScanResultSelectionManager { get; }
    IResultListSelectionManager<SavedAddressViewModel> SavedAddressesSelectionManager { get; }
}