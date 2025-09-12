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
using MemEngine360.Sequencing.View;
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Sequencing;

public delegate void BaseSequenceOperationEventHandler(BaseSequenceOperation sender);

public delegate void BaseSequenceOperationStateChangedEventHandler(BaseSequenceOperation sender, OperationState oldState, OperationState newState);

/// <summary>
/// The base class for a sequencer operation
/// </summary>
public abstract class BaseSequenceOperation : ITransferableData, IConditionsHost {
    public static readonly DataKey<BaseSequenceOperation> DataKey = DataKey<BaseSequenceOperation>.Create(nameof(BaseSequenceOperation));
    
    internal SequenceOperationViewState? internalViewState; // UI stuff, but not publicly exposed so this should be okay. saves using IComponentManager

    private OperationState state = OperationState.NotRunning;
    private bool isEnabled = true;
    private OperationConditionBehaviour conditionBehaviour = OperationConditionBehaviour.Wait;

    public TransferableData TransferableData { get; }

    /// <summary>
    /// Gets the sequence that owns this operation
    /// </summary>
    public TaskSequence? TaskSequence { get; private set; }

    public OperationState State {
        get => this.state;
        set => PropertyHelper.SetAndRaiseINE(ref this.state, value, this, static (t, o, n) => t.StateChanged?.Invoke(t, o, n));
    }

    /// <summary>
    /// Gets or sets whether this operation is enabled, meaning can it actually run when the sequence is running
    /// </summary>
    public bool IsEnabled {
        get => this.isEnabled;
        set => PropertyHelper.SetAndRaiseINE(ref this.isEnabled, value, this, static t => t.IsEnabledChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the behaviour for when conditions are not met
    /// </summary>
    public OperationConditionBehaviour ConditionBehaviour {
        get => this.conditionBehaviour;
        set => PropertyHelper.SetAndRaiseINE(ref this.conditionBehaviour, value, this, static t => t.ConditionBehaviourChanged?.Invoke(t));
    }

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

    public event BaseSequenceOperationStateChangedEventHandler? StateChanged;
    public event BaseSequenceOperationEventHandler? IsEnabledChanged;
    public event BaseSequenceOperationEventHandler? ConditionBehaviourChanged;

    protected BaseSequenceOperation() {
        this.TransferableData = new TransferableData(this);
        this.RandomTriggerHelper = new RandomTriggerHelper();
        this.Conditions = new ObservableList<BaseSequenceCondition>();
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
    /// Runs this operation
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
    public abstract BaseSequenceOperation CreateClone();
}