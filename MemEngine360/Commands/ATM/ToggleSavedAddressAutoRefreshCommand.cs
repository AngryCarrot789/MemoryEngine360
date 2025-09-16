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

public class ToggleSavedAddressAutoRefreshCommand : BaseSavedAddressSelectionCommand {
    protected override Executability CanExecuteOverride(List<BaseAddressTableEntry> entries, MemoryEngine engine, CommandEventArgs e) {
        return entries.Any(x => x is AddressTableEntry)
            ? base.CanExecuteOverride(entries, engine, e)
            : Executability.ValidButCannotExecute;
    }

    protected override Task ExecuteCommandAsync(List<BaseAddressTableEntry> entries, MemoryEngine engine, CommandEventArgs e) {
        entries = entries.Where(x => x is AddressTableEntry).ToList();
        if (entries.Count < 1) {
            return Task.CompletedTask;
        }

        int countDisabled = 0;
        foreach (BaseAddressTableEntry item in entries) {
            if (!((AddressTableEntry) item).IsAutoRefreshEnabled) {
                countDisabled++;
            }
        }

        bool isEnabled = entries.Count == 1 ? (countDisabled != 0) : countDisabled >= (entries.Count / 2);
        foreach (BaseAddressTableEntry item in entries) {
            ((AddressTableEntry) item).IsAutoRefreshEnabled = isEnabled;
        }

        return Task.CompletedTask;
    }
}