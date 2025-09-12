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

namespace MemEngine360.Sequencing.Contexts;

public static class OperationsContextRegistry {
    public static readonly ContextRegistry Registry = new ContextRegistry("Operation");
    
    static OperationsContextRegistry() {
        FixedContextGroup actions = Registry.GetFixedGroup("operations");
        actions.AddCommand("commands.sequencer.DuplicateOperationsCommand", "Duplicate");
        actions.AddDynamicSubGroup((group, ctx, items) => {
            if (!TaskSequenceManager.DataKey.TryGetContext(ctx, out TaskSequenceManager? ui)) {
                return;
            }

            TaskSequenceManagerViewState state = TaskSequenceManagerViewState.GetInstance(ui);
            if (state.SelectedOperations != null && state.SelectedOperations.Count > 0) {
                if (state.SelectedOperations.Count == 1) {
                    BaseSequenceOperation item = state.SelectedOperations[0];
                    items.Add(new CommandContextEntry("commands.sequencer.ToggleOperationEnabledCommand", item.IsEnabled ? "Disable" : "Enable"));
                }
                else {
                    items.Add(new CommandContextEntry("commands.sequencer.ToggleOperationEnabledCommand", "Toggle Enabled"));
                }
            }
        });
        
        CommandContextEntry entry = actions.AddCommand("commands.sequencer.ToggleOperationConditionBehaviourCommand", "Skip when conditions not met", "Skips over this operation when conditions not met, otherwise, wait until they are met");
        entry.IsCheckedFunction = e => {
            if (e.CapturedContext != null && BaseSequenceOperation.DataKey.TryGetContext(e.CapturedContext, out BaseSequenceOperation? operation)) {
                return operation.ConditionBehaviour == OperationConditionBehaviour.Skip;
            }
            
            return false;
        };

        entry.AddContextChangeHandler(BaseSequenceOperation.DataKey, (e, oldUI, newUI) => {
            if (oldUI != null)
                oldUI.ConditionBehaviourChanged -= OperationOnConditionBehaviourChanged;
            if (newUI != null)
                newUI.ConditionBehaviourChanged += OperationOnConditionBehaviourChanged;
        });

        actions.AddSeparator();
        actions.AddCommand("commands.sequencer.DeleteOperationSelectionCommand", "Delete");
        return;

        void OperationOnConditionBehaviourChanged(BaseSequenceOperation sender) {
            entry.RaiseIsCheckedChanged();
        }
    }
}