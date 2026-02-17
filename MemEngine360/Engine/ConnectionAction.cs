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
using PFXToolKitUI.Activities;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine;

public delegate Task<bool> ConnectionActionSetupHandler(ConnectionAction action, IConsoleConnection connection, bool hasConnectionChanged);

public delegate Task ConnectionActionRunHandler(ConnectionAction action, IConsoleConnection connection);

/// <summary>
/// Abstract an action that uses a connection, while handling common problems and annoyances (e.g. connection changes between acquiring busy token)
/// </summary>
public class ConnectionAction {
    private readonly IConnectionLockPair connectionLockPair;
    private bool isRunning;

    /// <summary>
    /// Gets or sets if we should run an activity to acquire the busy lock.
    /// Default value is true.
    /// </summary>
    public bool UseActivityToGetLock { get; init; } = true;

    /// <summary>
    /// Gets or sets if we should start a new activity when <see cref="UseActivityToGetLock"/> is true,
    /// even if <see cref="RunAsync"/> is called from within an activity.
    /// </summary>
    public bool UseNewActivity { get; init; }

    /// <summary>
    /// Gets or sets if we should show the activity as a foreground process.
    /// Only used when <see cref="UseActivityToGetLock"/> is true. Default value is true.
    /// </summary>
    public bool UseForegroundActivity { get; init; } = true;

    /// <summary>
    /// Gets or sets the amount of time to wait before actually showing the foreground dialog.
    /// This is useful to prevent the dialog flashing onscreen if the application isn't busy and we acquire the token quickly.
    /// Default value is <see cref="BusyLock.DefaultForegroundDelay"/> milliseconds.
    /// </summary>
    public int ForegroundDialogShowDelay { get; init; } = BusyLock.DefaultForegroundDelay;

    /// <summary>
    /// Gets or sets the timeout for trying to acquire the busy lock when <see cref="UseActivityToGetLock"/> is false.
    /// Default value is 500 milliseconds.
    /// </summary>
    public int NonActivityBusyLockTimeout { get; init; } = 500;

    /// <summary>
    /// Gets or sets the cancellation token used to stop trying to acquire the busy token when <see cref="UseActivityToGetLock"/> is false.
    /// </summary>
    public CancellationToken NonActivityCancellationToken { get; init; }

    /// <summary>
    /// Gets or sets the caption used for the activity. Only used when <see cref="UseActivityToGetLock"/> is true
    /// </summary>
    public string ActivityCaption { get; init; } = "New Operation";

    /// <summary>
    /// Gets or sets the error message to show if the connection changes between acquiring the busy
    /// token. This is only used when <see cref="CanRetryOnConnectionChanged"/> is true.
    /// Default value is null, because we use another default message based on if the connection changed or was disconnected
    /// </summary>
    public string? ConnectionChangedErrorMessage { get; init; } = null;

    /// <summary>
    /// Gets or sets if <see cref="Setup"/> should be invoked again if the connection changes between
    /// the last setup and acquiring the busy token. Default value is false.
    /// </summary>
    public bool CanRetryOnConnectionChanged { get; init; } = false;

    /// <summary>
    /// Gets the currently acquired busy token. This is at least non-null during a
    /// call to <see cref="PrepareConnection"/> and <see cref="Setup"/>
    /// </summary>
    public IBusyToken? CurrentBusyToken { get; private set; }

    /// <summary>
    /// Gets or sets the initial setup function. The provided connection will be non-null and not closed.
    /// We will not have the busy token yet when this is invoked.
    /// <para>
    /// This may be invoked multiple times if the connection changes between busy-lock acquisition,
    /// and only if <see cref="CanRetryOnConnectionChanged"/> is true, otherwise <see cref="RunAsync"/> returns false.
    /// </para>
    /// </summary>
    public ConnectionActionSetupHandler? Setup { get; init; }

    /// <summary>
    /// Gets or sets the final run operation. The provided connection will be non-null and not closed.
    /// This is only executed after <see cref="Setup"/> returns true. We will have the busy token at this point.
    /// </summary>
    public required ConnectionActionRunHandler Execute { get; init; }

    /// <summary>
    /// Gets the error that caused <see cref="RunAsync"/> to return false
    /// </summary>
    public ErrorState Error { get; private set; }

    public ConnectionAction(IConnectionLockPair connectionLockPair, IBusyToken? initialBusyToken = null) {
        this.connectionLockPair = connectionLockPair;
        this.CurrentBusyToken = initialBusyToken;
    }

    /// <summary>
    /// Prepares the connection, runs <see cref="Setup"/> (if used) and finally runs our <see cref="Execute"/>
    /// function once the connection is okay and the busy token is acquired.
    /// </summary>
    /// <returns>True if <see cref="Execute"/> was invoked, False otherwise</returns>
    public async Task<bool> RunAsync() {
        if (this.isRunning) {
            throw new InvalidOperationException("Already running");
        }

        this.isRunning = true;
        this.Error = ErrorState.None;

        IConsoleConnection? connection = this.connectionLockPair.Connection;
        if (!await this.CheckIsConnected(connection, false)) {
            this.isRunning = false;
            return false;
        }

        Debug.Assert(connection != null);
        bool hasInitialBusyToken = this.CurrentBusyToken != null;

        try {
            return await this.InternalRunAsync(connection);
        }
        finally {
            if (!hasInitialBusyToken) {
                // If we acquired the token, then dispose it
                this.CurrentBusyToken?.Dispose();
                this.CurrentBusyToken = null;
            }
        }
    }

    private async Task<bool> InternalRunAsync(IConsoleConnection connection) {
        bool hasRunAgain = false;

        do {
            if (!await this.RunSetupAsync(connection, hasRunAgain)) {
                this.Error = ErrorState.SetupFailed;
                return false;
            }

            await this.TryAcquireBusyToken();
            if (this.CurrentBusyToken == null) {
                this.Error = ErrorState.BusyTokenAcquisitionCancelled;
                return false;
            }

            // Check if connection has changed
            IConsoleConnection? newConnection = this.connectionLockPair.Connection;
            if (ReferenceEquals(newConnection, connection)) {
                if (!await this.CheckIsConnected(connection, true)) {
                    return false;
                }

                break; // break loop and go to RunExecuteAsync
            }

            // Connection has changed. Can we re-run Setup?
            if (!this.CanRetryOnConnectionChanged) {
                this.Error = ErrorState.ConnectionChangedAfterSetup;
                MessageBoxInfo info = new MessageBoxInfo(newConnection == null ? MessageBoxes.ConnectionDisconnectedSinceSetup : MessageBoxes.ConnectionChancedSinceSetup);
                if (this.ConnectionChangedErrorMessage != null) {
                    info.Message = this.ConnectionChangedErrorMessage;
                }

                await IMessageDialogService.Instance.ShowMessage(info);
                return false;
            }

            // Is newConnection valid?
            if (!await this.CheckIsConnected(newConnection, true)) {
                return false;
            }

            Debug.Assert(newConnection != null);
            connection = newConnection;
            hasRunAgain = true;
        } while (true);

        await this.RunExecuteAsync(connection);
        return true;
    }

    private async Task TryAcquireBusyToken() {
        if (this.CurrentBusyToken != null) {
            return;
        }

        BusyLock busyLock = this.connectionLockPair.BusyLock;
        if (this.UseActivityToGetLock) {
            ITopLevel? topLevel;
            if (this.UseForegroundActivity && (topLevel = TopLevelContextUtils.GetTopLevelFromContext()) != null) {
                if (!this.UseNewActivity && ActivityManager.Instance.TryGetCurrentTask(out ActivityTask? currentActivity)) {
                    // We're currently inside another activity, so try show the foreground from within
                    using (currentActivity.Progress.SaveState(Optional<string?>.Empty, this.ActivityCaption)) {
                        this.CurrentBusyToken = await busyLock.BeginBusyOperationFromActivity(new BusyTokenRequestFromActivity() {
                            BusyCancellation = CancellationToken.None,
                            ForegroundInfo = new InForegroundInfo(topLevel, this.ForegroundDialogShowDelay)
                        });
                    }
                }
                else {
                    this.CurrentBusyToken = await busyLock.BeginBusyOperationUsingActivity(new BusyTokenRequestUsingActivity() {
                        Progress = {
                            Caption = this.ActivityCaption,
                            Text = BusyLock.WaitingMessage,
                        },
                        ForegroundInfo = new InForegroundInfo(topLevel, this.ForegroundDialogShowDelay)
                    });
                }
            }
            else {
                if (!this.UseNewActivity && ActivityManager.Instance.TryGetCurrentTask(out ActivityTask? currentActivity)) {
                    // We're currently inside another activity, so try show the foreground from within
                    using (currentActivity.Progress.SaveState(Optional<string?>.Empty, this.ActivityCaption)) {
                        this.CurrentBusyToken = await busyLock.BeginBusyOperationFromActivity(new BusyTokenRequestFromActivity());
                    }
                }
                else {
                    this.CurrentBusyToken = await busyLock.BeginBusyOperationUsingActivity(new BusyTokenRequestUsingActivity() {
                        Progress = {
                            Caption = this.ActivityCaption,
                            Text = BusyLock.WaitingMessage
                        }
                    });
                }
            }
        }
        else {
            this.CurrentBusyToken = await busyLock.BeginBusyOperation(this.NonActivityBusyLockTimeout, this.NonActivityCancellationToken);
        }
    }

    protected virtual async Task<bool> RunSetupAsync(IConsoleConnection connection, bool hasRunAgain) {
        if (this.Setup == null) {
            return true;
        }

        try {
            if (!await this.Setup(this, connection, hasRunAgain)) {
                return false;
            }
        }
        catch (TimeoutException e) {
            await IMessageDialogService.Instance.ShowMessage("Network Error", "Connection timed out", e.Message);
            return false;
        }
        catch (IOException e) {
            await IMessageDialogService.Instance.ShowMessage("Network Error", "General network error", e.Message);
            return false;
        }

        return true;
    }

    protected virtual async Task RunExecuteAsync(IConsoleConnection connection) {
        try {
            await this.Execute(this, connection);
        }
        catch (TimeoutException e) {
            await IMessageDialogService.Instance.ShowMessage("Network Error", "Connection timed out", e.Message);
        }
        catch (IOException e) {
            await IMessageDialogService.Instance.ShowMessage("Network Error", "General network error", e.Message);
        }
    }

    protected async Task<bool> CheckIsConnected(IConsoleConnection? connection, bool isAfterBusyTokenAcquisition) {
        if (connection == null) {
            this.Error = ErrorState.NoConnection;
            await MessageBoxes.NoConnection.ShowMessage();
            return false;
        }

        if (connection.IsClosed) {
            this.Error = ErrorState.ConnectionClosed;
            await MessageBoxes.ClosedConnection.ShowMessage();
            return false;
        }

        return true;
    }

    public enum ErrorState {
        None,
        NoConnection,
        ConnectionClosed,
        SetupFailed,
        BusyTokenAcquisitionCancelled,

        /// <summary>
        /// Note - this state is only allowed when <see cref="ConnectionAction.CanRetryOnConnectionChanged"/> is false
        /// </summary>
        ConnectionChangedAfterSetup
    }
}