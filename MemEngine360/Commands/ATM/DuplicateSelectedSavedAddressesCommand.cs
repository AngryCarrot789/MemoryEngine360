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
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands.ATM;

public class DuplicateSelectedSavedAddressesCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IEngineUI.DataKey.TryGetContext(e.ContextData, out IEngineUI? engine))
            return Executability.Invalid;

        return engine.AddressTableSelectionManager.Count < 1 ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IEngineUI.DataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
            return;
        }

        // Create list of clones, ordered by their index in the sequence list
        List<BaseAddressTableEntry> selection = ui.AddressTableSelectionManager.SelectedItemList.Select(x => x.Entry).ToList();
        ui.AddressTableSelectionManager.SelectedItemList.Clear();

        List<BaseAddressTableEntry> clonedItems = new List<BaseAddressTableEntry>();
        Dictionary<AddressTableGroupEntry, List<(BaseAddressTableEntry, int)>> duplication = GetEffectiveOrderedDuplication(selection);
        foreach (KeyValuePair<AddressTableGroupEntry, List<(BaseAddressTableEntry, int)>> entry in duplication) {
            int offset = 0;
            foreach ((BaseAddressTableEntry, int) pair in entry.Value) {
                BaseAddressTableEntry cloned = pair.Item1.CreateClone();
                cloned.Description = TextIncrement.GetNextText(pair.Item1.Parent!.Items.Select(x => x.Description ?? "").ToList(), pair.Item1.Description ?? "", false);
                pair.Item1.Parent!.InsertEntry(offset + pair.Item2 + 1, cloned); // +1 to add after the existing item
                clonedItems.Add(cloned);
                offset++;
            }
        }
        
        ui.AddressTableSelectionManager.Select(clonedItems.Select(x => ui.GetATEntryUI(x)));
    }

    public static Dictionary<AddressTableGroupEntry, List<(BaseAddressTableEntry, int)>> GetEffectiveOrderedDuplication(List<BaseAddressTableEntry> source) {
        List<BaseAddressTableEntry> roots = [];
        foreach (BaseAddressTableEntry item in source) {
            for (int i = roots.Count - 1; i >= 0; i--) {
                if (IsParent(roots[i], item) || IsParent(item, roots[i])) {
                    roots.RemoveAt(i);
                }
            }

            roots.Add(item);
        }

        Dictionary<AddressTableGroupEntry, List<(BaseAddressTableEntry, int)>> items = [];
        foreach (BaseAddressTableEntry item in roots) {
            if (!items.TryGetValue(item.Parent!, out List<(BaseAddressTableEntry, int)>? entry))
                items[item.Parent!] = entry = [];
            entry.Add((item, item.GetIndexInParent()));
        }

        foreach (KeyValuePair<AddressTableGroupEntry, List<(BaseAddressTableEntry, int)>> entry in items) {
            entry.Value.Sort((a, b) => a.Item2.CompareTo(b.Item2));
        }

        return items;
    }

    private static bool IsParent(BaseAddressTableEntry @this, BaseAddressTableEntry check, bool self = true) {
        for (BaseAddressTableEntry? entry = self ? @this : @this.Parent; entry != null; entry = entry.Parent) {
            if (entry == check) {
                return true;
            }
        }

        return false;
    }
}