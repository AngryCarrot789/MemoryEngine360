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

using System.Collections.Specialized;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

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
    public ObservableList<BaseSequenceOperation> SelectedOperations { get; } = new ObservableList<BaseSequenceOperation>();

    /// <summary>
    /// Gets the list of selected conditions
    /// </summary>
    public ObservableList<BaseSequenceCondition> SelectedConditions { get; } = new ObservableList<BaseSequenceCondition>();

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
        this.SelectedOperations.CollectionChanged += this.OnSelectedOperationsCollectionChanged;
    }

    private void OnSelectedOperationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        this.PrimarySelectedOperation = this.SelectedOperations.Count == 1 ? this.SelectedOperations[0] : null;
    }

    public static TaskSequenceViewState GetInstance(TaskSequence sequence) => sequence.internalViewState ??= new TaskSequenceViewState(sequence);
}