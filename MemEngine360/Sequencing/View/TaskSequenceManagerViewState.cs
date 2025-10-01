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
using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.View;

public delegate void TaskSequencerViewStateSelectedSequenceChangedEventHandler(TaskSequenceManagerViewState sender, TaskSequence? oldSelectedSequence, TaskSequence? newSelectedSequence);

public delegate void TaskSequenceManagerViewStateConditionHostChangedEventHandler(TaskSequenceManagerViewState sender, IConditionsHost? oldConditionHost, IConditionsHost? newConditionHost);

/// <summary>
/// Represents the persistent state of the task sequencer view
/// </summary>
public sealed class TaskSequenceManagerViewState {
    private TaskSequence? primarySelectedSequence;
    private IConditionsHost? conditionHost;

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
    public ListSelectionModel<BaseSequenceOperation>? SelectedOperations => this.PrimarySelectedSequence != null ? TaskSequenceViewState.GetInstance(this.PrimarySelectedSequence).SelectedOperations : null;

    /// <summary>
    /// Gets the selected conditions based on the <see cref="ConditionHost"/>. This is a convenience property
    /// </summary>
    public ListSelectionModel<BaseSequenceCondition>? SelectedConditionsFromHost {
        get {
            switch (this.conditionHost) {
                case TaskSequence sequence:    return TaskSequenceViewState.GetInstance(sequence).SelectedConditions;
                case BaseSequenceOperation op: return SequenceOperationViewState.GetInstance(op).SelectedConditions;
                default:                       return null;
            }
        }
    }

    /// <summary>
    /// Gets or sets the primary sequence, that is, the sequence whose operations are being presented
    /// </summary>
    public TaskSequence? PrimarySelectedSequence {
        get => this.primarySelectedSequence;
        private set => PropertyHelper.SetAndRaiseINE(ref this.primarySelectedSequence, value, this, (t, o, n) => t.PrimarySelectedSequenceChanged?.Invoke(t, o, n));
    }

    /// <summary>
    /// Gets or sets the object that presents the conditions in the UI
    /// </summary>
    public IConditionsHost? ConditionHost {
        get => this.conditionHost;
        set => PropertyHelper.SetAndRaiseINE(ref this.conditionHost, value, this, static (t, o, n) => t.ConditionHostChanged?.Invoke(t, o, n));
    }

    /// <summary>
    /// Fired when the <see cref="PrimarySelectedSequence"/> changes
    /// </summary>
    public event TaskSequencerViewStateSelectedSequenceChangedEventHandler? PrimarySelectedSequenceChanged;

    public event TaskSequenceManagerViewStateConditionHostChangedEventHandler? ConditionHostChanged;

    private TaskSequenceManagerViewState(TaskSequenceManager taskSequenceManager) {
        this.TaskSequenceManager = taskSequenceManager;
        this.SelectedSequences = new ListSelectionModel<TaskSequence>(this.TaskSequenceManager.Sequences);
        this.SelectedSequences.SelectionChanged += this.OnSelectedSequencesCollectionChanged;
    }

    private void OnSelectedSequencesCollectionChanged(object? sender, ListSelectionModelChangedEventArgs e) {
        this.ConditionHost = this.PrimarySelectedSequence = this.SelectedSequences.Count == 1 ? this.SelectedSequences[0] : null;
    }

    public static TaskSequenceManagerViewState GetInstance(TaskSequenceManager manager) {
        return ((IComponentManager) manager).GetOrCreateComponent((t) => new TaskSequenceManagerViewState((TaskSequenceManager) t));
    }
}