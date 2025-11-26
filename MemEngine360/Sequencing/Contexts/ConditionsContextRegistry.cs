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

using MemEngine360.Sequencing.View;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Selections;

namespace MemEngine360.Sequencing.Contexts;

public static class ConditionsContextRegistry {
    private readonly struct CurrentConditionInfo(BaseSequenceCondition condition, EventHandler isEnabledHandler) {
        public readonly BaseSequenceCondition Condition = condition;
        public readonly EventHandler IsEnabledHandler = isEnabledHandler;
    }

    private static readonly DataKey<CurrentConditionInfo> CurrentConditionDataKey = DataKeys.Create<CurrentConditionInfo>(nameof(ConditionsContextRegistry) + "_internal_" + nameof(CurrentConditionDataKey));
    
    public static readonly ContextRegistry Registry = new ContextRegistry("Condition");

    static ConditionsContextRegistry() {
        FixedWeightedMenuEntryGroup actions = Registry.GetFixedGroup("conditions");
        actions.AddHeader("Edit");
        actions.AddCommand("commands.sequencer.EditConditionOutputModeCommand", "Edit output mode");
        actions.AddHeader("General");
        actions.AddCommand("commands.sequencer.DuplicateConditionsCommand", "Duplicate");
        actions.AddCommand("commands.sequencer.ToggleConditionEnabledCommand", "Toggle Enabled").AddContextChangedHandler(TaskSequenceManager.DataKey, (entry, e) => {
            if (CurrentConditionDataKey.TryGetContext(entry.UserContext, out CurrentConditionInfo current)) {
                current.Condition.IsEnabledChanged -= current.IsEnabledHandler;
                entry.UserContext.Remove(CurrentConditionDataKey);
                entry.DisplayName = "Toggle Enabled";
                entry.IsCheckedFunction = null;
            }

            if (e.NewValue != null) {
                ListSelectionModel<BaseSequenceCondition>? conditions = TaskSequenceManagerViewState.GetInstance(e.NewValue).SelectedConditionsFromHost;
                if (conditions != null && conditions.Count == 1) {
                    BaseSequenceCondition newCond = conditions.First;
                    CurrentConditionInfo info = new CurrentConditionInfo(newCond, (s, e) => entry.RaiseIsCheckedChanged());
                    entry.UserContext.Set(CurrentConditionDataKey, info);
                    entry.DisplayName = "Is Enabled";
                    entry.IsCheckedFunction = static e => CurrentConditionDataKey.GetContext(e.UserContext).Condition.IsEnabled;
                    newCond.IsEnabledChanged += info.IsEnabledHandler;
                }
            }
        });

        actions.AddSeparator();
        actions.AddCommand("commands.sequencer.DeleteConditionSelectionCommand", "Delete");
    }
}