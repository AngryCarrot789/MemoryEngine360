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

using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.View;

public delegate void TaskSequenceViewStatePrimarySelectedOperationChangedEventHandler(TaskSequenceViewState sender, BaseSequenceOperation? oldPrimarySelectedOperation, BaseSequenceOperation? newPrimarySelectedOperation);

/// <summary>
/// Represents the persistent view state of a task sequence
/// </summary>
public class TaskSequenceViewState {
    private BaseSequenceOperation? primarySelectedOperation;

    /// <summary>
    /// Gets the sequence that this view state represents
    /// </summary>
    public TaskSequence Sequence { get; }

    /// <summary>
    /// Gets the list of selected operations
    /// </summary>
    public ListSelectionModel<BaseSequenceOperation> SelectedOperations { get; }

    /// <summary>
    /// Gets the list of selected conditions
    /// </summary>
    public ListSelectionModel<BaseSequenceCondition> SelectedConditions { get; }

    /// <summary>
    /// Gets the primary selected operation
    /// </summary>
    public BaseSequenceOperation? PrimarySelectedOperation {
        get => this.primarySelectedOperation;
        private set => PropertyHelper.SetAndRaiseINE(ref this.primarySelectedOperation, value, this, static (t, o, n) => t.PrimarySelectedOperationChanged?.Invoke(t, o, n));
    }

    public event TaskSequenceViewStatePrimarySelectedOperationChangedEventHandler? PrimarySelectedOperationChanged;

    internal TaskSequenceViewState(TaskSequence sequence) {
        this.Sequence = sequence;
        this.SelectedOperations = new ListSelectionModel<BaseSequenceOperation>(sequence.Operations);
        this.SelectedConditions = new ListSelectionModel<BaseSequenceCondition>(sequence.Conditions);
        this.SelectedOperations.SelectionChanged += this.OnSelectedOperationsCollectionChanged;
    }

    private void OnSelectedOperationsCollectionChanged(object? sender, ListSelectionModelChangedEventArgs e) {
        this.PrimarySelectedOperation = this.SelectedOperations.Count == 1 ? this.SelectedOperations[0] : null;
        
        // Null means the TaskSequence was deleted/not yet added, so
        // we don't have to panic here about not updating ConditionHost
        TaskSequenceManager? manager = this.Sequence.Manager;
        if (manager != null) {
            TaskSequenceManagerViewState.GetInstance(manager).ConditionHost = this.PrimarySelectedOperation;
        }
    }

    public static TaskSequenceViewState GetInstance(TaskSequence sequence) => sequence.internalViewState ??= new TaskSequenceViewState(sequence);
}