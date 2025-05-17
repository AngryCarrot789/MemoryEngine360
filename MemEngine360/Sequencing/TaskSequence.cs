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
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Sequencing;

public delegate void TaskSequenceEventHandler(TaskSequence sender);

/// <summary>
/// A sequence that contains a list of operations
/// </summary>
public sealed class TaskSequence {
    private readonly ObservableList<BaseSequenceOperation> operations;
    private readonly LinkedList<TaskCompletionSource> completionNotifications = [];
    internal TaskSequencerManager? myManager;
    private string displayName = "Empty Sequence";
    private uint runCount = 1;
    private bool hasBusyLockPriority;

    // Running info
    private bool isRunning;
    private CancellationTokenSource? myCts;
    private SequenceExecutionContext? myContext;

    /// <summary>
    /// Gets our operations
    /// </summary>
    public ReadOnlyObservableList<BaseSequenceOperation> Operations { get; }

    /// <summary>
    /// Gets whether this sequence is currently running
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

    public uint RunCount {
        get => this.runCount;
        set {
            if (this.runCount == value)
                return;

            this.runCount = value;
            this.RunCountChanged?.Invoke(this);
        }
    }

    public bool HasBusyLockPriority {
        get => this.hasBusyLockPriority;
        set {
            if (this.hasBusyLockPriority == value)
                return;

            this.hasBusyLockPriority = value;
            this.HasBusyLockPriorityChanged?.Invoke(this);
        }
    }

    public IActivityProgress Progress { get; }

    /// <summary>
    /// An event fired when our running state changes. When this fires, the first operation will not have run yet.
    /// </summary>
    public event TaskSequenceEventHandler? IsRunningChanged;

    public event TaskSequenceEventHandler? DisplayNameChanged;
    public event TaskSequenceEventHandler? RunCountChanged;
    public event TaskSequenceEventHandler? HasBusyLockPriorityChanged;

    public TaskSequence() {
        this.operations = new ObservableList<BaseSequenceOperation>();
        this.Operations = new ReadOnlyObservableList<BaseSequenceOperation>(this.operations);
        this.Progress = new DefaultProgressTracker();
        this.Progress.Text = "Sequence not running";
    }

    public async Task Run(CancellationTokenSource cts, IConsoleConnection connection, IDisposable? busyToken) {
        this.CheckNotRunning("Cannot run while already running");
        if (this.myManager == null)
            throw new InvalidOperationException("Cannot run standalone without a " + nameof(TaskSequencerManager));

        Debug.Assert(this.myCts == null);
        Debug.Assert(this.myContext == null);
        this.myCts = cts;
        this.myContext = new SequenceExecutionContext(this, this.Progress, connection, busyToken);
        this.LastException = null;
        TaskSequencerManager.InternalSetIsRunning(this.myManager!, this, true);
        this.IsRunning = true;
        this.Progress.Text = "Running sequence";

        CancellationToken token = this.myCts.Token;
        for (uint count = this.runCount; count != 0 && !token.IsCancellationRequested; count--) {
            foreach (BaseSequenceOperation operation in this.operations) {
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

        this.Progress.Text = "Sequence finished";
        this.myCts = null;
        this.myContext = null;
        TaskSequencerManager.InternalSetIsRunning(this.myManager!, this, false);
        this.IsRunning = false;

        List<TaskCompletionSource> completions = this.completionNotifications.ToList();
        this.completionNotifications.Clear();
        foreach (TaskCompletionSource tcs in completions) {
            tcs.TrySetResult();
        }
    }

    public async Task WaitForCompletion() {
        if (!this.isRunning) {
            return;
        }

        LinkedListNode<TaskCompletionSource> node = this.completionNotifications.AddLast(new TaskCompletionSource());
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