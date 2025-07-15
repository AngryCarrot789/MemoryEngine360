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
using System.Diagnostics.CodeAnalysis;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Engine;

public delegate void RWBusyLockEventHandler(BusyLock busyLock);

/// <summary>
/// An asynchronous lock implementation, used primarily used by the <see cref="MemoryEngine"/>,
/// to synchronize access to a resource without blocking at all. The lock is used to ensure only one
/// reader and one writer can access an object (in the memory engine case, a console connection)
/// <para>
/// Provides mechanisms for taking a token asynchronously within a timeout limit, with a cancellation token, or both.
/// </para>
/// </summary>
public sealed class BusyLock {
    private volatile int busyCount; // this is a boolean. 0 = no token, 1 = token acquired. any other value is invalid
    private volatile BusyToken? activeToken;

    // A list of TCSes that are signal when the busy lock becomes available.
    // They are custom in that they also support a CancellationToken to signal them too
    private readonly LinkedList<CancellableTaskCompletionSource> busyLockAsyncWaiters;

    /// <summary>
    /// Gets the lock used to synchronize taking and returning the busy lock.
    /// This can be used externally to prevent threads taking the busy lock during a critical operation
    /// </summary>
    public object CriticalLock { get; } = new object();

    /// <summary>
    /// Returns true when there's a busy token in use
    /// </summary>
    public bool IsBusy => this.busyCount > 0;

    /// <summary>
    /// Fired when the <see cref="IsConnectionBusy"/> state changes. It is crucial that no 'busy' operations are performed
    /// in the event handlers, otherwise, a deadlock could occur.
    /// <para>
    /// It's also important that exceptions are not thrown in the handlers, because they will be swallowed and never see
    /// the light of day, and the next handlers in the list will not be invoked, potentially leading to application wide corruption
    /// </para>
    /// </summary>
    public event RWBusyLockEventHandler? IsBusyChanged;

    public BusyLock() {
        this.busyLockAsyncWaiters = new LinkedList<CancellableTaskCompletionSource>();
    }

    /// <summary>
    /// Tries to take the token, or returns null if already busy. Dispose the returned object to finish the busy operation.
    /// </summary>
    /// <returns>A token to dispose when the operation is completed. Returns null if currently busy</returns>
    public IDisposable? BeginBusyOperation() {
        bool lockTaken = false;
        object lockObj = this.CriticalLock;
        
        try {
            Monitor.TryEnter(lockObj, ref lockTaken);
            if (!lockTaken)
                return null;

            if (this.busyCount == 0)
                return this.activeToken = new BusyToken(this);

            return null;
        }
        finally {
            if (lockTaken)
                Monitor.Exit(lockObj);
        }
    }

    /// <summary>
    /// Begins a busy operation. If currently busy, we await for the current taker to finish before trying to take it again.
    /// <para>
    /// This method does not throw <see cref="OperationCanceledException"/> when the token is cancelled, instead, the method returns a null token
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">Used to cancel the operation, causing the task to return a null busy token</param>
    /// <returns>The acquired token, or null if the task was cancelled</returns>
    public async Task<IDisposable?> BeginBusyOperationAsync(CancellationToken cancellationToken) {
        IDisposable? token = this.BeginBusyOperation();
        if (token != null) return token;

        return await this.InternalBeginOperationLoop(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Begins a busy operation, waiting for existing busy operations to finish or the timeout period elapsed, in which case this method returns null.
    /// <para>
    /// This method does not throw <see cref="OperationCanceledException"/> when the token is cancelled or the timeout ellapses, instead, the method returns a null token
    /// </para>
    /// </summary>
    /// <param name="timeoutMilliseconds">The maximum amount of time to wait to try and begin the operations. -1 means we wait forever</param>
    /// <param name="cancellationToken">Used to cancel the operation, causing the task to return a null busy token</param>
    /// <returns></returns>
    public async Task<IDisposable?> BeginBusyOperationAsync(int timeoutMilliseconds, CancellationToken cancellationToken = default) {
        if (timeoutMilliseconds < -1) 
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), "Timeout milliseconds cannot be below -1");

        IDisposable? token = this.BeginBusyOperation();
        if (token != null) return token;

        if (timeoutMilliseconds == -1) // we'll wait infinitely.
            return await this.InternalBeginOperationLoop(cancellationToken).ConfigureAwait(false);

        if (timeoutMilliseconds == 0)
            return null; // well WTF, I guess this is the right thing to do???

        // Probably no need to go whacko mode and overoptimize... but meh I already wrote this code so no point in undoing it
        CancellationTokenSource? cts, otherCts = null;
        if (cancellationToken.CanBeCanceled) {
            otherCts = new CancellationTokenSource(timeoutMilliseconds);
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, otherCts.Token);
        }
        else {
            cts = new CancellationTokenSource(timeoutMilliseconds);
        }

        try {
            token = await this.InternalBeginOperationLoop(cts.Token).ConfigureAwait(false);
        }
        finally {
            // should this be disposed before or after cts?
            otherCts?.Dispose();
            cts.Dispose();
        }

        return token;
    }

    /// <summary>
    /// Tries to begin a busy operation. If we could not get the token immediately, we start
    /// a new activity and try to get it asynchronously, passing the given progress to the activity
    /// <para>
    /// This method does not throw <see cref="OperationCanceledException"/> when cancelled, instead, the method returns a null token
    /// </para>
    /// </summary>
    /// <returns>
    /// A task with the token, or null if the user cancelled the operation or some other weird error occurred
    /// </returns>
    public async Task<IDisposable?> BeginBusyOperationActivityAsync(string caption = "New Operation", string message = "Waiting for busy operations...", CancellationTokenSource? cancellationTokenSource = null) {
        IDisposable? token = this.BeginBusyOperation();
        if (token == null) {
            return await this.BeginBusyOperationActivityAsync(new DefaultProgressTracker() { Caption = caption, Text = message }, cancellationTokenSource).ConfigureAwait(false);
        }

        return token;
    }
    /// <summary>
    /// Tries to begin a busy operation. If we could not get the token immediately, we start
    /// a new activity and try to get it asynchronously, passing the given progress to the activity
    /// <para>
    /// This method does not throw <see cref="OperationCanceledException"/> when cancelled, instead, the method returns a null token
    /// </para>
    /// </summary>
    /// <returns>
    /// A task with the token, or null if the user cancelled the operation or some other weird error occurred
    /// </returns>
    public async Task<IDisposable?> BeginBusyOperationActivityAsync(IActivityProgress progress, CancellationTokenSource? cancellationTokenSource = null) {
        IDisposable? token = this.BeginBusyOperation();
        if (token == null) {
            CancellationTokenSource cts = cancellationTokenSource ?? new CancellationTokenSource();
            token = await ActivityManager.Instance.RunTask(() => {
                ActivityTask task = ActivityManager.Instance.CurrentTask;
                return this.BeginBusyOperationAsync(task.CancellationToken);
            }, progress, cts);

            if (cancellationTokenSource == null) {
                cts.Dispose();
            }
        }

        return token;
    }

    private async Task<IDisposable?> InternalBeginOperationLoop(CancellationToken cancellationToken) {
        IDisposable? token;
        int waitState = 0;

        do {
            LinkedListNode<CancellableTaskCompletionSource>? tcs = this.EnqueueAsyncWaiter(cancellationToken);
            if (tcs == null) {
                if (this.busyCount != 0) {
                    // Either CriticalLock is acquired on another thread and the busy token is still taken,
                    // or we lost the race condition between busyCount being 0 in EnqueueAsyncWaiter.
                    // So the only thing we can do is just wait some time. Yield first, since the
                    // lock will most likely be released once the continuation is executed, unless
                    // an external user is using CriticalLock
                    if (waitState == 0) {
                        waitState = 1;
                        await Task.Yield();
                    }
                    else {
                        try {
                            switch (waitState) {
                                case 1:
                                    waitState = 2;
                                    await Task.Delay(1, cancellationToken);
                                    break;
                                case 2:
                                    await Task.Delay(10, cancellationToken);
                                    break;
                            }   
                        }
                        catch (OperationCanceledException) {
                            return null;
                        }  
                    }
                }
            }
            else {
                try {
                    await tcs.Value.Task;
                }
                catch (OperationCanceledException) {
                    if (tcs.List != null) {
                        lock (this.CriticalLock) {
                            // Possible race condition between OCE handled on one thread, and the busy token disposed on another thread.
                            // It's fine if we win the race since we'd remove the node before OnTokenDisposedUnderLock() clears
                            // the list. But if that method wins, the list gets cleared under lock and tcs.List is null below
                            if (tcs.List != null) {
                                // We only need to remove on cancelled, because when it
                                // completes normally, the list gets cleared anyway
                                tcs.List!.Remove(tcs);
                                tcs.Value.Dispose();
                            }
                        }
                    }

                    return null;
                }
            }
            // stoopid IDE, using the async overload could cause stack overflow if we get really unlucky in lock taking
            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
        } while ((token = this.BeginBusyOperation()) == null);

        return token;
    }

    /// <summary>
    /// Ensures the token is a valid busy token and is our currently taken token (and is not someone elses)
    /// </summary>
    /// <param name="token">The token to validate</param>
    /// <exception cref="ArgumentNullException">Token is null</exception>
    /// <exception cref="ArgumentException">Object is not a token or is disposed, not associated with this lock or not the taken token</exception>
    public void ValidateToken(IDisposable token) {
        if (token == null) 
            throw new ArgumentNullException(nameof(token), "Token is null");
        if (!(token is BusyToken busy)) 
            throw new ArgumentException("Argument is not a busy token object");

        // myLock can be atomically exchanged
        BusyLock? theLock = busy.myLock;
        if (theLock == null)
            throw new ArgumentException("Token has already been disposed");
        if (theLock != this)
            throw new ArgumentException("Token is not associated with this lock");
        if (this.activeToken != busy)
            throw new ArgumentException(this.busyCount == 0 ? "No tokens are currently in use" : "Token is not the current token");
    }

    /// <summary>
    /// Same as <see cref="ValidateToken"/> except does not throw, but instead returns a boolean
    /// </summary>
    /// <param name="token">The token to validate</param>
    /// <returns>True when valid for use, otherwise false</returns>
    public bool IsTokenValid([NotNullWhen(true)] IDisposable? token) {
        if (!(token is BusyToken busy)) return false;

        // myLock can be atomically exchanged
        BusyLock? theLock = busy.myLock;
        return theLock == this && this.activeToken == busy;
    }

    private LinkedListNode<CancellableTaskCompletionSource>? EnqueueAsyncWaiter(CancellationToken token) {
        bool lockTaken = false;
        object lockObj = this.CriticalLock;
        
        try {
            Monitor.TryEnter(lockObj, ref lockTaken);
            if (!lockTaken || this.busyCount == 0) {
                // When busyCount is 0 at this point, it means we probably lost the lock race.
                // The caller will notice null and check busyCount anyway so it's fine.
                // The last thing we want is to return a valid TCS and busyCount is 0, because
                // it will never become completed until another token is acquired and disposed
                return null;
            }

            return this.busyLockAsyncWaiters.AddLast(new CancellableTaskCompletionSource(token));
        }
        finally {
            if (lockTaken)
                Monitor.Exit(lockObj);
        }
    }

    private void OnTokenCreatedUnderLock() {
        if (Interlocked.Increment(ref this.busyCount) == 1) {
            try {
                this.IsBusyChanged?.Invoke(this);
            }
            catch {
                Debugger.Break(); // exceptions are swallowed because it's the user's fault for not catching errors :D
            }
        }
    }

    private void OnTokenDisposedUnderLock() {
        this.activeToken = null;
        if (Interlocked.Decrement(ref this.busyCount) == 0) {
            try {
                this.IsBusyChanged?.Invoke(this);
            }
            catch {
                Debugger.Break(); // exceptions are swallowed because it's the user's fault for not catching errors :D
            }
        }

        foreach (CancellableTaskCompletionSource tcs in this.busyLockAsyncWaiters) {
            try {
                tcs.TrySetResult();
            }
            finally {
                tcs.Dispose();
            }
        }

        this.busyLockAsyncWaiters.Clear();
    }

    // A helper class deriving TCS that allows an external cancellation signal to mark it as completed
    private sealed class CancellableTaskCompletionSource : TaskCompletionSource, IDisposable {
        private readonly CancellationToken token;
        private readonly CancellationTokenRegistration registration;

        public CancellableTaskCompletionSource(CancellationToken token) : base(TaskCreationOptions.RunContinuationsAsynchronously) {
            this.token = token;
            if (token.CanBeCanceled)
                this.registration = token.Register(this.SetCanceledCore);
        }

        private void SetCanceledCore() {
            this.TrySetCanceled(this.token);
        }

        public void Dispose() {
            Debug.Assert(this.Task.IsCompleted, "Expected task to be completed at this point");

            this.registration.Dispose();
        }
    }

    private class BusyToken : IDisposable {
        public volatile BusyLock? myLock;
#if DEBUG
        // debugging stack trace, just in case the app locks up then the source is likely in here
        public readonly string? creationTrace;
        public string? disposalTrace;
#endif

        public BusyToken(BusyLock theLock) {
            Debug.Assert(theLock.activeToken == null, "Active token already non-null");
            this.myLock = theLock;
            theLock.OnTokenCreatedUnderLock();
#if DEBUG
            this.creationTrace = new StackTrace(true).ToString();
#endif
        }

        public void Dispose() {
            // we're being omega thread safe here
            BusyLock? theLock = Interlocked.Exchange(ref this.myLock, null);
            if (theLock == null) {
                return; // already disposed...
            }

#if DEBUG
            string? oldDisposalTrace = this.disposalTrace;
            this.disposalTrace = new StackTrace(true).ToString();
#endif
            
            lock (theLock.CriticalLock) {
                Debug.Assert(theLock.activeToken == this, "Different active token references");
                theLock.OnTokenDisposedUnderLock();
            }
        }
    }
}