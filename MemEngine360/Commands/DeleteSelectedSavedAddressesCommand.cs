using System.Collections.ObjectModel;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public class DeleteSelectedSavedAddressesCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? engine))
            return Executability.Invalid;
        
        return engine.SavedAddressesSelectionManager.Count < 1 ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? engine)) {
            return;
        }

        ObservableCollection<SavedAddressViewModel> items = engine.MemoryEngine360.ScanningProcessor.SavedAddresses;
        if (engine.SavedAddressesSelectionManager.Count == items.Count) {
            items.Clear();
        }
        else {
            List<SavedAddressViewModel> list = engine.SavedAddressesSelectionManager.SelectedItemList.ToList();
            foreach (SavedAddressViewModel address in list) {
                items.Remove(address);
            }
        }
    }
}