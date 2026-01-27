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
using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Sequencing;

/// <summary>
/// Manager of task sequences
/// </summary>
public class TaskSequenceManager : IComponentManager, IUserLocalContext {
    private readonly ObservableList<TaskSequence> activeSequences;

    /// <summary>
    /// The list of task sequences
    /// </summary>
    public ObservableList<TaskSequence> Sequences { get; }

    /// <summary>
    /// Gets a list of sequences that are currently running. Note that items are added to/remove
    /// from this collection BEFORE <see cref="TaskSequence.IsRunning"/> changes
    /// </summary>
    public ReadOnlyObservableList<TaskSequence> ActiveSequences { get; }

    /// <summary>
    /// Gets the memory engine that owns this sequencer
    /// </summary>
    public MemoryEngine MemoryEngine { get; }
    
    public IMutableContextData UserContext { get; } = new ContextData();

    ComponentStorage IComponentManager.ComponentStorage => field ??= new ComponentStorage(this);

    public TaskSequenceManager(MemoryEngine engine) {
        Console.WriteLine();
        
        this.MemoryEngine = engine ?? throw new ArgumentNullException(nameof(engine));
        this.Sequences = new ObservableList<TaskSequence>();
        this.Sequences.ValidateAdd += (list, index, items) => {
            foreach (TaskSequence item in items) {
                if (item == null)
                    throw new ArgumentNullException(nameof(items), "List contains a null entry");
                if (item.Manager == this)
                    throw new InvalidOperationException("Entry already exists in this entry. It must be removed first");
                if (item.Manager != null)
                    throw new InvalidOperationException("Entry already exists in another sequence manager. It must be removed first");
                Debug.Assert(!item.IsRunning, "Impossible for a sequence to run without a manager");
            }
        };

        this.Sequences.ValidateRemove += (list, index, count) => {
            for (int i = 0; i < count; i++)
                list[index + i].CheckNotRunning("Cannot remove sequence while it's running");
        };

        this.Sequences.ValidateMove += (list, oldIdx, newIdx, item) => item.CheckNotRunning("Cannot move sequence while it's running");
        this.Sequences.ValidateReplace += (list, index, oldItem, newItem) => {
            if (newItem == null)
                throw new ArgumentNullException(nameof(newItem), "Cannot replace sequence with null");

            oldItem.CheckNotRunning("Cannot replace item while it's running");
            newItem.CheckNotRunning("Replacement item cannot be running");
        };

        this.Sequences.ItemsAdded += (list, index, items) => {
            foreach (TaskSequence item in items)
                item.myManager = this;
        };

        this.Sequences.ItemsRemoved += (list, index, items) => {
            foreach (TaskSequence item in items)
                item.myManager = null;
        };

        this.Sequences.ItemReplaced += (list, index, oldItem, newItem) => {
            oldItem.myManager = null;
            newItem.myManager = this;
        };

        this.activeSequences = new ObservableList<TaskSequence>();
        this.ActiveSequences = new ReadOnlyObservableList<TaskSequence>(this.activeSequences);

        this.MemoryEngine.ConnectionAboutToChange += this.OnMemoryEngineConnectionAboutToChange;

        this.Sequences.Add(new TaskSequence() {
            DisplayName = "Freeze BO1 Primary Ammo",
            RunCount = -1,
            // Conditions = {
            //     new CompareMemoryCondition() {
            //         Address = 0xDEADBEEF,
            //         CompareTo = new DataValueInt64(1234567),
            //         CompareType = CompareType.LessThanOrEquals,
            //     }
            // }
            Operations = {
                new SetMemoryOperation() {
                    Address = new StaticAddress(0x8303AA08),
                    DataValueProvider = new ConstantDataProvider(IDataValue.CreateNumeric((int) 25))
                }
            }
        });

        this.Sequences.Add(new TaskSequence() {
            DisplayName = "Literally sleep for 1s",
            Operations = {
                new DelayOperation(1000)
            }
        });

        if (Debugger.IsAttached) {
            TaskSequence sequence = new TaskSequence() {
                DisplayName = "Test Conditions | Shooting BO1 Sniper"
            };

            sequence.Operations.Add(new DelayOperation(100));

            sequence.Conditions.Add(new CompareMemoryCondition() { OutputMode = ConditionOutputMode.WhileMet, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0) });
            sequence.Conditions.Add(new CompareMemoryCondition() { OutputMode = ConditionOutputMode.WhileNotMet, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0) });
            sequence.Conditions.Add(new CompareMemoryCondition() { OutputMode = ConditionOutputMode.ChangeToMet, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0) });
            sequence.Conditions.Add(new CompareMemoryCondition() { OutputMode = ConditionOutputMode.ChangeToNotMet, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0) });
            sequence.Conditions.Add(new CompareMemoryCondition() { OutputMode = ConditionOutputMode.WhileMetOnce, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0) });
            sequence.Conditions.Add(new CompareMemoryCondition() { OutputMode = ConditionOutputMode.WhileNotMetOnce, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0) });
            sequence.Conditions.Add(new CompareMemoryCondition() { OutputMode = ConditionOutputMode.ChangeToMetOnce, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0) });
            sequence.Conditions.Add(new CompareMemoryCondition() { OutputMode = ConditionOutputMode.ChangeToNotMetOnce, Address = new StaticAddress(0x8303A82A), CompareType = CompareType.NotEquals, CompareTo = new DataValueInt32(0) });
            this.Sequences.Add(sequence);
        }
    }

    private async Task OnMemoryEngineConnectionAboutToChange(object? s, ConnectionChangingEventArgs args) {
        args.Progress.SetCaptionAndText("Task Sequencer", "Waiting for sequences to complete...");
        List<TaskSequence> items = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => this.ActiveSequences.ToList());
        if (items.Count > 0) {
            bool isShuttingDown = ((MemoryEngine) s!).IsShuttingDown;
            foreach (TaskSequence sequence in items) {
                if (sequence.UseEngineConnection || isShuttingDown)
                    sequence.RequestCancellation();
            }

            await Task.WhenAll(items.Select(x => x.WaitForCompletion()));
        }
    }

    internal static void InternalSetIsRunning(TaskSequenceManager tsm, TaskSequence sequence, bool isRunning) {
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