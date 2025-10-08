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

#if DEBUG
#define TRACK_TOKEN_CREATION_STACK_TRACE
#endif

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine;

public delegate void BusyLockEventHandler(BusyLock busyLock);

public delegate void BusyLockQuickReleaseRequestedEventHandler(BusyLock busyLock, TaskCompletionSource tcsQuickActionFinished);

/// <summary>
/// An asynchronous lock implementation, used primarily used by the <see cref="MemoryEngine"/>,
/// to synchronize access to a resource without blocking at all. The lock is used to ensure only one
/// reader and one writer can access an object (in the memory engine case, a console connection)
/// <para>
/// Provides mechanisms for taking a token asynchronously within a timeout limit, with a cancellation token, or both.
/// </para>
/// </summary>
public sealed class BusyLock {
    public const string WaitingMessage = "Waiting for busy operations...";
    public const int DefaultForegroundDelay = 500;

    private volatile int busyCount; // this is a boolean. 0 = no token, 1 = token acquired. any other value is invalid
    private volatile BusyToken? activeToken;
    private volatile TaskCompletionSource? tcsBusyLockUsage;

    // Note: this will never produce a Cancelled task, only an incomplete/completed
    // Also, this does not represent the entire action, only the token acquisition stage.
    // It is marked as completed once the token was obtained or acquisition was cancelled
    private volatile CancellableTaskCompletionSource? tcsPrimaryQuickReleaseAction;

    /// <summary>
    /// Gets the lock used to synchronize taking and returning the busy lock.
    /// This can be used externally to prevent threads taking the busy lock during a critical operation
    /// </summary>
    public Lock CriticalLock { get; } = new Lock();

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
    /// <para>
    /// It is also vital that no handler ever tries to obtain the busy token in the same call frame.
    /// </para>
    /// </summary>
    public event BusyLockEventHandler? IsBusyChanged;

    /// <summary>
    /// Fired when a short running operation is trying to obtain the busy token, and the operation is substantial enough
    /// such that any listener should give up their ownership of the busy token to let the user action run, before trying
    /// to re-obtain the token. 
    /// <para>
    /// Example operations that might cause this event are editing 
    /// </para>
    /// </summary>
    /// <remarks>
    /// During the call frame for handlers, a busy token will remain unobtainable, because the caller will
    /// have already tried to obtain it and will be the first priority, no matter what
    /// </remarks>
    public event BusyLockQuickReleaseRequestedEventHandler? UserQuickReleaseRequested;

    public BusyLock() {
    }

    /// <summary>
    /// Tries to take the token, or returns null if already busy. Dispose the returned object to finish the busy operation.
    /// </summary>
    /// <returns>A token to dispose when the operation is completed. Returns null if currently busy</returns>
    public IBusyToken? TryBeginBusyOperation() {
        return this.InternalTryBeginBusyOperation(null);
    }

    /// <summary>
    /// Begins a busy operation. If currently busy, we await for the current taker to finish before trying to take it again.
    /// <para>
    /// This method does not throw <see cref="OperationCanceledException"/> when the token is cancelled, instead, the method returns a null token
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">A cancellation token used to stop trying to acquire the busy token</param>
    /// <returns>The acquired token, or null if the cancellation token became cancelled before the token could be acquired</returns>
    /// <remarks>This method does not throw <see cref="OperationCanceledException"/></remarks>
    public Task<IBusyToken?> BeginBusyOperation(CancellationToken cancellationToken) {
        return this.BeginBusyOperation(new BusyTokenRequest() { CancellationToken = cancellationToken, QuickReleaseIntention = false });
    }

    /// <summary>
    /// Begins a busy operation, waiting for existing busy operations to finish or the timeout
    /// period elapsed, or the cancellation token becomes cancelled, in which case this method returns null.
    /// <para>
    /// This method does not throw <see cref="OperationCanceledException"/> when the token is cancelled or the
    /// timeout elapsed, instead, the method returns a null token
    /// </para>
    /// </summary>
    /// <param name="timeoutMilliseconds">The maximum amount of time to wait to try and begin the operations. -1 means we wait forever</param>
    /// <param name="cancellationToken">Used to cancel the operation, causing the task to return a null busy token</param>
    /// <returns>The token, or null, if the timeout elapsed or the cancellation token becomes cancelled</returns>
    public Task<IBusyToken?> BeginBusyOperation(int timeoutMilliseconds, CancellationToken cancellationToken = default) {
        return this.BeginBusyOperation(new BusyTokenRequest() { CancellationToken = cancellationToken, TimeoutMilliseconds = timeoutMilliseconds });
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
    public Task<IBusyToken?> BeginBusyOperationUsingActivity(string caption = "New Operation", string message = WaitingMessage, CancellationToken cancellationToken = default) {
        return this.BeginBusyOperationUsingActivity(new BusyTokenRequestUsingActivity() {
            CancellationToken = cancellationToken,
            Progress = { Caption = caption, Text = message }
        });
    }

    /// <summary>
    /// Begins acquiring the busy token from within an already running activity
    /// </summary>
    /// <param name="busyCancellation">A cancellation token used to stop trying to acquire the busy token</param>
    /// <returns>The acquired token, or null if the cancellation token became cancelled before the token could be acquired, or the activity became cancelled</returns>
    public Task<IBusyToken?> BeginBusyOperationFromActivity(CancellationToken busyCancellation = default) {
        return this.BeginBusyOperationFromActivity(new BusyTokenRequestFromActivity() {
            BusyCancellation = busyCancellation
        });
    }

    /// <summary>
    /// Tries to obtain the busy token
    /// </summary>
    /// <param name="request">The busy token request information</param>
    /// <returns>The token, or null, if the request was cancelled</returns>
    /// <remarks>
    /// The returned token object must be disposed once your operation has completed,
    /// so that other operations can do busy operations
    /// </remarks>
    public Task<IBusyToken?> BeginBusyOperation(BusyTokenRequest request) {
        IBusyToken? token = this.TryBeginBusyOperation();
        if (token != null) {
            return Task.FromResult<IBusyToken?>(token);
        }

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken);
        if (request.TimeoutMilliseconds > 0) {
            cts.CancelAfter(request.TimeoutMilliseconds);
        }

        return request.QuickReleaseIntention
            ? this.InternalQuickReleaseLoop(cts.Token)
            : this.InternalBeginBusyOperationLoop(cts.Token);
    }

    /// <summary>
    /// Tries to obtain the busy token using a new activity
    /// </summary>
    /// <param name="request">The busy token request information</param>
    /// <returns>The token, or null, if the request was cancelled</returns>
    /// <remarks>
    /// The returned token object must be disposed once your operation has completed,
    /// so that other operations can do busy operations
    /// </remarks>
    public async Task<IBusyToken?> BeginBusyOperationUsingActivity(BusyTokenRequestUsingActivity request) {
        if (request.Progress == null)
            throw new ArgumentException($"Request contained a null {nameof(IActivityProgress)}");

        InForegroundInfo? foregroundInfo = request.ForegroundInfo;
        if (foregroundInfo.HasValue && foregroundInfo.Value.TopLevel == null) {
            throw new ArgumentException($"Request's InForeground info contained a null {nameof(ITopLevel)}");
        }

        IBusyToken? token = this.TryBeginBusyOperation();
        if (token != null) {
            return token;
        }

        // A CTS that can be cancelled by the activity itself or via 'cancellationToken' or via the timeout
        using CancellationTokenSource ctsBusyOperation = CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken);
        if (request.TimeoutMilliseconds > 0) {
            ctsBusyOperation.CancelAfter(request.TimeoutMilliseconds);
        }

        Task<IBusyToken?> busyOperation = request.QuickReleaseIntention
            ? this.InternalQuickReleaseLoop(ctsBusyOperation.Token)
            : this.InternalBeginBusyOperationLoop(ctsBusyOperation.Token);

        // Wait a short amount of time before starting an activity, to prevent it flashing on-screen
        await Task.WhenAny(busyOperation, Task.Delay(25, ctsBusyOperation.Token));
        if (busyOperation.IsCompleted) {
            return await busyOperation;
        }

        ActivityTask<IBusyToken?> activity = ActivityManager.Instance.RunTask(() => busyOperation, request.Progress, ctsBusyOperation);
        if (foregroundInfo.HasValue && foregroundInfo.Value.ShowDelay > 0) {
            if (IForegroundActivityService.TryGetInstance(out IForegroundActivityService? service)) {
                WaitForActivityResult result = await service.DelayedWaitForActivity(new WaitForActivityOptions(foregroundInfo.Value.TopLevel, activity) {
                    CanMinimizeIntoBackgroundActivity = true
                }, foregroundInfo.Value.ShowDelay);

                // If transitioning into a background activity, don't force-cancel the token acquisition process.
                // If the parent window was closed before the dialog opened, we don't need
                // to worry since the activity is directly cancellable and will show in the activity list.
                if (!result.TransitionToBackground && !busyOperation.IsCompleted) {
                    await ctsBusyOperation.CancelAsync();
                }
            }
        }

        return (await activity).GetValueOrDefault();
    }

    /// <summary>
    /// Tries to obtain the busy token from within an already running activity
    /// </summary>
    /// <param name="request">The busy token request information</param>
    /// <returns>The token, or null, if the request was cancelled</returns>
    /// <remarks>
    /// The returned token object must be disposed once your operation has completed,
    /// so that other operations can do busy operations
    /// </remarks>
    public async Task<IBusyToken?> BeginBusyOperationFromActivity(BusyTokenRequestFromActivity request) {
        if (request.CurrentTask == null)
            throw new ArgumentException($"Request contained a null {nameof(ActivityTask)}");
        if (request.CurrentTask != ActivityTask.Current)
            throw new ArgumentException($"Request contained a different activity from the current activity. {request.CurrentTask} != {ActivityTask.Current}");

        InForegroundInfo? foregroundInfo = request.ForegroundInfo;
        if (foregroundInfo.HasValue && foregroundInfo.Value.TopLevel == null) {
            throw new ArgumentException($"Request's InForeground info contained a null {nameof(ITopLevel)}");
        }

        IBusyToken? token = this.TryBeginBusyOperation();
        if (token != null) {
            return token;
        }

        // Check if someone passes the activity's cancellation token as a parameter
        // to this method. Nothing should go wrong if we passed two of the came CTs
        // to CreateLinkedTokenSource, but we may as well check anyway
        using CancellationTokenSource ctsBusyOperation = request.BusyCancellation.Equals(request.CurrentTask.CancellationToken)
            ? CancellationTokenSource.CreateLinkedTokenSource(request.BusyCancellation)
            : CancellationTokenSource.CreateLinkedTokenSource(request.BusyCancellation, request.CurrentTask.CancellationToken);

        if (request.TimeoutMilliseconds > 0) {
            ctsBusyOperation.CancelAfter(request.TimeoutMilliseconds);
        }

        using (request.CurrentTask.Progress.SaveState(WaitingMessage, newIsIndeterminate: true)) {
            if (!foregroundInfo.HasValue) {
                // Not showing in a foreground, so simply wait for the token
                // or until either activity or request token cancelled
                return request.QuickReleaseIntention
                    ? await this.InternalQuickReleaseLoop(ctsBusyOperation.Token)
                    : await this.InternalBeginBusyOperationLoop(ctsBusyOperation.Token);
            }

            Task<IBusyToken?> busyOperation = request.QuickReleaseIntention
                ? this.InternalQuickReleaseLoop(ctsBusyOperation.Token)
                : this.InternalBeginBusyOperationLoop(ctsBusyOperation.Token);

            if (IForegroundActivityService.TryGetInstance(out IForegroundActivityService? service)) {
                if (foregroundInfo.Value.ShowDelay > 0) {
                    // Wait for either the activity to complete, or for the show delay to elapse
                    await Task.WhenAny(busyOperation, Task.Delay(foregroundInfo.Value.ShowDelay, ctsBusyOperation.Token));
                }

                // Operation completed in the showing delay, so return it now
                if (busyOperation.IsCompleted) {
                    return await busyOperation;
                }

                WaitForActivityResult result;
                
                // Pass 'cts' so that the dialog closes ASAP, so that the caller can process any additional
                // cancellation logic it may want. If we don't pass cts, then nothing tells the dialog to close,
                // and it will deadlock the activity by waiting for it to complete from within the activity itself
                using (CancellationTokenSource ctsDialog = TaskUtils.CreateCompletionSource(busyOperation, ctsBusyOperation.Token)) {
                    result = await service.WaitForActivity(new WaitForActivityOptions(foregroundInfo.Value.TopLevel, request.CurrentTask, ctsDialog.Token) {
                        CancelActivityOnCloseRequest = false, // when the user tries to close it, just close the dialog
                        WaitForActivityOnCloseRequest = false, // do not wait for the activity to complete!!!
                        CanMinimizeIntoBackgroundActivity = request.CurrentTask.IsDirectlyCancellable
                    });
                }
                
                if (busyOperation.IsCompleted || result.TransitionToBackground) {
                    // Operation has completed, or we're transitioning into a background activity, so just wait here
                    return await busyOperation;
                }
                else if (result.ParentClosedEarly) {
                    // Parent window was closed before the dialog could open. If the activity
                    // cannot be cancelled directly (i.e. from the activity status bar),
                    // we need to cancel it, otherwise it might be permanently stuck waiting
                    if (!request.CurrentTask.IsDirectlyCancellable) {
                        await ctsBusyOperation.CancelAsync();
                    }
                }
                else /* if (!busyOperation.IsCompleted) */ {
                    // User might have closed the dialog, or the busyCancellation or activity became cancelled,
                    // and 'operationTask' might not have processed the cancellation yet.
                    // So, mark 'cts' as cancelled just to be safe in all cases
                    await ctsBusyOperation.CancelAsync();
                }
            }

            return await busyOperation;
        }
    }

    /// <summary>
    /// Ensures the token is a valid busy token and is our currently taken token (and is not someone elses)
    /// </summary>
    /// <param name="token">The token to validate</param>
    /// <exception cref="ArgumentNullException">Token is null</exception>
    /// <exception cref="ArgumentException">Object is not a token or is disposed, not associated with this lock or not the taken token</exception>
    public void ValidateToken(IBusyToken token) {
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
    public bool IsTokenValid([NotNullWhen(true)] IBusyToken? token) {
        if (!(token is BusyToken busy))
            return false;

        // myLock can be atomically exchanged
        BusyLock? theLock = busy.myLock;
        return theLock == this && this.activeToken == busy;
    }

    private async Task<IBusyToken?> InternalQuickReleaseLoop(CancellationToken cancellationToken = default) {
        IBusyToken? token = this.TryBeginBusyOperation();
        if (token != null) {
            return token; // heh, nice. No headaches!
        }

        // Create a TCS that is only marked as completed
        CancellableTaskCompletionSource myTcs = new CancellableTaskCompletionSource(cancellationToken, setSuccessfulInsteadOfCancelled: true);

        this.UserQuickReleaseRequested?.Invoke(this, myTcs);

        // Try and exchange the current tcs with ours.
        // If unsuccessful, we get the current one and just wait for it
        TaskCompletionSource? oldTcs;
        while ((oldTcs = Interlocked.CompareExchange(ref this.tcsPrimaryQuickReleaseAction, myTcs, null)) != null) {
            try {
                await oldTcs.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException e) {
                Debug.Assert(cancellationToken.IsCancellationRequested && e.CancellationToken == cancellationToken);
                return null;
            }
        }

        Debug.Assert(this.tcsPrimaryQuickReleaseAction == myTcs, "whaaaT!");

        // Broadcast to listeners to give up their busy token.
        // Hopefully they will release them in the call frame, but that probably won't happen

        try {
            // At this point, we are now the main waiter for the quick action.
            // tcsQuickActionBeginning will stay as myTcs until we replace it.
            if ((token = this.InternalTryBeginBusyOperation(myTcs)) != null) {
                return token;
            }

            return await this.InternalBeginBusyOperationLoop(cancellationToken, myTcs);
        }
        catch (OperationCanceledException) {
            // ignored
        }
        catch (Exception) {
            Debug.Fail("Unexpected exception");
        }
        finally {
            // Set as null first, which will hopefully prevent potential wasted energy
            // awaiting it again if we were to SetResult() first.
            this.tcsPrimaryQuickReleaseAction = null;

            // We don't mark it as cancelled because there's no need since this is the only
            // class waiting on it, so it's just a waste of a new OperationCancelledException
            // allocation. And it might result in an unobserved exception issue if cancelled
            myTcs.SetResult();
        }

        return null;
    }

    internal async Task<IBusyToken?> InternalBeginBusyOperationLoop(CancellationToken cancellationToken, TaskCompletionSource? tcsQuickReleaseAction = null) {
        IBusyToken? token;
        do {
            // If there's a quick action still running, and the caller isn't
            // the one initiating it, we will wait on that first.
            TaskCompletionSource? tcsQuickAction;
            while ((tcsQuickAction = this.tcsPrimaryQuickReleaseAction) != null && tcsQuickAction != tcsQuickReleaseAction /* don't wait on the caller tcs!!! deadlock!! */) {
                try {
                    await tcsQuickAction.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException e) {
                    Debug.Assert(cancellationToken.IsCancellationRequested && e.CancellationToken == cancellationToken);
                    return null;
                }
            }

            TaskCompletionSource? primaryTcs;
            while ((primaryTcs = this.tcsBusyLockUsage) != null) {
                try {
                    await primaryTcs.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException e) {
                    Debug.Assert(cancellationToken.IsCancellationRequested && e.CancellationToken == cancellationToken);
                    return null;
                }
            }
        } while ((token = this.InternalTryBeginBusyOperation(tcsQuickReleaseAction)) == null);

        return token;
    }

    private IBusyToken? InternalTryBeginBusyOperation(TaskCompletionSource? tcsQuickReleaseAction) {
        bool lockTaken = false;
        Lock lockObj = this.CriticalLock;

        try {
            if (!(lockTaken = lockObj.TryEnter()))
                return null;

            if (this.busyCount == 0 && tcsQuickReleaseAction == this.tcsPrimaryQuickReleaseAction /* ensure we are the primary taker */)
                return this.activeToken = new BusyToken(this);

            return null;
        }
        finally {
            if (lockTaken)
                lockObj.Exit();
        }
    }

    private void OnTokenCreatedUnderLock() {
        Debug.Assert(this.CriticalLock.IsHeldByCurrentThread, "Busy lock not acquired");
        if (Interlocked.Increment(ref this.busyCount) == 1) {
            try {
                this.IsBusyChanged?.Invoke(this);
            }
            catch {
                Debugger.Break(); // exceptions are swallowed because it's the user's fault for not catching errors :D
            }
        }

        TaskCompletionSource? oldTcs = Interlocked.Exchange(ref this.tcsBusyLockUsage, new TaskCompletionSource());
        Debug.Assert(oldTcs == null, "Huh!?");
        oldTcs?.TrySetResult();
    }

    private void OnTokenDisposedUnderLock() {
        Debug.Assert(this.CriticalLock.IsHeldByCurrentThread, "Busy lock not acquired");
        this.activeToken = null;
        if (Interlocked.Decrement(ref this.busyCount) == 0) {
            try {
                this.IsBusyChanged?.Invoke(this);
            }
            catch {
                Debugger.Break(); // exceptions are swallowed because it's the user's fault for not catching errors :D
            }
        }

        TaskCompletionSource? oldTcs = Interlocked.Exchange(ref this.tcsBusyLockUsage, null);
        Debug.Assert(oldTcs != null, nameof(this.tcsBusyLockUsage) + " should not have been set null while token is taken");
        oldTcs?.TrySetResult();
    }

    private class BusyToken : IBusyToken {
        public volatile BusyLock? myLock;

        public BusyLock? BusyLock => this.myLock;

#if TRACK_TOKEN_CREATION_STACK_TRACE
        // debugging stack trace, just in case the app locks up then the source is likely in here
        public string? creationStackTrace;
        public string? disposalStackTrace;

#endif

        public BusyToken(BusyLock theLock) {
            Debug.Assert(theLock.activeToken == null, "Active token already non-null");
            this.myLock = theLock;
            theLock.OnTokenCreatedUnderLock();

#if TRACK_TOKEN_CREATION_STACK_TRACE
            this.creationStackTrace = new StackTrace(true).ToString();
#endif
        }

        public void Dispose() {
            // we're being omega thread safe here
            BusyLock? theLock = Interlocked.Exchange(ref this.myLock, null);
            if (theLock == null) {
                return; // already disposed...
            }

#if TRACK_TOKEN_CREATION_STACK_TRACE
            string oldDisposalTrace = this.disposalStackTrace;
            this.disposalStackTrace = new StackTrace(true).ToString();
            Debug.Assert(oldDisposalTrace == null);
#endif

            lock (theLock.CriticalLock) {
                Debug.Assert(theLock.activeToken == this, "Different active token references");
                theLock.OnTokenDisposedUnderLock();
            }
        }
    }
}

/// <summary>
/// Contains information about a request for the busy token
/// </summary>
public readonly struct BusyTokenRequest {
    /// <summary>
    /// Gets the cancellation token used to cancel waiting for the busy token
    /// </summary>
    public required CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets the amount of milliseconds to wait. Default is -1, meaning wait forever (until <see cref="CancellationToken"/> is cancelled)
    /// </summary>
    public int TimeoutMilliseconds { get; init; } = -1;

    /// <summary>
    /// Gets whether the caller has quick release intention, meaning they don't intent on
    /// using the token for long, and any activities running that were using the token can
    /// resume once the caller has finished 
    /// </summary>
    public bool QuickReleaseIntention { get; init; }

    public BusyTokenRequest() {
    }
}

/// <summary>
/// Contains information about a request for the busy token using an activity
/// </summary>
public readonly struct BusyTokenRequestUsingActivity {
    /// <summary>
    /// Gets the cancellation token used to cancel waiting for the busy token and therefore cancel the activity
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets the amount of milliseconds to wait. Default is -1, meaning wait forever (until <see cref="CancellationToken"/> is cancelled)
    /// </summary>
    public int TimeoutMilliseconds { get; init; } = -1;

    /// <summary>
    /// Gets the progress used with the activity
    /// </summary>
    public IActivityProgress Progress { get; } = new DispatcherActivityProgress() {
        Caption = "New Operation",
        Text = BusyLock.WaitingMessage,
        IsIndeterminate = true
    };

    /// <summary>
    /// Gets or sets whether the caller has a quick-release intention, meaning they don't intend on
    /// using the token for long, and any activities running that were using the token can
    /// resume once the caller has finished 
    /// </summary>
    public bool QuickReleaseIntention { get; init; }

    /// <summary>
    /// Gets the foreground info, which, when present, is used to show the activity in a foreground
    /// </summary>
    public InForegroundInfo? ForegroundInfo { get; init; }

    public BusyTokenRequestUsingActivity() {
    }
}

/// <summary>
/// Contains information about a request for the busy token from within a running activity
/// </summary>
public readonly struct BusyTokenRequestFromActivity {
    /// <summary>
    /// Gets the activity task this struct was created with
    /// </summary>
    public ActivityTask CurrentTask { get; } = ActivityTask.Current;

    /// <summary>
    /// Gets the cancellation token used to cancels waiting for the busy token. This will not necessarily
    /// cause the caller activity to become cancelled, unless the caller passes the activity's
    /// <see cref="ActivityTask.CancellationToken"/> as this property 
    /// </summary>
    public CancellationToken BusyCancellation { get; init; }

    /// <summary>
    /// Gets the amount of milliseconds to wait. Default is -1, meaning wait forever (until <see cref="BusyCancellation"/> is cancelled)
    /// </summary>
    public int TimeoutMilliseconds { get; init; } = -1;

    /// <summary>
    /// Gets or sets whether the caller has a quick-release intention, meaning they don't intend on
    /// using the token for long, and any activities running that were using the token can
    /// resume once the caller has finished 
    /// </summary>
    public bool QuickReleaseIntention { get; init; }

    /// <summary>
    /// Gets the foreground info, which, when present, is used to show the activity in a foreground
    /// </summary>
    public InForegroundInfo? ForegroundInfo { get; init; }

    public BusyTokenRequestFromActivity() {
    }
}

/// <summary>
/// Contains information about showing a foreground for the busy token with activities (either using a new one or from within one)
/// </summary>
public readonly struct InForegroundInfo(ITopLevel topLevel, int showDelay = BusyLock.DefaultForegroundDelay) {
    /// <summary>
    /// Gets the top-level that the foreground dialog will be shown relative to
    /// </summary>
    public ITopLevel TopLevel { get; } = topLevel;

    /// <summary>
    /// Gets the show delay, which is how long to wait before showing the dialog (since it may not take long to take the busy token)
    /// </summary>
    public int ShowDelay { get; } = showDelay;

    [Obsolete("Do not use empty constructor")]
    public InForegroundInfo() : this(null, 0) {
    }
}