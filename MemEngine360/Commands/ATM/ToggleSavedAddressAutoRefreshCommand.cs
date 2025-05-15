using MemEngine360.Engine;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands.ATM;

public class ToggleSavedAddressAutoRefreshCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            return;
        }

        List<AddressTableEntry> list = ui.AddressTableSelectionManager.SelectedItems.Where(x => x.Entry is AddressTableEntry).Select(x => (AddressTableEntry) x.Entry).ToList();
        if (list.Count < 1) {
            return;
        }

        int countDisabled = 0;
        foreach (AddressTableEntry entry in list) {
            if (!entry.IsAutoRefreshEnabled) {
                countDisabled++;
            }
        }

        bool isEnabled = list.Count == 1 ? (countDisabled != 0) : countDisabled >= (list.Count / 2);
        foreach (AddressTableEntry entry in list) {
            entry.IsAutoRefreshEnabled = isEnabled;
        }
    }
}