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
using PFXToolKitUI.Composition;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Sequencing.View;

public delegate void TaskSequencerViewStateSelectedSequenceChangedEventHandler(TaskSequenceManagerViewState sender, TaskSequence? oldSelectedSequence, TaskSequence? newSelectedSequence);

/// <summary>
/// Represents the persistent state of the task sequencer view
/// </summary>
public sealed class TaskSequenceManagerViewState {
    private TaskSequence? primarySelectedSequence;

    /// <summary>
    /// Gets the task sequence manager for this state
    /// </summary>
    public TaskSequenceManager Manager { get; }

    /// <summary>
    /// Gets the observable list of selected sequences
    /// </summary>
    public ObservableList<TaskSequence> SelectedSequences { get; }

    /// <summary>
    /// Gets the list of selected operations. This changes after <see cref="PrimarySelectedSequence"/> changes. This property is for convenience
    /// </summary>
    public ObservableList<BaseSequenceOperation>? SelectedOperations => this.PrimarySelectedSequence != null ? TaskSequenceViewState.GetInstance(this.PrimarySelectedSequence).SelectedOperations : null;

    /// <summary>
    /// Gets the list of selected operations. This changes after <see cref="PrimarySelectedSequence"/> changes. This property is for convenience
    /// </summary>
    public ObservableList<BaseSequenceCondition>? SelectedConditions => this.PrimarySelectedSequence != null ? TaskSequenceViewState.GetInstance(this.PrimarySelectedSequence).SelectedConditions : null;

    /// <summary>
    /// Gets or sets the primary sequence, that is, the sequence whose operations are being presented
    /// </summary>
    public TaskSequence? PrimarySelectedSequence {
        get => this.primarySelectedSequence;
        private set => PropertyHelper.SetAndRaiseINE(ref this.primarySelectedSequence, value, this, (t, o, n) => t.PrimarySelectedSequenceChanged?.Invoke(t, o, n));
    }

    /// <summary>
    /// Fired when the <see cref="PrimarySelectedSequence"/> changes
    /// </summary>
    public event TaskSequencerViewStateSelectedSequenceChangedEventHandler? PrimarySelectedSequenceChanged;

    private TaskSequenceManagerViewState(TaskSequenceManager manager) {
        this.Manager = manager;
        this.Manager.Sequences.ItemsRemoved += this.OnSequencesRemoved;
        this.Manager.Sequences.ItemReplaced += this.OnSequenceReplaced;

        this.SelectedSequences = new ObservableList<TaskSequence>();
        this.SelectedSequences.CollectionChanged += this.OnSelectedSequencesCollectionChanged;
    }

    private void OnSequencesRemoved(IObservableList<TaskSequence> list, int index, IList<TaskSequence> items) {
        if (this.SelectedSequences.Count > 0) {
            foreach (TaskSequence sequence in items) {
                this.SelectedSequences.Remove(sequence);
            }
        }
    }

    private void OnSequenceReplaced(IObservableList<TaskSequence> list, int index, TaskSequence oldItem, TaskSequence newItem) {
        if (this.SelectedSequences.Count > 0)
            this.SelectedSequences.Remove(oldItem);
        // I don't think we should select newItem... right?
    }

    private void OnSelectedSequencesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        this.PrimarySelectedSequence = this.SelectedSequences.Count == 1 ? this.SelectedSequences[0] : null;
    }

    public static TaskSequenceManagerViewState GetInstance(TaskSequenceManager manager) {
        return ((IComponentManager) manager).GetOrCreateComponent((t) => new TaskSequenceManagerViewState((TaskSequenceManager) t));
    }
}