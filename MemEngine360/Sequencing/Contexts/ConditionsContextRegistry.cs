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

public static class ConditionsContextRegistry {
    public static readonly ContextRegistry Registry = new ContextRegistry("Condition");

    static ConditionsContextRegistry() {
        FixedContextGroup actions = Registry.GetFixedGroup("conditions");
        actions.AddHeader("Edit");
        actions.AddCommand("commands.sequencer.EditConditionOutputModeCommand", "Edit output mode");
        actions.AddHeader("General");
        actions.AddCommand("commands.sequencer.DuplicateConditionsCommand", "Duplicate");
        actions.AddCommand("commands.sequencer.ToggleConditionEnabledCommand", "Toggle Enabled").AddSimpleContextUpdate(ITaskSequencerUI.DataKey, (e, ui) => {
            if (ui != null && ui.ConditionSelectionManager.SelectedItemList.Count == 1) {
                IConditionItemUI item = ui.ConditionSelectionManager.SelectedItemList[0];
                e.DisplayName = item.Condition.IsEnabled ? "Disable" : "Enable";
            }
            else {
                e.DisplayName = "Toggle Enabled";
            }
        });

        actions.AddSeparator();
        actions.AddCommand("commands.sequencer.DeleteConditionSelectionCommand", "Delete");
    }
}