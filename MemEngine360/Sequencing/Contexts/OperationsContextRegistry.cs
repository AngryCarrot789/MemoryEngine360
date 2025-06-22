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

using PFXToolKitUI.AdvancedMenuService;

namespace MemEngine360.Sequencing.Contexts;

public static class OperationsContextRegistry {
    public static readonly ContextRegistry Registry = new ContextRegistry("Operation");
    
    static OperationsContextRegistry() {
        FixedContextGroup actions = Registry.GetFixedGroup("operations");
        actions.AddCommand("commands.sequencer.DuplicateOperationsCommand", "Duplicate");
        actions.AddDynamicSubGroup((group, ctx, items) => {
            if (!ITaskSequenceManagerUI.DataKey.TryGetContext(ctx, out ITaskSequenceManagerUI? ui)) {
                return;
            }

            int count = ui.OperationSelectionManager.SelectedItemList.Count;
            if (count < 1) {
                return;
            }
            
            if (count == 1) {
                IOperationItemUI item = ui.OperationSelectionManager.SelectedItemList[0];
                items.Add(new CommandContextEntry("commands.sequencer.ToggleOperationEnabledCommand", item.Operation.IsEnabled ? "Disable" : "Enable"));
            }
            else {
                items.Add(new CommandContextEntry("commands.sequencer.ToggleOperationEnabledCommand", "Toggle Enabled"));
            }
        });
        
        actions.AddSeparator();
        actions.AddCommand("commands.sequencer.DeleteOperationSelectionCommand", "Delete");
    }
}