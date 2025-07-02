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

using System.Diagnostics;
using MemEngine360.Engine;
using MemEngine360.Engine.Addressing;
using MemEngine360.Sequencing.Conditions;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.Sequencing.Operations;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Sequencing;

public delegate void TaskSequencerManagerSelectedSequenceChangedEventHandler(TaskSequencerManager sender, TaskSequence? oldSelectedSequence, TaskSequence? newSelectedSequence);

public class TaskSequencerManager {
    private readonly ObservableList<TaskSequence> sequences, activeSequences;

    /// <summary>
    /// Gets our operations
    /// </summary>
    public ReadOnlyObservableList<TaskSequence> Sequences { get; }

    /// <summary>
    /// Gets a list of sequences that are currently running. Note that items are added to/remove
    /// from this collection BEFORE <see cref="TaskSequence.IsRunning"/> changes
    /// </summary>
    public ReadOnlyObservableList<TaskSequence> ActiveSequences { get; }

    /// <summary>
    /// Gets the memory engine that owns this sequencer
    /// </summary>
    public MemoryEngine MemoryEngine { get; }

    public TaskSequencerManager(MemoryEngine engine) {
        this.MemoryEngine = engine ?? throw new ArgumentNullException(nameof(engine));
        this.sequences = new ObservableList<TaskSequence>();
        this.Sequences = new ReadOnlyObservableList<TaskSequence>(this.sequences);
        this.activeSequences = new ObservableList<TaskSequence>();
        this.ActiveSequences = new ReadOnlyObservableList<TaskSequence>(this.activeSequences);

        this.MemoryEngine.ConnectionAboutToChange += this.OnMemoryEngineConnectionAboutToChange;

        {
            TaskSequence sequence = new TaskSequence() {
                DisplayName = "Freeze BO1 Primary Ammo",
                RunCount = -1,
                // Conditions = {
                //     new CompareMemoryCondition() {
                //         Address = 0xDEADBEEF,
                //         CompareTo = new DataValueInt64(1234567),
                //         CompareType = CompareType.LessThanOrEquals,
                //     }
                // }
            };

            sequence.AddOperation(new SetMemoryOperation() { Address = new StaticAddress(0x8303AA08), DataValueProvider = new ConstantDataProvider(IDataValue.CreateNumeric((int) 25)) });
            this.AddSequence(sequence);
        }

        {
            TaskSequence sequence = new TaskSequence() {
                DisplayName = "Literally sleep for 1s"
            };

            sequence.AddOperation(new DelayOperation(1000));
            this.AddSequence(sequence);
        }

        if (Debugger.IsAttached) {
            TaskSequence sequence = new TaskSequence() {
                DisplayName = "Test Conditions | Shooting BO1 Sniper"
            };

            sequence.AddOperation(new DelayOperation(100));
            
            sequence.Conditions.Add(new CompareMemoryCondition() {OutputMode = ConditionOutputMode.WhileMet, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0)});
            sequence.Conditions.Add(new CompareMemoryCondition() {OutputMode = ConditionOutputMode.WhileNotMet, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0)});
            sequence.Conditions.Add(new CompareMemoryCondition() {OutputMode = ConditionOutputMode.ChangeToMet, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0)});
            sequence.Conditions.Add(new CompareMemoryCondition() {OutputMode = ConditionOutputMode.ChangeToNotMet, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0)});
            sequence.Conditions.Add(new CompareMemoryCondition() {OutputMode = ConditionOutputMode.WhileMetOnce, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0)});
            sequence.Conditions.Add(new CompareMemoryCondition() {OutputMode = ConditionOutputMode.WhileNotMetOnce, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0)});
            sequence.Conditions.Add(new CompareMemoryCondition() {OutputMode = ConditionOutputMode.ChangeToMetOnce, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0)});
            sequence.Conditions.Add(new CompareMemoryCondition() {OutputMode = ConditionOutputMode.ChangeToNotMetOnce, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0)});
            this.AddSequence(sequence);
        }
    }

    private async Task OnMemoryEngineConnectionAboutToChange(MemoryEngine sender, ulong frame) {
        List<TaskSequence> items = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => this.ActiveSequences.ToList());
        if (items.Count > 0) {
            foreach (TaskSequence sequence in items) {
                if (sequence.UseEngineConnection)
                    sequence.RequestCancellation();
            }

            await Task.WhenAll(items.Select(x => x.WaitForCompletion()));
        }
    }

    public void AddSequence(TaskSequence entry) => this.InsertSequence(this.sequences.Count, entry);

    public void InsertSequence(int index, TaskSequence entry) {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Negative indices not allowed");
        if (index > this.sequences.Count)
            throw new ArgumentOutOfRangeException(nameof(index), $"Index is beyond the range of this list: {index} > count({this.sequences.Count})");
        if (entry == null)
            throw new ArgumentNullException(nameof(entry), "Cannot add a null entry");
        if (entry.Manager == this)
            throw new InvalidOperationException("Entry already exists in this entry. It must be removed first");
        if (entry.Manager != null)
            throw new InvalidOperationException("Entry already exists in another container. It must be removed first");

        // It shouldn't be able to run without a manager set anyway
        entry.CheckNotRunning("Cannot add entry while it is running");

        entry.myManager = this;
        this.sequences.Insert(index, entry);
    }

    public bool RemoveSequence(TaskSequence entry) {
        if (!ReferenceEquals(entry.Manager, this)) {
            return false;
        }

        int idx = this.IndexOf(entry);
        Debug.Assert(idx != -1);
        this.RemoveSequenceAt(idx);

        Debug.Assert(entry.Manager != this, "Entry parent not updated, still ourself");
        Debug.Assert(entry.Manager == null, "Entry parent not updated to null");
        return true;
    }

    public void RemoveSequenceAt(int index) {
        TaskSequence entry = this.sequences[index];
        entry.CheckNotRunning("Cannot remove sequence while it's running");

        try {
            this.sequences.RemoveAt(index);
        }
        finally {
            entry.myManager = null;
        }
    }

    public void ClearSequences() {
        List<TaskSequence> list = this.sequences.ToList();
        foreach (TaskSequence t in list) {
            t.CheckNotRunning("Cannot clear sequences because a sequence is running");
        }

        try {
            this.sequences.Clear();
        }
        finally {
            foreach (TaskSequence t in list) {
                t.myManager = null;
            }
        }
    }

    public int IndexOf(TaskSequence entry) {
        return ReferenceEquals(entry.Manager, this) ? this.sequences.IndexOf(entry) : -1;
    }

    public bool Contains(TaskSequence entry) {
        return this.IndexOf(entry) != -1;
    }

    internal static void InternalSetIsRunning(TaskSequencerManager tsm, TaskSequence sequence, bool isRunning) {
        if (isRunning) {
            Debug.Assert(!tsm.activeSequences.Contains(sequence));
            tsm.activeSequences.Add(sequence);
        }
        else {
            bool removed = tsm.activeSequences.Remove(sequence);
            Debug.Assert(removed);
        }
    }
}