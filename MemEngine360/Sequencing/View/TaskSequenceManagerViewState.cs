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
using System.Diagnostics;
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
        this.SelectedSequences = new ObservableList<TaskSequence>();
        this.SelectedSequences.BeforeItemAdded += this.VerifyAddItem;
        this.SelectedSequences.BeforeItemReplace += this.VerifyReplaceItem;
        this.SelectedSequences.CollectionChanged += this.OnSelectedSequencesCollectionChanged;
    }

    private void OnSelectedSequencesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        this.PrimarySelectedSequence = this.SelectedSequences.Count == 1 ? this.SelectedSequences[0] : null;
    }

    public static TaskSequenceManagerViewState GetInstance(TaskSequenceManager manager) {
        return ((IComponentManager) manager).GetOrCreateComponent((t) => new TaskSequenceManagerViewState((TaskSequenceManager) t));
    }

    private void VerifyAddItem(IObservableList<TaskSequence> list, int index, TaskSequence item) {
        if (item == null)
            throw new ArgumentException("Cannot add null items");
        if (item.myManager == null)
            throw new ArgumentException("Cannot add item with no manager associated");
        Debug.Assert(this.Manager.Sequences.Contains(item));
        if (item.myManager != this.Manager)
            throw new ArgumentException("Cannot add item in different manager");
    }

    private void VerifyReplaceItem(IObservableList<TaskSequence> list, int index, TaskSequence oldItem, TaskSequence newItem) {
        if (newItem == null)
            throw new ArgumentException("New item is nul");
        if (newItem.myManager == null)
            throw new ArgumentException("New item has no manager associated");
        Debug.Assert(this.Manager.Sequences.Contains(newItem));
        if (newItem.myManager != this.Manager)
            throw new ArgumentException("New item in different manager");
    }
}