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
using PFXToolKitUI;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Sequencing;

public delegate void TaskSequenceEventHandler(TaskSequence sender);

public delegate void TaskSequenceDedicatedConnectionChangedEventHandler(TaskSequence sender, IConsoleConnection? oldDedicatedConnection, IConsoleConnection? newDedicatedConnection);

/// <summary>
/// A sequence that contains a list of operations
/// </summary>
public sealed class TaskSequence {
    internal TaskSequencerManager? myManager;
    private string displayName = "Empty Sequence";
    private int runCount = 1;
    private bool hasBusyLockPriority;
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

    public bool HasBusyLockPriority {
        get => this.hasBusyLockPriority;
        set {
            ApplicationPFX.Instance.Dispatcher.VerifyAccess();
            this.CheckNotRunning($"Cannot change {nameof(this.HasBusyLockPriority)} while running");
            PropertyHelper.SetAndRaiseINE(ref this.hasBusyLockPriority, value, this, static t => t.HasBusyLockPriorityChanged?.Invoke(t));
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
    /// Gets the <see cref="TaskSequencerManager"/> that this sequence exists in
    /// </summary>
    public TaskSequencerManager? Manager => this.myManager;

    /// <summary>
    /// Gets the exception encountered while executing an operation in this sequence. This is set before <see cref="IsRunning"/> is changed
    /// </summary>
    public Exception? LastException { get; private set; }

    /// <summary>
    /// Gets our operations
    /// </summary>
    public ObservableList<BaseSequenceOperation> Operations { get; }

    /// <summary>
    /// Gets the list of conditions that must be met for this sequence to run.
    /// </summary>
    public ObservableList<BaseSequenceCondition> Conditions { get; }

    public IActivityProgress Progress { get; }

    /// <summary>
    /// An event fired when our running state changes. When this fires, the first operation will not have run yet.
    /// </summary>
    public event TaskSequenceEventHandler? IsRunningChanged;

    public event TaskSequenceEventHandler? DisplayNameChanged;
    public event TaskSequenceEventHandler? RunCountChanged;
    public event TaskSequenceEventHandler? HasBusyLockPriorityChanged;
    public event TaskSequenceEventHandler? UseEngineConnectionChanged;

    /// <summary>
    /// Raised when <see cref="DedicatedConnection"/> changes. When <see cref="UseEngineConnection"/> is being set to true,
    /// the dedicated connection is closed and set to null, in which case, this event fires BEFORE <see cref="UseEngineConnection"/> is set to true
    /// </summary>
    public event TaskSequenceDedicatedConnectionChangedEventHandler? DedicatedConnectionChanged;

    public TaskSequence() {
        this.Operations = new ObservableList<BaseSequenceOperation>();
        this.Operations.BeforeItemAdded += (list, index, item) => {
            if (item == null)
                throw new ArgumentNullException(nameof(item), "Cannot add a null operation");
            if (item.Sequence == this)
                throw new InvalidOperationException("Operation already exists in this operation. It must be removed first");
            if (item.Sequence != null)
                throw new InvalidOperationException("Operation already exists in another container. It must be removed first");
            this.CheckNotRunning("Cannot modify sequence list while running");
        };

        this.Operations.BeforeItemsRemoved += (list, index, count) => this.CheckNotRunning("Cannot modify sequence list while running");
        this.Operations.BeforeItemMoved += (list, oldIdx, newIdx, item) => this.CheckNotRunning("Cannot modify sequence list while running");
        this.Operations.BeforeItemReplace += (list, index, oldItem, newItem) => {
            if (newItem == null)
                throw new ArgumentNullException(nameof(newItem), "Cannot replace operation with null");
            this.CheckNotRunning("Cannot modify sequence list while running");
        };

        this.Operations.ItemsAdded += (list, index, items) => items.ForEach(this, BaseSequenceOperation.InternalSetSequence);
        this.Operations.ItemsRemoved += (list, index, items) => items.ForEach((TaskSequence?) null, BaseSequenceOperation.InternalSetSequence);
        this.Operations.ItemReplaced += (list, index, oldItem, newItem) => {
            BaseSequenceOperation.InternalSetSequence(oldItem, null);
            BaseSequenceOperation.InternalSetSequence(newItem, this);
        };

        this.Conditions = new ObservableList<BaseSequenceCondition>();
        this.Conditions.BeforeItemAdded += (list, i, item) => {
            this.CheckNotRunning("Cannot add conditions while running");
            if (item.TaskSequence == this)
                throw new InvalidOperationException("Condition already added to this sequence");
            if (item.TaskSequence != null)
                throw new InvalidOperationException("Condition already exists in another sequence");
        };

        this.Conditions.BeforeItemsRemoved += (list, index, count) => this.CheckNotRunning($"Cannot remove condition{Lang.S(count)} while running");
        this.Conditions.BeforeItemMoved += (list, oldIdx, newIdx, item) => this.CheckNotRunning("Cannot move conditions while running");
        this.Conditions.BeforeItemReplace += (list, index, oldItem, newItem) => {
            if (newItem == null)
                throw new ArgumentNullException(nameof(newItem), "Cannot replace condition with null");
            this.CheckNotRunning("Cannot replace condition while running");
        };

        this.Conditions.ItemsAdded += (list, index, items) => items.ForEach(this, BaseSequenceCondition.InternalSetSequence);
        this.Conditions.ItemsRemoved += (list, index, items) => items.ForEach((TaskSequence?) null, BaseSequenceCondition.InternalSetSequence);
        this.Conditions.ItemReplaced += (list, index, oldItem, newItem) => {
            BaseSequenceCondition.InternalSetSequence(oldItem, null);
            BaseSequenceCondition.InternalSetSequence(newItem, this);
        };

        this.Progress = new ConcurrentActivityProgress();
        this.Progress.Text = "Sequence not running";
    }

    /// <summary>
    /// Creates a clone of this sequence as if the user had created and configured one to match the current instance
    /// </summary>
    /// <returns></returns>
    public TaskSequence CreateClone() {
        TaskSequence sequence = new TaskSequence() {
            DisplayName = this.displayName,
            RunCount = this.runCount,
            HasBusyLockPriority = this.hasBusyLockPriority
        };

        foreach (BaseSequenceCondition condition in this.Conditions)
            sequence.Conditions.Add(condition.CreateClone());

        foreach (BaseSequenceOperation operation in this.Operations)
            sequence.Operations.Add(operation.CreateClone());

        return sequence;
    }

    public async Task Run(IConsoleConnection connection, IDisposable? busyToken, bool isConnectionDedicated) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        this.CheckNotRunning("Cannot run while already running");
        if (this.myManager == null)
            throw new InvalidOperationException("Cannot run standalone without a " + nameof(TaskSequencerManager));

        Debug.Assert(this.myCts == null && this.myTcs == null);
        Debug.Assert(this.myContext == null);
        Debug.Assert(isConnectionDedicated == !this.UseEngineConnection);
        using CancellationTokenSource cts = this.myCts = new CancellationTokenSource();
        this.myTcs = new TaskCompletionSource();
        this.myContext = new SequenceExecutionContext(this, connection, busyToken, isConnectionDedicated);
        this.LastException = null;
        TaskSequencerManager.InternalSetIsRunning(this.myManager!, this, true);
        this.IsRunning = true;
        this.Progress.Caption = this.DisplayName;
        this.Progress.Text = "Running sequence";
        this.Progress.IsIndeterminate = true;

        List<BaseSequenceOperation> operations = this.Operations.ToList();
        List<BaseSequenceCondition> conditions = this.Conditions.ToList();
        foreach (BaseSequenceOperation operation in operations)
            operation.OnSequenceStarted();
        foreach (BaseSequenceCondition condition in conditions)
            condition.OnSequenceStarted();

        CancellationToken token = this.myCts.Token;
        Task task = Task.Run(async () => {
            int remainingRunCount = this.runCount;
            while ((remainingRunCount < 0 || remainingRunCount != 0) && !token.IsCancellationRequested) {
                if (conditions.Count > 0) {
                    this.Progress.Text = "Checking conditions...";
                    this.Progress.IsIndeterminate = true;

                    try {
                        Task<bool> updateTask = this.UpdateConditionsAndCheckCanRun(conditions, token);
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

                    // Even though there's no operations to run, the user may want to just
                    // see a condition's output. We add a delay to save some CPU cycles
                    if (!operations.Any(x => x.IsEnabled)) {
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
                    foreach (BaseSequenceOperation operation in operations) {
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

        foreach (BaseSequenceOperation operation in operations)
            operation.OnSequenceStopped();
        foreach (BaseSequenceCondition condition in conditions)
            condition.OnSequenceStopped();

        this.Progress.Text = "Sequence finished";
        this.myCts = null;
        this.myContext = null;
        TaskSequencerManager.InternalSetIsRunning(this.myManager!, this, false);
        this.IsRunning = false;
        this.myTcs.TrySetResult();
        this.myTcs = null;
    }

    private async Task<bool> UpdateConditionsAndCheckCanRun(List<BaseSequenceCondition> conditions, CancellationToken cancellationToken) {
        CachedConditionData cache = new CachedConditionData();
        bool isConditionMet = true;
        foreach (BaseSequenceCondition condition in conditions) {
            await condition.UpdateCondition(this.myContext!, cache, cancellationToken);
            isConditionMet &= condition.IsCurrentlyMet;
        }

        return isConditionMet;
    }

    public async Task WaitForCompletion() {
        await (this.myTcs?.Task ?? Task.CompletedTask).ConfigureAwait(false);
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

    public int IndexOf(BaseSequenceOperation entry) {
        if (!ReferenceEquals(entry.Sequence, this))
            return -1;
        int idx = this.Operations.IndexOf(entry);
        Debug.Assert(idx != -1);
        return idx;
    }

    public bool Contains(BaseSequenceOperation entry) => this.IndexOf(entry) != -1;
}