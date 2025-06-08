// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemoryEngine360.
// 
// MemoryEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemoryEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemoryEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using MemEngine360.Engine;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands.ATM;

public class ToggleSavedAddressAutoRefreshCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IEngineUI.EngineUIDataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IEngineUI.EngineUIDataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
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