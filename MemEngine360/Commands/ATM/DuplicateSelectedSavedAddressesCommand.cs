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
using MemEngine360.Engine.View;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands.ATM;

public class DuplicateSelectedSavedAddressesCommand : BaseSavedAddressSelectionCommand {
    protected override Task ExecuteCommandAsync(List<BaseAddressTableEntry> entries, MemoryEngine engine, CommandEventArgs e) {
        // Create list of clones, ordered by their index in the sequence list
        List<BaseAddressTableEntry> selection = entries.ToList();
        MemoryEngineViewState vs = MemoryEngineViewState.GetInstance(engine);
        
        List<BaseAddressTableEntry> clonedItems = new List<BaseAddressTableEntry>();
        Dictionary<AddressTableGroupEntry, List<(BaseAddressTableEntry, int)>> duplication = GetEffectiveOrderedDuplication(selection);
        foreach (KeyValuePair<AddressTableGroupEntry, List<(BaseAddressTableEntry, int)>> entry in duplication) {
            int offset = 0;
            foreach ((BaseAddressTableEntry, int) pair in entry.Value) {
                BaseAddressTableEntry cloned = pair.Item1.CreateClone();
                cloned.Description = TextIncrement.GetNextText(pair.Item1.Parent!.Items.Select(x => x.Description ?? "").ToList(), pair.Item1.Description ?? "", false);
                pair.Item1.Parent!.Items.Insert(offset + pair.Item2 + 1, cloned); // +1 to add after the existing item
                clonedItems.Add(cloned);
                offset++;
            }
        }

        vs.AddressTableSelectionManager.SetSelection(clonedItems);
        return Task.CompletedTask;
    }

    public static Dictionary<AddressTableGroupEntry, List<(BaseAddressTableEntry, int)>> GetEffectiveOrderedDuplication(List<BaseAddressTableEntry> source) {
        return HierarchicalDuplicationUtils.GetEffectiveOrderedDuplication(source, x => x.Parent!, e => e.Parent!.IndexOf(e));
    }
}