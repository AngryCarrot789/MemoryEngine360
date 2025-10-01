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
    private readonly struct CurrentOperationInfo(BaseSequenceOperation operation, BaseSequenceOperationEventHandler isEnabledHandler) {
        public readonly BaseSequenceOperation Operation = operation;
        public readonly BaseSequenceOperationEventHandler IsEnabledHandler = isEnabledHandler;
    }

    private static readonly DataKey<CurrentOperationInfo> CurrentOperationDataKey = DataKeys.Create<CurrentOperationInfo>(nameof(OperationsContextRegistry) + "_internal_" + nameof(CurrentOperationDataKey));
    private static readonly DataKey<BaseContextEntry> IsEnabledEntryDataKey = DataKeys.Create<BaseContextEntry>(nameof(OperationsContextRegistry) + "_internal_" + nameof(IsEnabledEntryDataKey));

    public static readonly ContextRegistry Registry = new ContextRegistry("Operation");

    static OperationsContextRegistry() {
        FixedContextGroup actions = Registry.GetFixedGroup("operations");
        actions.AddCommand("commands.sequencer.DuplicateOperationsCommand", "Duplicate");

        CommandContextEntry entry1 = actions.AddCommand("commands.sequencer.ToggleOperationEnabledCommand", "Toggle Enabled");
        entry1.AddContextChangeHandler(TaskSequenceManager.DataKey, ToggleEnabled_TaskSequenceManagerChanged);

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

    private static void ToggleEnabled_TaskSequenceManagerChanged(BaseContextEntry ctxEntry, TaskSequenceManager? oldManager, TaskSequenceManager? newManager) {
        // We need both CurrentOperationDataKey and IsEnabledEntryDataKey to prevent "blind" closure allocations,
        // which are closures that capture local variables for a specific call frame.
        // 
        // In this case, it would have been OnPrimarySelectedSequenceChanged because it has to capture the `ctxEntry` parameter.
        // So to get around this, we make TaskSequenceManager inherit IUserLocalContext, and we create a data key,
        // IsEnabledEntryDataKey, to store the ctxEntry inside the TSM, so we can access it while the context menu is open. 

        if (oldManager != null) {
            TaskSequenceManagerViewState state = TaskSequenceManagerViewState.GetInstance(oldManager);
            state.PrimarySelectedSequenceChanged -= OnPrimarySelectedSequenceChanged;
            state.TaskSequenceManager.UserContext.Remove(IsEnabledEntryDataKey);
        }

        if (newManager != null) {
            TaskSequenceManagerViewState state = TaskSequenceManagerViewState.GetInstance(newManager);
            state.TaskSequenceManager.UserContext.Set(IsEnabledEntryDataKey, ctxEntry);
            state.PrimarySelectedSequenceChanged += OnPrimarySelectedSequenceChanged;
        }

        OnSelectionChanged(ctxEntry, newManager != null ? TaskSequenceManagerViewState.GetInstance(newManager) : null);
        return;

        static void OnPrimarySelectedSequenceChanged(TaskSequenceManagerViewState sender, TaskSequence? oldSeq, TaskSequence? newSeq) {
            OnSelectionChanged(IsEnabledEntryDataKey.GetContext(sender.TaskSequenceManager.UserContext)!, sender);
        }

        static void OnSelectionChanged(BaseContextEntry ctxEntry, TaskSequenceManagerViewState? sender) {
            // Because the handler to IsEnabledChanged requires accessing the ctxEntry, it either has
            // to be a closure (like it is now) or BaseOperationSequence stores the ctxEntry via a DataKey.
            // In this case it's simpler to just store the operation itself and
            // its event handler closure inside the ctxEntry via a data key,
            // since it's also accessed by the IsCheckedFunction too
            
            if (CurrentOperationDataKey.TryGetContext(ctxEntry.UserContext, out CurrentOperationInfo currentOperation)) {
                ctxEntry.UserContext.Remove(CurrentOperationDataKey);
                ctxEntry.DisplayName = "Toggle Enabled";
                ctxEntry.IsCheckedFunction = null;
                currentOperation.Operation.IsEnabledChanged -= currentOperation.IsEnabledHandler;
            }

            if (sender?.SelectedOperations is { Count: 1 }) {
                BaseSequenceOperation newOp = sender.SelectedOperations[0];
                CurrentOperationInfo info = new CurrentOperationInfo(newOp, op => ctxEntry.RaiseIsCheckedChanged());
                ctxEntry.UserContext.Set(CurrentOperationDataKey, info);
                ctxEntry.DisplayName = "Is Enabled";
                ctxEntry.IsCheckedFunction = static e => CurrentOperationDataKey.GetContext(e.UserContext).Operation.IsEnabled;
                newOp.IsEnabledChanged += info.IsEnabledHandler;
            }
        }
    }
}