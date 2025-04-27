using System.Collections.ObjectModel;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public class DeleteSelectedScanResultsCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? engine))
            return Executability.Invalid;

        return engine.ScanResultSelectionManager.Count < 1 ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? engine)) {
            return;
        }

        ObservableCollection<ScanResultViewModel> items = engine.MemoryEngine360.ScanningProcessor.ScanResults;
        if (engine.ScanResultSelectionManager.Count == items.Count) {
            items.Clear();
        }
        else {
            List<ScanResultViewModel> list = engine.ScanResultSelectionManager.SelectedItemList.ToList();
            foreach (ScanResultViewModel address in list) {
                items.Remove(address);
            }
        }
    }
}