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
using PFXToolKitUI.Activities;
using PFXToolKitUI.Composition;
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Sequencing;

/// <summary>
/// The base class for a sequencer operation
/// </summary>
public abstract class BaseSequenceOperation : ITransferableData, IComponentManager, IConditionsHost {
    public static readonly DataKey<BaseSequenceOperation> DataKey = DataKeys.Create<BaseSequenceOperation>(nameof(BaseSequenceOperation));

    public TransferableData TransferableData { get; }
    
    public ComponentStorage ComponentStorage => field ??= new ComponentStorage(this);

    /// <summary>
    /// Gets the sequence that owns this operation
    /// </summary>
    public TaskSequence? TaskSequence { get; private set; }

    public OperationState State {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.StateChanged);
    } = OperationState.NotRunning;

    /// <summary>
    /// Gets or sets whether this operation is enabled, meaning can it actually run when the sequence is running
    /// </summary>
    public bool IsEnabled {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsEnabledChanged);
    } = true;

    /// <summary>
    /// Gets or sets the behaviour for when conditions are not met
    /// </summary>
    public OperationConditionBehaviour ConditionBehaviour {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.ConditionBehaviourChanged);
    } = OperationConditionBehaviour.Wait;

    /// <summary>
    /// Gets a short readable description of this operation, e.g. "Set Memory"
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Gets the random trigger helper for this operation
    /// </summary>
    public RandomTriggerHelper RandomTriggerHelper { get; }

    TaskSequence? IConditionsHost.TaskSequence => this.TaskSequence;

    public ObservableList<BaseSequenceCondition> Conditions { get; }

    public event EventHandler? StateChanged;
    public event EventHandler? IsEnabledChanged;
    public event EventHandler? ConditionBehaviourChanged;

    private const string CheckNotRunningMessage = "Cannot modify condition list of an operation while its owner sequence is running";

    protected BaseSequenceOperation() {
        this.TransferableData = new TransferableData(this);
        this.RandomTriggerHelper = new RandomTriggerHelper();
        this.Conditions = new ObservableList<BaseSequenceCondition>();
        this.Conditions.ValidateAdd += (list, index, items) => {
            foreach (BaseSequenceCondition item in items) {
                CheckAddCondition(this, item);
            }
        };

        this.Conditions.ValidateRemove += (list, index, count) => this.TaskSequence?.CheckNotRunning(CheckNotRunningMessage);
        this.Conditions.ValidateMove += (list, oldIdx, newIdx, item) => this.TaskSequence?.CheckNotRunning(CheckNotRunningMessage);
        this.Conditions.ValidateReplace += (list, index, oldItem, newItem) => {
            CheckAddCondition(this, newItem);
        };

        this.Conditions.ItemsAdded += (list, index, items) => {
            foreach (BaseSequenceCondition condition in items) {
                BaseSequenceCondition.InternalSetOwner(condition, this);
            }
        };

        this.Conditions.ItemsRemoved += (list, index, items) => {
            foreach (BaseSequenceCondition condition in items) {
                BaseSequenceCondition.InternalSetOwner(condition, null);
            }
        };

        this.Conditions.ItemReplaced += (list, index, oldItem, newItem) => {
            BaseSequenceCondition.InternalSetOwner(oldItem, null);
            BaseSequenceCondition.InternalSetOwner(newItem, this);
        };

        return;

        static void CheckAddCondition(BaseSequenceOperation self, BaseSequenceCondition item) {
            if (item == null)
                throw new ArgumentNullException(nameof(item), "Cannot add a null condition");
            if (item.Owner == self)
                throw new InvalidOperationException("Condition already exists in this operation. It must be removed first");
            if (item.Owner != null)
                throw new InvalidOperationException("Condition already exists in another container. It must be removed first");
            self.TaskSequence?.CheckNotRunning(CheckNotRunningMessage);
        }
    }

    /// <summary>
    /// Runs this operation. Sets <see cref="IsRunning"/> to true before calling <see cref="RunOperation"/>
    /// and then false once completed, even after an exception
    /// </summary>
    /// <param name="ctx">Sequence execution information (containing progress, connection and more)</param>
    /// <param name="token">A token used to indicate whether the user wants to stop the sequence executing</param>
    /// <exception cref="InvalidOperationException">Already running</exception>
    /// <exception cref="OperationCanceledException">Operation cancelled</exception>
    /// <returns>A task that represents the operation</returns>
    internal async Task Run(SequenceExecutionContext ctx, CancellationToken token) {
        if (!this.IsEnabled || !await this.RandomTriggerHelper.TryTrigger(token)) {
            return;
        }

        Task task = this.RunOperation(ctx, token);
        bool wasCompletedSync = task.IsCompleted;
        await task;

        if (wasCompletedSync) {
            // Yielding here prevents this operation from running about 1 million times a second,
            // which may be battering the UI thread with IsRunning being switched so often.
            // Yielding brings it down to about 15,000 times a second.
            // Of course, may be different for faster/slower systems than a ryzen 5
            await Task.Yield();
        }
    }

    /// <summary>
    /// Invoked on all operations before any operation is run. 
    /// </summary>
    protected internal virtual void OnSequenceStarted() {
    }

    /// <summary>
    /// Invoked on all operations after the sequence has finished. <see cref="RunOperation"/> may
    /// not have actually been called (maybe sequence was stopped mid-way or conditions not met)
    /// </summary>
    protected internal virtual void OnSequenceStopped() {
    }

    /// <summary>
    /// Runs this operation. This method is called in a background thread and is NOT running in an <see cref="ActivityTask"/>
    /// </summary>
    /// <param name="ctx">Sequence execution information (containing progress, connection and more)</param>
    /// <param name="token">A token used to indicate whether the user wants to stop the sequence executing</param>
    /// <returns>A task that represents the operation</returns>
    protected abstract Task RunOperation(SequenceExecutionContext ctx, CancellationToken token);

    internal static void InternalSetSequence(BaseSequenceOperation operation, TaskSequence? sequence) {
        operation.TaskSequence = sequence;
    }

    /// <summary>
    /// Creates a clone of this operation as if the user created it by hand.
    /// </summary>
    /// <returns></returns>
    protected abstract BaseSequenceOperation CreateCloneCore();

    public BaseSequenceOperation CreateClone() {
        BaseSequenceOperation cloned = this.CreateCloneCore();
        cloned.IsEnabled = this.IsEnabled;
        cloned.ConditionBehaviour = this.ConditionBehaviour;
        cloned.RandomTriggerHelper.CopySettingsFrom(this.RandomTriggerHelper);
        foreach (BaseSequenceCondition condition in this.Conditions) {
            cloned.Conditions.Add(condition.CreateClone());
        }

        return cloned;
    }
}