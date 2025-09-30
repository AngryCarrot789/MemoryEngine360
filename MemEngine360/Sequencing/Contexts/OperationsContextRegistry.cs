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

namespace MemEngine360.Sequencing.Contexts;

public static class OperationsContextRegistry {
    private static readonly DataKey<BaseSequenceOperation> CurrentOperationDataKey = DataKeys.Create<BaseSequenceOperation>(nameof(OperationsContextRegistry) + "_internal_" + nameof(CurrentOperationDataKey));
    
    public static readonly ContextRegistry Registry = new ContextRegistry("Operation");

    static OperationsContextRegistry() {
        FixedContextGroup actions = Registry.GetFixedGroup("operations");
        actions.AddCommand("commands.sequencer.DuplicateOperationsCommand", "Duplicate");

        CommandContextEntry entry1 = actions.AddCommand("commands.sequencer.ToggleOperationEnabledCommand", "Toggle Enabled");
        entry1.AddContextChangeHandler(TaskSequenceManager.DataKey, (entry, oldManager, newManager) => {
            if (oldManager != null)
                TaskSequenceManagerViewState.GetInstance(oldManager).PrimarySelectedSequenceChanged -= OnPrimarySelectedSequenceChanged;
            if (newManager != null)
                TaskSequenceManagerViewState.GetInstance(newManager).PrimarySelectedSequenceChanged += OnPrimarySelectedSequenceChanged;

            OnSelectionChanged(newManager != null ? TaskSequenceManagerViewState.GetInstance(newManager) : null);
            return;

            void OnPrimarySelectedSequenceChanged(TaskSequenceManagerViewState sender, TaskSequence? oldSeq, TaskSequence? newSeq) {
                OnSelectionChanged(sender);
            }

            void OnSelectionChanged(TaskSequenceManagerViewState? sender) {
                if (CurrentOperationDataKey.TryGetContext(entry.UserContext, out BaseSequenceOperation? currentOperation)) {
                    entry.UserContext.Remove(CurrentOperationDataKey);
                    entry.DisplayName = "Toggle Enabled";
                    entry.IsCheckedFunction = null;
                    currentOperation.IsEnabledChanged -= OnIsOperationEnabledChanged;
                }

                if (sender != null && sender.SelectedOperations != null && sender.SelectedOperations.Count == 1) {
                    BaseSequenceOperation newOp = sender.SelectedOperations[0];
                    entry.UserContext.Set(CurrentOperationDataKey, newOp);
                    entry.DisplayName = "Is Enabled";
                    entry.IsCheckedFunction = static e => CurrentOperationDataKey.GetContext(e.UserContext)!.IsEnabled;
                    newOp.IsEnabledChanged += OnIsOperationEnabledChanged;
                }
                
                return;

                void OnIsOperationEnabledChanged(BaseSequenceOperation op) => entry.RaiseIsCheckedChanged();
            }
        });

        // actions.AddDynamicSubGroup((group, ctx, items) => {
        //     if (!TaskSequenceManager.DataKey.TryGetContext(ctx, out TaskSequenceManager? ui)) {
        //         return;
        //     }
        //
        //     TaskSequenceManagerViewState state = TaskSequenceManagerViewState.GetInstance(ui);
        //     if (state.SelectedOperations != null && state.SelectedOperations.Count > 0) {
        //         if (state.SelectedOperations.Count == 1) {
        //             BaseSequenceOperation item = state.SelectedOperations[0];
        //             items.Add(new CommandContextEntry("commands.sequencer.ToggleOperationEnabledCommand", item.IsEnabled ? "Disable" : "Enable"));
        //         }
        //         else {
        //             items.Add(new CommandContextEntry("commands.sequencer.ToggleOperationEnabledCommand", "Toggle Enabled"));
        //         }
        //     }
        // });

        CommandContextEntry entry = actions.AddCommand("commands.sequencer.ToggleOperationConditionBehaviourCommand", "Skip when conditions not met", "Skips over this operation when conditions not met, otherwise, wait until they are met");
        entry.AddIsCheckedChangeUpdaterForEvent(BaseSequenceOperation.DataKey, nameof(BaseSequenceOperation.ConditionBehaviourChanged));
        entry.IsCheckedFunction = e => {
            if (e.CapturedContext != null && BaseSequenceOperation.DataKey.TryGetContext(e.CapturedContext, out BaseSequenceOperation? operation)) {
                return operation.ConditionBehaviour == OperationConditionBehaviour.Skip;
            }

            return false;
        };

        actions.AddSeparator();
        actions.AddCommand("commands.sequencer.DeleteOperationSelectionCommand", "Delete");
    }
}