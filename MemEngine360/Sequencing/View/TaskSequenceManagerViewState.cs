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

using MemEngine360.Sequencing.Conditions;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Sequencing.View;

/// <summary>
/// Represents the persistent state of the task sequencer view
/// </summary>
public sealed class TaskSequenceManagerViewState {
    public static readonly DataKey<TaskSequenceManagerViewState> DataKey = DataKeys.Create<TaskSequenceManagerViewState>(nameof(TaskSequenceManagerViewState));

    /// <summary>
    /// Gets the task sequence manager for this state
    /// </summary>
    public TaskSequenceManager TaskSequenceManager { get; }

    /// <summary>
    /// Gets the observable list of selected sequences
    /// </summary>
    public ListSelectionModel<TaskSequence> SelectedSequences { get; }

    /// <summary>
    /// Gets the selected operations from <see cref="PrimarySelectedSequence"/>. This is a convenience property
    /// </summary>
    public ListSelectionModel<BaseSequenceOperation>? SelectedOperations => this.PrimarySelectedSequence != null ? TaskSequenceViewState.GetInstance(this.PrimarySelectedSequence, this.TopLevelIdentifier).SelectedOperations : null;

    /// <summary>
    /// Gets the selected conditions based on the <see cref="ConditionHost"/>. This is a convenience property
    /// </summary>
    public ListSelectionModel<BaseSequenceCondition>? SelectedConditionsFromHost {
        get {
            switch (this.ConditionHost) {
                case TaskSequence sequence:    return TaskSequenceViewState.GetInstance(sequence, this.TopLevelIdentifier).SelectedConditions;
                case BaseSequenceOperation op: return SequenceOperationViewState.GetInstance(op, this.TopLevelIdentifier).SelectedConditions;
                default:                       return null;
            }
        }
    }

    /// <summary>
    /// Gets or sets the primary sequence, that is, the sequence whose operations are being presented
    /// </summary>
    public TaskSequence? PrimarySelectedSequence {
        get => field;
        private set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.PrimarySelectedSequenceChanged);
    }

    /// <summary>
    /// Gets or sets the object that presents the conditions in the UI
    /// </summary>
    public IConditionsHost? ConditionHost {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.ConditionHostChanged);
    }

    /// <summary>
    /// Gets the top level identifier that identifies which top level this view state is associated with
    /// </summary>
    public TopLevelIdentifier TopLevelIdentifier { get; }
    
    /// <summary>
    /// Fired when the <see cref="PrimarySelectedSequence"/> changes
    /// </summary>
    public event EventHandler<ValueChangedEventArgs<TaskSequence?>>? PrimarySelectedSequenceChanged;

    public event EventHandler<ValueChangedEventArgs<IConditionsHost?>>? ConditionHostChanged;

    // The `taskSequenceManager` identifies the model, however, models can be used in multiple UIs
    // The `identifier` identifies the exact window effectively.
    
    private TaskSequenceManagerViewState(TaskSequenceManager taskSequenceManager, TopLevelIdentifier topLevelIdentifier) {
        this.TaskSequenceManager = taskSequenceManager;
        this.TopLevelIdentifier = topLevelIdentifier;
        this.SelectedSequences = new ListSelectionModel<TaskSequence>(this.TaskSequenceManager.Sequences);
        this.SelectedSequences.SelectionChanged += this.OnSelectedSequencesCollectionChanged;
    }

    private void OnSelectedSequencesCollectionChanged(object? sender, ListSelectionModelChangedEventArgs<TaskSequence> e) {
        this.ConditionHost = this.PrimarySelectedSequence = this.SelectedSequences.Count == 1 ? this.SelectedSequences.First : null;
    }

    public static TaskSequenceManagerViewState GetInstance(TaskSequenceManager manager, TopLevelIdentifier topLevelIdentifier) {
        return TopLevelDataMap.GetInstance(manager).GetOrCreate(topLevelIdentifier, manager, static (s, i) => new TaskSequenceManagerViewState((TaskSequenceManager) s!, i));
    }
}