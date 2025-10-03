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

public static class OperationsContextRegistry {
    private readonly struct CurrentOperationInfo(BaseSequenceOperation operation, BaseSequenceOperationEventHandler isEnabledHandler) {
        public readonly BaseSequenceOperation Operation = operation;
        public readonly BaseSequenceOperationEventHandler IsEnabledHandler = isEnabledHandler;
    }

    private static readonly DataKey<CurrentOperationInfo> CurrentOperationDataKey = DataKeys.Create<CurrentOperationInfo>(nameof(OperationsContextRegistry) + "_internal_" + nameof(CurrentOperationDataKey));

    public static readonly ContextRegistry Registry = new ContextRegistry("Operation");

    static OperationsContextRegistry() {
        FixedContextGroup actions = Registry.GetFixedGroup("operations");
        actions.AddCommand("commands.sequencer.DuplicateOperationsCommand", "Duplicate");

        CommandContextEntry entry1 = actions.AddCommand("commands.sequencer.ToggleOperationEnabledCommand", "Toggle Enabled");
        entry1.AddContextChangeHandler(TaskSequenceManager.DataKey, (entry, oldManager, newManager) => {
            if (CurrentOperationDataKey.TryGetContext(entry.UserContext, out CurrentOperationInfo currentOperation)) {
                currentOperation.Operation.IsEnabledChanged -= currentOperation.IsEnabledHandler;

                entry.UserContext.Remove(CurrentOperationDataKey);
                entry.DisplayName = "Toggle Enabled";
                entry.IsCheckedFunction = null;
            }

            if (newManager != null) {
                ListSelectionModel<BaseSequenceOperation>? operations = TaskSequenceManagerViewState.GetInstance(newManager).SelectedOperations;
                if (operations != null && operations.Count == 1) {
                    BaseSequenceOperation newOp = operations[0];
                    CurrentOperationInfo info = new CurrentOperationInfo(newOp, _ => entry.RaiseIsCheckedChanged());
                    entry.UserContext.Set(CurrentOperationDataKey, info);
                    entry.DisplayName = "Is Enabled";
                    entry.IsCheckedFunction = static e => CurrentOperationDataKey.GetContext(e.UserContext).Operation.IsEnabled;
                    newOp.IsEnabledChanged += info.IsEnabledHandler;
                }
            }
        });

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