using System.Collections.ObjectModel;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public class AddSelectedScanResultsToSavedAddressListCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            return Executability.Invalid;
        }

        if (ui.ScanResultSelectionManager.Count < 1) {
            return Executability.ValidButCannotExecute;
        }

        // Probably too intensive for CanExecute
        // List<ScanResultViewModel> list1 = ui.ScanResultSelectionManager.SelectedItemList.ToList();
        // HashSet<uint> addresses = ui.MemoryEngine360.ScanningProcessor.SavedAddresses.Select(x => x.Address).ToHashSet();
        // if (list1.All(x => addresses.Contains(x.Address))) {
        //     return Executability.ValidButCannotExecute;
        // }

        return Executability.Valid;
    }

    protected override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            return Task.CompletedTask;
        }

        if (ui.ScanResultSelectionManager.Count < 1) {
            return Task.CompletedTask;
        }

        ObservableCollection<SavedAddressViewModel> saved = ui.MemoryEngine360.ScanningProcessor.SavedAddresses;
        List<ScanResultViewModel> selection = ui.ScanResultSelectionManager.SelectedItemList.ToList();
        HashSet<uint> existing = ui.MemoryEngine360.ScanningProcessor.SavedAddresses.Select(x => x.Address).ToHashSet();
        foreach (ScanResultViewModel result in selection) {
            if (!existing.Contains(result.Address)) {
                saved.Add(new SavedAddressViewModel(result));
            }
        }
        
        if (selection.All(x => existing.Contains(x.Address))) {
            
        }

        return Task.CompletedTask;
    }
}