// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
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
    private readonly ObservableList<BaseSequenceOperation> operations;
    private readonly LinkedList<TaskCompletionSource> completionNotifications = [];
    internal TaskSequencerManager? myManager;
    private string displayName = "Empty Sequence";
    private int runCount = 1;
    private bool hasBusyLockPriority;
    private bool useEngineConnection = true;
    private IConsoleConnection? dedicatedConnection;

    // Running info
    private bool isRunning;
    private CancellationTokenSource? myCts;
    private SequenceExecutionContext? myContext;

    /// <summary>
    /// Gets our operations
    /// </summary>
    public ReadOnlyObservableList<BaseSequenceOperation> Operations { get; }

    /// <summary>
    /// Gets whether this sequence is currently running. This only changes on the main thread
    /// </summary>
    public bool IsRunning {
        get => this.isRunning;
        private set {
            if (this.isRunning == value)
                return;

            this.isRunning = value;
            this.IsRunningChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets the exception encountered while executing an operation in this sequence. This is set before <see cref="IsRunning"/> is changed
    /// </summary>
    public Exception? LastException { get; private set; }

    /// <summary>
    /// Gets the <see cref="TaskSequencerManager"/> that this sequence exists in
    /// </summary>
    public TaskSequencerManager? Manager => this.myManager;

    public string DisplayName {
        get => this.displayName;
        set {
            if (this.displayName == value)
                return;

            this.displayName = value;
            this.DisplayNameChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets or sets the run count. Setting to a negative value (ideally -1) means infinite runtime until cancelled
    /// </summary>
    public int RunCount {
        get => this.runCount;
        set {
            ApplicationPFX.Instance.Dispatcher.VerifyAccess();
            if (this.isRunning)
                throw new InvalidOperationException($"Cannot change {nameof(this.RunCount)} while running");

            if (this.runCount == value)
                return;

            this.runCount = value;
            this.RunCountChanged?.Invoke(this);
        }
    }

    public bool HasBusyLockPriority {
        get => this.hasBusyLockPriority;
        set {
            ApplicationPFX.Instance.Dispatcher.VerifyAccess();
            if (this.isRunning)
                throw new InvalidOperationException($"Cannot change {nameof(this.HasBusyLockPriority)} while running");

            if (this.hasBusyLockPriority == value)
                return;

            this.hasBusyLockPriority = value;
            this.HasBusyLockPriorityChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets or sets if we should use the engine connection or our own dedicated connection
    /// </summary>
    public bool UseEngineConnection {
        get => this.useEngineConnection;
        set {
            ApplicationPFX.Instance.Dispatcher.VerifyAccess();
            if (this.isRunning)
                throw new InvalidOperationException($"Cannot change {nameof(this.UseEngineConnection)} while running");

            if (this.useEngineConnection != value) {
                this.useEngineConnection = value;
                this.UseEngineConnectionChanged?.Invoke(this);
            }
        }
    }

    public IConsoleConnection? DedicatedConnection {
        get => this.dedicatedConnection;
        set {
            ApplicationPFX.Instance.Dispatcher.VerifyAccess();
            if (this.isRunning)
                throw new InvalidOperationException("Cannot change dedicated connection while running");

            IConsoleConnection? oldDedicatedConnection = this.dedicatedConnection;
            if (oldDedicatedConnection != value) {
                this.dedicatedConnection = value;
                this.DedicatedConnectionChanged?.Invoke(this, oldDedicatedConnection, value);
            }
        }
    }

    /// <summary>
    /// Raised when <see cref="DedicatedConnection"/> changes. When <see cref="UseEngineConnection"/> is being set to true,
    /// the dedicated connection is closed and set to null, in which case, this event fires BEFORE <see cref="UseEngineConnection"/> is set to true
    /// </summary>
    public event TaskSequenceDedicatedConnectionChangedEventHandler? DedicatedConnectionChanged;

    public IActivityProgress Progress { get; }

    /// <summary>
    /// An event fired when our running state changes. When this fires, the first operation will not have run yet.
    /// </summary>
    public event TaskSequenceEventHandler? IsRunningChanged;

    public event TaskSequenceEventHandler? DisplayNameChanged;
    public event TaskSequenceEventHandler? RunCountChanged;
    public event TaskSequenceEventHandler? HasBusyLockPriorityChanged;
    public event TaskSequenceEventHandler? UseEngineConnectionChanged;

    public TaskSequence() {
        this.operations = new ObservableList<BaseSequenceOperation>();
        this.Operations = new ReadOnlyObservableList<BaseSequenceOperation>(this.operations);
        this.Progress = new DefaultProgressTracker();
        this.Progress.Text = "Sequence not running";
    }

    public async Task Run(IConsoleConnection connection, IDisposable? busyToken, bool isConnectionDedicated) {
        this.CheckNotRunning("Cannot run while already running");
        if (this.myManager == null)
            throw new InvalidOperationException("Cannot run standalone without a " + nameof(TaskSequencerManager));

        Debug.Assert(this.myCts == null);
        Debug.Assert(this.myContext == null);
        Debug.Assert(isConnectionDedicated == !this.UseEngineConnection);
        using CancellationTokenSource cts = this.myCts = new CancellationTokenSource();
        this.myContext = new SequenceExecutionContext(this, this.Progress, connection, busyToken, isConnectionDedicated);
        this.LastException = null;
        TaskSequencerManager.InternalSetIsRunning(this.myManager!, this, true);
        this.IsRunning = true;
        this.Progress.Caption = this.DisplayName;
        this.Progress.Text = "Running sequence";

        CancellationToken token = this.myCts.Token;
        await ActivityManager.Instance.RunTask(async () => {
            for (int count = this.runCount; (count < 0 || count != 0) && !token.IsCancellationRequested; --count) {
                foreach (BaseSequenceOperation operation in this.operations) {
                    if (operation.IsEnabled) {
                        try {
                            await operation.Run(this.myContext, token);
                        }
                        catch (OperationCanceledException) {
                            break;
                        }
                        catch (Exception e) {
                            this.LastException = e;
                            break;
                        }
                    }
                }
            }
        }, this.Progress, this.myCts);

        this.Progress.Text = "Sequence finished";
        this.myCts = null;
        this.myContext = null;
        TaskSequencerManager.InternalSetIsRunning(this.myManager!, this, false);
        this.IsRunning = false;

        List<TaskCompletionSource> completions = CollectionUtils.AtomicGetAndClear(this.completionNotifications, this.completionNotifications);
        foreach (TaskCompletionSource tcs in completions) {
            tcs.TrySetResult();
        }
    }

    public async Task WaitForCompletion() {
        if (!this.isRunning) {
            return;
        }

        LinkedListNode<TaskCompletionSource> node;
        lock (this.completionNotifications) {
            if (!this.isRunning) {
                return;
            }

            node = this.completionNotifications.AddLast(new TaskCompletionSource());
        }

        await node.Value.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Requests this sequence stop as soon as possible
    /// </summary>
    /// <returns></returns>
    public bool RequestCancellation() {
        if (this.myCts == null) {
            return false;
        }

        try {
            this.myCts.Cancel();
        }
        catch (ObjectDisposedException) {
            // ignored
        }

        return true;
    }

    public void CheckNotRunning(string message) {
        if (this.isRunning)
            throw new InvalidOperationException(message);
    }

    public void AddOperation(BaseSequenceOperation entry) => this.InsertOperation(this.operations.Count, entry);

    public void InsertOperation(int index, BaseSequenceOperation entry) {
        this.CheckNotRunning("Cannot modify sequence list while running");
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Negative indices not allowed");
        if (index > this.operations.Count)
            throw new ArgumentOutOfRangeException(nameof(index), $"Index is beyond the range of this list: {index} > count({this.operations.Count})");

        if (entry == null)
            throw new ArgumentNullException(nameof(entry), "Cannot add a null entry");
        if (entry.Sequence == this)
            throw new InvalidOperationException("Entry already exists in this entry. It must be removed first");
        if (entry.Sequence != null)
            throw new InvalidOperationException("Entry already exists in another container. It must be removed first");

        BaseSequenceOperation.InternalSetSequence(entry, this);
        this.operations.Insert(index, entry);
    }

    public void AddOperations(IEnumerable<BaseSequenceOperation> layers) {
        this.CheckNotRunning("Cannot modify sequence list while running");
        foreach (BaseSequenceOperation entry in layers) {
            this.AddOperation(entry);
        }
    }

    public bool RemoveOperation(BaseSequenceOperation entry) {
        this.CheckNotRunning("Cannot modify sequence list while running");
        if (!ReferenceEquals(entry.Sequence, this)) {
            return false;
        }

        int idx = this.IndexOf(entry);
        Debug.Assert(idx != -1);
        this.RemoveOperationAt(idx);

        Debug.Assert(entry.Sequence != this, "Entry parent not updated, still ourself");
        Debug.Assert(entry.Sequence == null, "Entry parent not updated to null");
        return true;
    }

    public void RemoveOperationAt(int index) {
        this.CheckNotRunning("Cannot modify sequence list while running");
        BaseSequenceOperation entry = this.operations[index];
        try {
            this.operations.RemoveAt(index);
        }
        finally {
            // not that we really need try finally since exceptions would crash
            // the app... don't judge me I like safety during catastrophes
            BaseSequenceOperation.InternalSetSequence(entry, null);
        }
    }

    public void RemoveOperations(IEnumerable<BaseSequenceOperation> entries) {
        this.CheckNotRunning("Cannot modify sequence list while running");
        foreach (BaseSequenceOperation entry in entries) {
            this.RemoveOperation(entry);
        }
    }

    public void ClearOperations() {
        this.CheckNotRunning("Cannot modify sequence list while running");
        foreach (BaseSequenceOperation t in this.operations) {
            BaseSequenceOperation.InternalSetSequence(t, null);
        }

        this.operations.Clear();
    }

    public int IndexOf(BaseSequenceOperation entry) {
        return ReferenceEquals(entry.Sequence, this) ? this.operations.IndexOf(entry) : -1;
    }

    public bool Contains(BaseSequenceOperation entry) {
        return this.IndexOf(entry) != -1;
    }
}