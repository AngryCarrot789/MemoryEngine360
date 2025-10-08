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
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Sequencing.Conditions;
using MemEngine360.Sequencing.Operations;
using MemEngine360.Sequencing.View;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Sequencing;

public delegate void TaskSequenceEventHandler(TaskSequence sender);

public delegate void TaskSequenceDedicatedConnectionChangedEventHandler(TaskSequence sender, IConsoleConnection? oldDedicatedConnection, IConsoleConnection? newDedicatedConnection);

/// <summary>
/// A sequence that contains a list of operations
/// </summary>
public sealed class TaskSequence : IConditionsHost, IUserLocalContext {
    public static readonly DataKey<TaskSequence> DataKey = DataKeys.Create<TaskSequence>(nameof(TaskSequence));

    internal TaskSequenceViewState? internalViewState; // UI stuff, but not publicly exposed so this should be okay. saves using IComponentManager

    internal TaskSequenceManager? myManager;
    private string displayName = "Empty Sequence";
    private int runCount = 1;
    private bool hasEngineConnectionPriority;
    private bool useEngineConnection = true;
    private IConsoleConnection? dedicatedConnection;

    // Running info
    private volatile int isRunning;
    private CancellationTokenSource? myCts;
    private TaskCompletionSource? myTcs;
    private SequenceExecutionContext? myContext;

    /// <summary>
    /// Gets whether this sequence is currently running. This only changes on the main thread
    /// </summary>
    public bool IsRunning {
        get => this.isRunning != 0;
        private set {
            int newState = value ? 1 : 0;
            if (Interlocked.Exchange(ref this.isRunning, newState) != newState) {
                this.IsRunningChanged?.Invoke(this);
            }
        }
    }

    public string DisplayName {
        get => this.displayName;
        set => PropertyHelper.SetAndRaiseINE(ref this.displayName, value, this, static t => t.DisplayNameChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the run count. Setting to a negative value (ideally -1) means infinite runtime until cancelled
    /// </summary>
    public int RunCount {
        get => this.runCount;
        set {
            ApplicationPFX.Instance.Dispatcher.VerifyAccess();
            this.CheckNotRunning($"Cannot change {nameof(this.RunCount)} while running");
            PropertyHelper.SetAndRaiseINE(ref this.runCount, value, this, static t => t.RunCountChanged?.Invoke(t));
        }
    }

    public bool HasEngineConnectionPriority {
        get => this.hasEngineConnectionPriority;
        set {
            ApplicationPFX.Instance.Dispatcher.VerifyAccess();
            this.CheckNotRunning($"Cannot change {nameof(this.HasEngineConnectionPriority)} while running");
            PropertyHelper.SetAndRaiseINE(ref this.hasEngineConnectionPriority, value, this, static t => t.HasEngineConnectionPriorityChanged?.Invoke(t));
        }
    }

    /// <summary>
    /// Gets or sets if we should use the engine connection or our own dedicated connection
    /// </summary>
    public bool UseEngineConnection {
        get => this.useEngineConnection;
        set {
            ApplicationPFX.Instance.Dispatcher.VerifyAccess();
            this.CheckNotRunning($"Cannot change {nameof(this.UseEngineConnection)} while running");
            PropertyHelper.SetAndRaiseINE(ref this.useEngineConnection, value, this, static t => t.UseEngineConnectionChanged?.Invoke(t));
        }
    }

    public IConsoleConnection? DedicatedConnection {
        get => this.dedicatedConnection;
        set {
            ApplicationPFX.Instance.Dispatcher.VerifyAccess();
            this.CheckNotRunning("Cannot change dedicated connection while running");
            PropertyHelper.SetAndRaiseINE(ref this.dedicatedConnection, value, this, static (t, a, b) => t.DedicatedConnectionChanged?.Invoke(t, a, b));
        }
    }

    /// <summary>
    /// Gets the <see cref="TaskSequenceManager"/> that this sequence exists in
    /// </summary>
    public TaskSequenceManager? Manager => this.myManager;

    /// <summary>
    /// Gets the exception encountered while executing an operation in this sequence. This is set before <see cref="IsRunning"/> is changed
    /// </summary>
    public Exception? LastException { get; private set; }

    /// <summary>
    /// Gets our operations
    /// </summary>
    public ObservableList<BaseSequenceOperation> Operations { get; }

    TaskSequence IConditionsHost.TaskSequence => this;

    /// <summary>
    /// Gets the list of conditions that must be met for this sequence to run.
    /// </summary>
    public ObservableList<BaseSequenceCondition> Conditions { get; }

    public IActivityProgress Progress { get; }
    
    public IMutableContextData UserContext { get; } = new ContextData();

    /// <summary>
    /// An event fired when our running state changes. When this fires, the first operation will not have run yet.
    /// </summary>
    public event TaskSequenceEventHandler? IsRunningChanged;

    public event TaskSequenceEventHandler? DisplayNameChanged;
    public event TaskSequenceEventHandler? RunCountChanged;
    public event TaskSequenceEventHandler? HasEngineConnectionPriorityChanged;
    public event TaskSequenceEventHandler? UseEngineConnectionChanged;

    /// <summary>
    /// Raised when <see cref="DedicatedConnection"/> changes. When <see cref="UseEngineConnection"/> is being set to true,
    /// the dedicated connection is closed and set to null, in which case, this event fires BEFORE <see cref="UseEngineConnection"/> is set to true
    /// </summary>
    public event TaskSequenceDedicatedConnectionChangedEventHandler? DedicatedConnectionChanged;

    public TaskSequence() {
        this.Progress = new DispatcherActivityProgress();
        this.Progress.Text = "Sequence not running";
        
        this.Operations = new ObservableList<BaseSequenceOperation>();
        this.Operations.BeforeItemsAdded += (list, index, items) => {
            foreach (BaseSequenceOperation item in items) {
                if (item == null)
                    throw new ArgumentNullException(nameof(item), "Cannot add a null operation");
                if (item.TaskSequence == this)
                    throw new InvalidOperationException("Operation already exists in this operation. It must be removed first");
                if (item.TaskSequence != null)
                    throw new InvalidOperationException("Operation already exists in another container. It must be removed first");
                this.CheckNotRunning("Cannot modify sequence list while running");

                if (item is LabelOperation label && label.LabelName != null && this.Operations.Any(x => x is LabelOperation otherLabel && otherLabel.LabelName == label.LabelName)) {
                    throw new InvalidOperationException("Attempt to add label whose name is already in use");
                }
            }
        };

        this.Operations.BeforeItemsRemoved += (list, index, count) => this.CheckNotRunning("Cannot modify sequence list while running");
        this.Operations.BeforeItemMoved += (list, oldIdx, newIdx, item) => this.CheckNotRunning("Cannot modify sequence list while running");
        this.Operations.BeforeItemReplace += (list, index, oldItem, newItem) => {
            if (newItem == null)
                throw new ArgumentNullException(nameof(newItem), "Cannot replace operation with null");
            this.CheckNotRunning("Cannot modify sequence list while running");
        };

        this.Operations.ItemsAdded += (list, index, items) => {
            bool updateLabels = items.Any(x => x is LabelOperation || x is JumpToLabelOperation);
            foreach (BaseSequenceOperation operation in items) {
                BaseSequenceOperation.InternalSetSequence(operation, this);
            }

            if (updateLabels) {
                this.UpdateAllJumpTargets();
            }
        };
        
        this.Operations.ItemsRemoved += (list, index, removedItems) => {
            List<LabelOperation>? removedLabels = null;

            foreach (BaseSequenceOperation operation in removedItems) {
                BaseSequenceOperation.InternalSetSequence(operation, null);
                if (operation is LabelOperation label) {
                    (removedLabels ??= new List<LabelOperation>()).Add(label);
                }
            }

            if (removedLabels != null) {
                foreach (JumpToLabelOperation ope in this.Operations.OfType<JumpToLabelOperation>()) {
                    if (ope.TargetLabel != null && ope.CurrentTarget != null && removedLabels.Contains(ope.CurrentTarget)) {
                        ope.SetTarget(ope.TargetLabel, null);
                    }
                }
            }
        };

        this.Operations.ItemReplaced += (list, index, oldItem, newItem) => {
            BaseSequenceOperation.InternalSetSequence(oldItem, null);
            if (oldItem is LabelOperation label) {
                foreach (JumpToLabelOperation ope in this.Operations.OfType<JumpToLabelOperation>()) {
                    if (ope.TargetLabel != null && ope.CurrentTarget == label) {
                        // Replacing an item in a list seems more like a removal operation to me,
                        // so I think clearing the target is a better idea here. Though maybe it isn't.
                        ope.SetTarget(ope.TargetLabel, null);
                    }
                }
            }

            BaseSequenceOperation.InternalSetSequence(newItem, this);
        };

        this.Conditions = new ObservableList<BaseSequenceCondition>();
        this.Conditions.BeforeItemsAdded += (list, i, items) => {
            foreach (BaseSequenceCondition item in items) {
                if (item == null)
                    throw new InvalidOperationException("Cannot add null condition");
                if (item.TaskSequence == this)
                    throw new InvalidOperationException("Condition already added to this sequence");
                if (item.TaskSequence != null)
                    throw new InvalidOperationException("Condition already exists in another sequence");
                this.CheckNotRunning("Cannot add conditions while running");
            }
        };

        this.Conditions.BeforeItemsRemoved += (list, index, count) => this.CheckNotRunning($"Cannot remove condition{Lang.S(count)} while running");
        this.Conditions.BeforeItemMoved += (list, oldIdx, newIdx, item) => this.CheckNotRunning("Cannot move conditions while running");
        this.Conditions.BeforeItemReplace += (list, index, oldItem, newItem) => {
            if (newItem == null)
                throw new InvalidOperationException("Cannot replace condition with null");
            this.CheckNotRunning("Cannot replace condition while running");
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
    }

    /// <summary>
    /// Creates a clone of this sequence as if the user had created and configured one to match the current instance
    /// </summary>
    /// <returns></returns>
    public TaskSequence CreateClone() {
        TaskSequence sequence = new TaskSequence() {
            DisplayName = this.displayName,
            RunCount = this.runCount,
            HasEngineConnectionPriority = this.hasEngineConnectionPriority
        };

        foreach (BaseSequenceCondition condition in this.Conditions)
            sequence.Conditions.Add(condition.CreateClone());

        foreach (BaseSequenceOperation operation in this.Operations)
            sequence.Operations.Add(operation.CreateClone());

        return sequence;
    }

    public async Task Run(IConsoleConnection connection, IBusyToken? busyToken, bool isConnectionDedicated) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        this.CheckNotRunning("Cannot run while already running");
        if (this.myManager == null)
            throw new InvalidOperationException("Cannot run standalone without a " + nameof(TaskSequenceManager));

        Debug.Assert(this.myCts == null && this.myTcs == null);
        Debug.Assert(this.myContext == null);
        Debug.Assert(isConnectionDedicated == !this.UseEngineConnection);
        using CancellationTokenSource cts = this.myCts = new CancellationTokenSource();
        this.myTcs = new TaskCompletionSource();
        this.myContext = new SequenceExecutionContext(this, connection, busyToken, isConnectionDedicated);
        this.LastException = null;
        TaskSequenceManager.InternalSetIsRunning(this.myManager!, this, true);
        this.IsRunning = true;
        this.Progress.Caption = this.DisplayName;
        this.Progress.Text = "Running sequence";
        this.Progress.IsIndeterminate = true;

        List<BaseSequenceOperation> operations = this.Operations.ToList();
        foreach (BaseSequenceOperation operation in operations) {
            operation.OnSequenceStarted();
            foreach (BaseSequenceCondition condition in operation.Conditions)
                condition.OnSequenceStarted();
        }

        foreach (BaseSequenceCondition condition in this.Conditions)
            condition.OnSequenceStarted();

        CancellationToken token = this.myCts.Token;
        Task task = Task.Run(async () => {
            int remainingRunCount = this.runCount;
            while ((remainingRunCount < 0 || remainingRunCount != 0) && !token.IsCancellationRequested) {
                if (this.Conditions.Count > 0) {
                    this.Progress.Text = "Waiting for sequence conditions";
                    this.Progress.IsIndeterminate = true;

                    try {
                        Task<bool> updateTask = this.UpdateConditionsAndCheckCanRun(this, token);
                        bool canRun = await updateTask;
                        if (!canRun) {
                            await Task.Delay(10, token);
                            continue;
                        }
                    }
                    catch (OperationCanceledException) {
                        return;
                    }
                    catch (Exception e) {
                        this.LastException = e;
                        return;
                    }

                    this.Progress.IsIndeterminate = false;

                    // Even though there's no valid operations to run, the user may want to just
                    // see a condition's output. We add a delay to save some CPU cycles
                    if (!operations.Any(x => x.IsEnabled && !(x is IPlaceholderOperation))) {
                        await Task.Delay(10, token);
                    }
                }
                else if (operations.Count < 1) {
                    // No reason to keep the task running; no operations to run and no conditions to update
                    this.RequestCancellation();
                    break;
                }

                if (operations.Count > 0) {
                    this.Progress.Text = "Running operation(s)";
                    for (int i = 0; i < operations.Count; i++) {
                        if (token.IsCancellationRequested) {
                            return;
                        }

                        BaseSequenceOperation operation = operations[i];
                        if (operation is IPlaceholderOperation) {
                            await Task.Yield();
                            continue;
                        }

                        try {
                            bool canRunOperation = true;
                            if (operation.Conditions.Count > 0) {
                                operation.State = OperationState.WaitingForConditions;
                                this.Progress.Text = "Waiting for operation conditions";
                                this.Progress.IsIndeterminate = true;
                                
                                try {
                                    do {
                                        if (await this.UpdateConditionsAndCheckCanRun(operation, token)) {
                                            break;
                                        }
                                
                                        switch (operation.ConditionBehaviour) {
                                            case OperationConditionBehaviour.Wait: await Task.Delay(10, token); break;
                                            case OperationConditionBehaviour.Skip: canRunOperation = false; break;
                                        }
                                    } while (canRunOperation /* always true until ConditionBehaviour becomes Skip */);
                                }
                                catch (OperationCanceledException) {
                                    return;
                                }
                                catch (Exception e) {
                                    this.LastException = e;
                                    return;
                                }

                                this.Progress.IsIndeterminate = false;
                            }

                            if (canRunOperation) {
                                operation.State = OperationState.Running;
                                this.Progress.Text = "Running operation(s)";
                                if (operation is JumpToLabelOperation jump) {
                                    if (jump.IsEnabled && jump.CurrentTarget != null && jump.CurrentTarget.TaskSequence == this /* should be impossible to be null */) {
                                        int idx = this.Operations.IndexOf(jump.CurrentTarget);
                                        Debug.Assert(idx != -1);

                                        i = idx - 1;
                                    }

                                    await Task.Yield();
                                }
                                else {
                                    try {
                                        await operation.Run(this.myContext, token);
                                    }
                                    catch (OperationCanceledException) {
                                        return;
                                    }
                                    catch (Exception e) {
                                        this.LastException = e;
                                        return;
                                    }
                                }
                            }
                        }
                        finally {
                            operation.State = OperationState.NotRunning;
                        }
                    }
                }

                // Do not decrease when runCount is negative, since it may underflow to positive MaxValue
                if (remainingRunCount > 0)
                    --remainingRunCount;
            }
        }, CancellationToken.None);

        try {
            await task;
        }
        catch (OperationCanceledException) {
            // ignored
        }
        catch (Exception e) {
            // An unexpected exception, maybe property change event handler threw
            Debugger.Break();
            this.LastException = e;
        }

        foreach (BaseSequenceOperation operation in operations) {
            operation.OnSequenceStopped();
            foreach (BaseSequenceCondition condition in operation.Conditions)
                condition.OnSequenceStopped();
        }

        foreach (BaseSequenceCondition condition in this.Conditions)
            condition.OnSequenceStopped();

        this.Progress.Text = "Sequence finished";
        this.myCts = null;
        this.myContext = null;
        TaskSequenceManager.InternalSetIsRunning(this.myManager!, this, false);
        this.IsRunning = false;
        this.myTcs.TrySetResult();
        this.myTcs = null;
    }

    private async Task<bool> UpdateConditionsAndCheckCanRun(IConditionsHost conditionsHost, CancellationToken cancellationToken) {
        CachedConditionData cache = new CachedConditionData();
        bool isConditionMet = true;
        foreach (BaseSequenceCondition condition in conditionsHost.Conditions) {
            await condition.UpdateCondition(this.myContext!, cache, cancellationToken);

            // maintain met state when disabled
            isConditionMet &= !condition.IsEnabled || condition.IsCurrentlyMet;
        }

        return isConditionMet;
    }

    public Task WaitForCompletion() {
        return this.myTcs?.Task ?? Task.CompletedTask;
    }

    /// <summary>
    /// Requests this sequence stop as soon as possible
    /// </summary>
    /// <returns>True when signalled to stop. False when not running or in the final process of stopping</returns>
    public bool RequestCancellation() {
        if (this.myCts == null)
            return false;
        this.myCts.Cancel();
        return true;
    }

    public void CheckNotRunning(string message) {
        if (this.IsRunning)
            throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Updates the target label for all jump operations. Used when a label's name changes or when a task sequence is deserialized
    /// </summary>
    public void UpdateAllJumpTargets() {
        Dictionary<string, LabelOperation> labels = this.Operations.OfType<LabelOperation>().Where(x => x.LabelName != null).ToDictionary(x => x.LabelName!);

        foreach (JumpToLabelOperation op in this.Operations.OfType<JumpToLabelOperation>()) {
            if (op.TargetLabel == null) {
                if (op.CurrentTarget != null)
                    op.SetTarget(null, null);
            }
            else {
                op.SetTarget(op.TargetLabel, labels.GetValueOrDefault(op.TargetLabel));
            }
        }
    }
}