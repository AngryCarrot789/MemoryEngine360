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
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.PointerScanning;
using MemEngine360.Sequencing;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine;

public delegate void MemoryEngineEventHandler(MemoryEngine sender);

public delegate Task MemoryEngineConnectionChangingEventHandler(MemoryEngine sender, ulong frame);

public delegate void MemoryEngineConnectionChangedEventHandler(MemoryEngine sender, ulong frame, IConsoleConnection? oldConnection, IConsoleConnection? newConnection, ConnectionChangeCause cause);

/// <summary>
/// The main manager class for a the memory engine window. Also provides utilities for reading/writing data values
/// </summary>
[DebuggerDisplay("ActiveToken = {activeToken}, Connection = {Connection}")]
public class MemoryEngine {
    public static readonly DataKey<MemoryEngine> EngineDataKey = DataKey<MemoryEngine>.Create("MemoryEngine");

    private volatile IConsoleConnection? connection; // our connection object -- volatile in case JIT plays dirty tricks, i ain't no expert in wtf volatile does though
    private readonly object connectionLock = new object(); // used to synchronize busy token creation and also connection change
    private volatile int busyCount; // this is a boolean. 0 = no token, 1 = token acquired. any other value is invalid
    private volatile BusyToken? activeToken;
    private bool isShuttingDown;
    private ulong currentConnectionAboutToChangeFrame;

    // A list of TCSes that are signal when the busy lock becomes available.
    // They are custom in that they also support a CancellationToken to signal them too
    private readonly LinkedList<CancellableTaskCompletionSource> busyLockAsyncWaiters;

    /// <summary>
    /// Gets or sets the current console connection
    /// <para>
    /// It's crucial that when using any command that requires sending/receiving data from the console that
    /// it is synchronized with <see cref="BeginBusyOperation"/> or any of the async overloads, because,
    /// connections may not be thread-safe (but may implement fail-safety when trying to read/write concurrently)
    /// </para>
    /// <para>
    /// There are two ways to interact with a connection. The first is try get lock, otherwise do nothing
    /// </para>
    /// <code>
    /// <![CDATA[
    /// using IDisposable? token = engine.BeginBusyOperation();
    /// if (token != null && engine.Connection != null) {
    ///     // do work involving connection
    /// }
    /// ]]>
    /// </code>
    /// <para>
    /// Alternatively, you can use <see cref="BeginBusyOperationAsync"/> which waits until you get the
    /// token and accepts a cancellation token, or use <see cref="BeginBusyOperationActivityAsync"/> which
    /// creates an activity to show the user you're waiting for the busy operations to complete
    /// </para>
    /// </summary>
    public IConsoleConnection? Connection => this.connection;

    /// <summary>
    /// Gets the <see cref="Connections.UserConnectionInfo"/> that was used to connect to a console. It is set
    /// by calling <see cref="SetConnection"/> with a non-null connection and non-null UCInfo, before <see cref="ConnectionChanged"/> is fired.
    /// <para>
    /// This is used to support reconnecting to the console when the connection was lost, without having to reconfigure all the options
    /// </para>
    /// </summary>
    public UserConnectionInfo? LastUserConnectionInfo { get; private set; }

    /// <summary>
    /// Gets or sets if the memory engine is currently busy, e.g. reading or writing data.
    /// This will never be true when <see cref="Connection"/> is null
    /// </summary>
    public bool IsConnectionBusy => this.busyCount > 0;

    public ScanningProcessor ScanningProcessor { get; }

    public TaskSequencerManager TaskSequencerManager { get; }

    public AddressTableManager AddressTableManager { get; }
    
    public PointerScanner PointerScanner { get; }

    /// <summary>
    /// Gets or sets if the memory engine is in the process of shutting down. Prevents scanning working
    /// </summary>
    public bool IsShuttingDown {
        get => this.isShuttingDown;
        set => PropertyHelper.SetAndRaiseINE(ref this.isShuttingDown, value, this, static t => t.IsShuttingDownChanged?.Invoke(t));
    }

    /// <summary>
    /// An async event fired when a connection is most likely about to change. This can be used by custom activities
    /// to cancel operations that are using the connection. There is no guarantee that the connection will actually
    /// change, and this event may get fired multiple times before the connection really changes
    /// <para>
    /// Beware, handlers are invoked in a background task (via <see cref="Task.Run(Func{Task})"/>), not on the
    /// main thread, so you must jump back to the main thread if required. <see cref="OperationCanceledException"/> exceptions are swallowed
    /// and regular exceptions are posted back to the main thread which will crash the application
    /// </para>
    /// <para>
    /// Note! This may not be called at all; maybe the connection is changing in a non-async context and the nature of the
    /// context makes it unable to wait for handlers to stop using the connection. So this event is just a hint to prevent
    /// potential timeout/IO exceptions from popping up in the UI and inconveniencing the user
    /// </para>
    /// <para>
    /// Also note, this event is fired while the busy token is acquired, so handlers cannot obtain it. However, if you absolutely
    /// need to do busy operations in this event, then do them on the main thread (see <see cref="IDispatcher.InvokeAsync"/>) and do
    /// not await any code, otherwise you risk multiple handlers invoking busy operation code concurrently and if you await on the
    /// main thread then you risk another handler's code being invoked between your await usage
    /// </para>
    /// </summary>
    public event MemoryEngineConnectionChangingEventHandler? ConnectionAboutToChange;

    /// <summary>
    /// Fired when <see cref="Connection"/> changes. It is crucial that no 'busy' operations
    /// are performed in the event handlers, otherwise, a deadlock could occur.
    /// </summary>
    public event MemoryEngineConnectionChangedEventHandler? ConnectionChanged;

    /// <summary>
    /// An event fired when <see cref="IsShuttingDown"/> changes. Ideally this should only be fired once per instance of <see cref="MemoryEngine"/>
    /// </summary>
    public event MemoryEngineEventHandler? IsShuttingDownChanged;

    /// <summary>
    /// Fired when the <see cref="IsConnectionBusy"/> state changes. It is crucial that no 'busy' operations are performed
    /// in the event handlers, otherwise, a deadlock could occur.
    /// <para>
    /// It's also important that exceptions are not thrown in the handlers, because they will be swallowed and never see
    /// the light of day, and the next handlers in the list will not be invoked, potentially leading to application wide corruption
    /// </para>
    /// </summary>
    public event MemoryEngineEventHandler? IsBusyChanged;

    public MemoryEngine() {
        this.ScanningProcessor = new ScanningProcessor(this);
        this.AddressTableManager = new AddressTableManager(this);
        this.TaskSequencerManager = new TaskSequencerManager(this);
        this.PointerScanner = new PointerScanner(this);
        this.busyLockAsyncWaiters = new LinkedList<CancellableTaskCompletionSource>();
        Task.Factory.StartNew(async () => {
            long timeSinceRefreshedAddresses = DateTime.Now.Ticks;
            BasicApplicationConfiguration cfg = BasicApplicationConfiguration.Instance;

            while (!this.IsShuttingDown) {
                IConsoleConnection? conn = this.connection;
                if (conn != null && !conn.IsConnected) {
                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                        // rarely not the case (depending on how quickly the callback runs on the main thread)
                        if (conn == this.Connection) {
                            this.CheckConnection();
                        }
                    });
                }

                await Task.Delay(250);
                if (cfg.IsAutoRefreshResultsEnabled && !this.IsShuttingDown) {
                    if ((DateTime.Now.Ticks - timeSinceRefreshedAddresses) >= (cfg.RefreshRateMillis * Time.TICK_PER_MILLIS)) {
                        await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => this.ScanningProcessor.RefreshSavedAddressesAsync());
                        timeSinceRefreshedAddresses = DateTime.Now.Ticks;
                    }
                }
            }
        }, TaskCreationOptions.LongRunning);
    }

    /// <summary>
    /// Gets the connection while validating the token
    /// </summary>
    /// <param name="token">The token to validate</param>
    /// <returns>The current connection</returns>
    /// <exception cref="InvalidOperationException">Token is invalid in some way</exception>
    public IConsoleConnection? GetConnection(IDisposable token) {
        this.ValidateToken(token);
        return this.connection;
    }

    /// <summary>
    /// Fires our <see cref="ConnectionAboutToChange"/> event and waits for all handlers to complete.
    /// This method will not throw any exceptions encountered during the event handlers, instead,
    /// they're dispatched back to the main thread (excluding <see cref="OperationCanceledException"/>)
    /// </summary>
    /// <param name="frame">The connection changing frame. See docs for <see cref="GetNextConnectionChangeFrame"/> for more info</param>
    /// <exception cref="Exception"></exception>
    public async Task BroadcastConnectionAboutToChange(ulong frame) {
        Delegate[]? list = this.ConnectionAboutToChange?.GetInvocationList();
        if (list != null) {
            Task[] tasks = new Task[list.Length];
            for (int i = 0; i < list.Length; i++) {
                MemoryEngineConnectionChangingEventHandler handler = (MemoryEngineConnectionChangingEventHandler) list[i];
                tasks[i] = Task.Run(async () => {
                    try {
                        await handler(this, frame);
                    }
                    catch (OperationCanceledException) {
                        // ignored
                    }
                    catch (Exception ex) {
                        ApplicationPFX.Instance.Dispatcher.Post(() => throw ex, DispatchPriority.Send);
                    }
                });
            }

            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// Sets the current connection, with the given cause. Must be called on main thread
    /// </summary>
    /// <param name="token">The busy operation token that is valid</param>
    /// <param name="frame">The connection change frame. Set to 0 if you have no idea what this is used for</param>
    /// <param name="newConnection">The new connection object</param>
    /// <param name="cause">The cause for connection change</param>
    /// <exception cref="InvalidOperationException">Token is invalid</exception>
    /// <exception cref="ArgumentException">New connection is non-null when cause is <see cref="ConnectionChangeCause.LostConnection"/></exception>
    public void SetConnection(IDisposable busyToken, ulong frame, IConsoleConnection? newConnection, ConnectionChangeCause cause, UserConnectionInfo? userConnectionInfo = null) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        this.ValidateToken(busyToken);
        if (newConnection != null && (cause == ConnectionChangeCause.LostConnection || cause == ConnectionChangeCause.ConnectionError))
            throw new ArgumentException($"Cause cannot be {cause} when setting connection to a non-null value");

        if (newConnection == null && userConnectionInfo != null)
            throw new ArgumentException(nameof(userConnectionInfo) + " is non-null when " + nameof(newConnection) + " is null");

        // ConnectionChanged is invoked under the lock to enforce busy operation rules
        lock (this.connectionLock) {
            // we don't necessarily need to access connection under lock since if we have
            // a valid busy token then nothing can modify it, but better be safe than sorry
            IConsoleConnection? oldConnection = this.connection;
            if (ReferenceEquals(oldConnection, newConnection)) {
                throw new ArgumentException("Cannot set the connection to the same value");
            }

            this.connection = newConnection;
            if (newConnection != null)
                this.LastUserConnectionInfo = userConnectionInfo;
            this.ConnectionChanged?.Invoke(this, frame, oldConnection, newConnection, cause);
        }
    }

    /// <summary>
    /// Increments and gets the next frame used by the connection changing/changed mechanism. Frames are used to
    /// determine if the connection is changing and actual change happened within the same context, which can
    /// be used to then resume an operation.
    /// <para>
    /// To use this system, you would handle <see cref="ConnectionAboutToChange"/> and stop your operation(s) and store the frame
    /// in a field. Then handle <see cref="ConnectionChanged"/>, and check if the frame given equals your stored frame.
    /// If they're equal, you can resume your operation
    /// </para>
    /// </summary>
    /// <returns>The next frame. Will never return 0</returns>
    public ulong GetNextConnectionChangeFrame() {
        ulong frame;
        do {
            frame = Interlocked.Increment(ref this.currentConnectionAboutToChangeFrame);
        } while (frame == 0); // should only ever loop twice which is when it's at ulong.MaxValue, some fucking how 

        return frame;
    }

    /// <summary>
    /// Begins a busy operation that uses the <see cref="Connection"/>. Dispose to finish the busy operation.
    /// <para>
    /// It is vital to use the busy-operation system when reading from or writing to the <see cref="Connection"/>,
    /// because the connection may not support concurrent commands, which may result in corruption in the pipeline
    /// </para>
    /// </summary>
    /// <returns>A token to dispose when the operation is completed. Returns null if currently busy</returns>
    /// <exception cref="InvalidOperationException">No connection is present</exception>
    public IDisposable? BeginBusyOperation() {
        bool lockTaken = false;
        try {
            Monitor.TryEnter(this.connectionLock, 0, ref lockTaken);
            if (!lockTaken)
                return null;

            if (this.busyCount == 0)
                return this.activeToken = new BusyToken(this);

            return null;
        }
        finally {
            if (lockTaken)
                Monitor.Exit(this.connectionLock);
        }
    }

    /// <summary>
    /// Begins a busy operation, waiting for existing busy operations to finish 
    /// </summary>
    /// <param name="cancellationToken">Used to cancel the operation, causing the task to return a null busy token</param>
    /// <returns>The acquired token, or null if the task was cancelled</returns>
    /// <exception cref="TaskCanceledException">Operation was cancelled, didn't get token in time</exception>
    public async Task<IDisposable?> BeginBusyOperationAsync(CancellationToken cancellationToken) {
        IDisposable? token = this.BeginBusyOperation();
        while (token == null) {
            LinkedListNode<CancellableTaskCompletionSource>? tcs = this.EnqueueAsyncWaiter(cancellationToken);
            if (tcs == null) {
                if (this.busyCount != 0) {
                    // connectionLock is acquired on another thread and the busy token is still taken,
                    // so the only thing we can do is just wait some time.
                    try {
                        await Task.Delay(10, cancellationToken);
                    }
                    catch (OperationCanceledException) {
                        return null;
                    }
                }
            }
            else {
                try {
                    await tcs.Value.Task;
                }
                catch (OperationCanceledException) {
                    if (tcs.List != null) {
                        lock (this.connectionLock) {
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

            token = this.BeginBusyOperation();
        }

        return token;
    }

    /// <summary>
    /// Tries to begin a busy operation. If we could not get the token immediately, we start
    /// a new activity and try to get it asynchronously with the text 'waiting for busy operations' 
    /// </summary>
    /// <param name="message">The message set as the <see cref="IActivityProgress.Text"/> property</param>
    /// <returns>
    /// A task with the token, or null if the user cancelled the operation or some other weird error occurred
    /// </returns>
    public async Task<IDisposable?> BeginBusyOperationActivityAsync(string caption = "New Operation", string message = "Waiting for busy operations...", CancellationTokenSource? cancellationTokenSource = null) {
        IDisposable? token = this.BeginBusyOperation();
        if (token == null) {
            CancellationTokenSource cts = cancellationTokenSource ?? new CancellationTokenSource();
            token = await ActivityManager.Instance.RunTask(() => {
                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = caption;
                task.Progress.Text = message;
                return this.BeginBusyOperationAsync(task.CancellationToken);
            }, cts);

            if (cancellationTokenSource == null) {
                cts.Dispose();
            }
        }

        return token;
    }

    /// <summary>
    /// Gets a busy token via <see cref="BeginBusyOperationActivityAsync(string)"/> and invokes a callback if the connection is available
    /// </summary>
    /// <param name="action">The callback to invoke when we have the token</param>
    /// <param name="message">A message to pass to the <see cref="BeginBusyOperationActivityAsync(string)"/> method</param>
    public async Task BeginBusyOperationActivityAsync(Func<IDisposable, IConsoleConnection, Task> action, string caption = "New Operation", string message = "Waiting for busy operations...") {
        using IDisposable? token = await this.BeginBusyOperationActivityAsync(caption, message);
        if (token != null && this.connection != null) {
            await action(token, this.connection!);
        }
    }

    /// <summary>
    /// Gets a busy token via <see cref="BeginBusyOperationActivityAsync(string)"/> and invokes a callback if the connection is available
    /// </summary>
    /// <param name="action">The callback to invoke when we have the token</param>
    /// <param name="message">A message to pass to the <see cref="BeginBusyOperationActivityAsync(string)"/> method</param>
    /// <typeparam name="TResult">The result of the callback task</typeparam>
    /// <returns>The task containing the result of action, or default if we couldn't get the token or connection was null</returns>
    public async Task<TResult?> BeginBusyOperationActivityAsync<TResult>(Func<IDisposable, IConsoleConnection, Task<TResult>> action, string caption = "New Operation", string message = "Waiting for busy operations...") {
        using IDisposable? token = await this.BeginBusyOperationActivityAsync(caption, message);
        if (token != null && this.connection != null) {
            return await action(token, this.connection!);
        }

        return default;
    }

    public void CheckConnection(ConnectionChangeCause likelyCause = ConnectionChangeCause.LostConnection) {
        // CheckConnection is just a helpful method to clear connection if it's 
        // disconnected internally, therefore, we don't need over the top synchronization,
        // because any code that actually tries to read/write will be async and can handle
        // the timeout exceptions
        IConsoleConnection? conn = this.connection;
        if (conn == null || conn.IsConnected) {
            return;
        }

        using (IDisposable? token1 = this.BeginBusyOperation()) {
            if (token1 != null && this.TryDisconnectForLostConnection(token1, likelyCause)) {
                return;
            }
        }

        ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
            using IDisposable? token2 = this.BeginBusyOperation();
            if (token2 != null)
                this.TryDisconnectForLostConnection(token2, likelyCause);
        }, DispatchPriority.Background);
    }

    /// <summary>
    /// Attempts to auto-disconnect the connection immediately if it is no longer actually connected (<see cref="IConsoleConnection.IsConnected"/> is false)
    /// </summary>
    /// <param name="token"></param>
    public void CheckConnection(IDisposable token, ConnectionChangeCause likelyCause = ConnectionChangeCause.LostConnection) {
        this.ValidateToken(token);
        this.TryDisconnectForLostConnection(token, likelyCause);
    }

    private bool TryDisconnectForLostConnection(IDisposable token, ConnectionChangeCause cause) {
        IConsoleConnection? conn = this.connection;
        if (conn == null)
            return true;
        if (conn.IsConnected)
            return false;

        this.SetConnection(token, 0, null, cause);
        return true;
    }
    
    private void ValidateToken(IDisposable token) {
        if (token == null)
            throw new ArgumentNullException(nameof(token), "Token is null");
        if (!(token is BusyToken busy))
            throw new ArgumentException("Argument is not a busy token object");

        // myEngine can be atomically exchanged
        MemoryEngine? engine = busy.myEngine;
        if (engine == null)
            throw new ArgumentException("Token has already been disposed");
        if (engine != this)
            throw new ArgumentException("Token is not associated with this engine");
        if (this.activeToken != busy)
            throw new ArgumentException(this.busyCount == 0 ? "No tokens are currently in use" : "Token is not the current token");
    }
    
    private LinkedListNode<CancellableTaskCompletionSource>? EnqueueAsyncWaiter(CancellationToken token) {
        bool lockTaken = false;
        try {
            Monitor.TryEnter(this.connectionLock, 0, ref lockTaken);
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
                Monitor.Exit(this.connectionLock);
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
    
    /// <summary>
    /// Reads a data value from the console
    /// </summary>
    /// <param name="connection">The connection to read the value from</param>
    /// <param name="address">The address to read at</param>
    /// <param name="dataType">The type of value we want to read</param>
    /// <param name="stringType"></param>
    /// <param name="strlen">The length of the string. Only used when the data type is <see cref="DataType.String"/></param>
    /// <param name="arrlen">The amount of array elements. Only used when the data type is <see cref="DataType.ByteArray"/> (or any array type, if we support that in the future)</param>
    /// <returns>The data value</returns>
    /// <exception cref="ArgumentOutOfRangeException">Invalid data type</exception>
    public static async Task<IDataValue> ReadDataValue(IConsoleConnection connection, uint address, DataType dataType, StringType stringType, int strlen, int arrlen) {
        switch (dataType) {
            case DataType.Byte:      return new DataValueByte(await connection.ReadByte(address).ConfigureAwait(false));
            case DataType.Int16:     return new DataValueInt16(await connection.ReadValue<short>(address).ConfigureAwait(false));
            case DataType.Int32:     return new DataValueInt32(await connection.ReadValue<int>(address).ConfigureAwait(false));
            case DataType.Int64:     return new DataValueInt64(await connection.ReadValue<long>(address).ConfigureAwait(false));
            case DataType.Float:     return new DataValueFloat(await connection.ReadValue<float>(address).ConfigureAwait(false));
            case DataType.Double:    return new DataValueDouble(await connection.ReadValue<double>(address).ConfigureAwait(false));
            case DataType.String:    return new DataValueString(await connection.ReadString(address, strlen, stringType.ToEncoding()).ConfigureAwait(false), stringType);
            case DataType.ByteArray: return new DataValueByteArray(await connection.ReadBytes(address, arrlen).ConfigureAwait(false));
            default:                 throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
        }
    }

    /// <summary>
    /// Same as <see cref="ReadAsDataValue(IConsoleConnection,uint,MemEngine360.Engine.Modes.DataType,StringType,uint,uint)"/>
    /// except this method uses the information from the given <see cref="IDataValue"/> to read the value in the exact same format.
    /// This basically just reads the latest version of the given data value
    /// </summary>
    /// <param name="connection">The connection to read the value from</param>
    /// <param name="address">The address to read at</param>
    /// <param name="value">The value we use things from like string type, and strlen/arrlen length</param>
    /// <returns>The latest version of <see cref="value"/></returns>
    /// <exception cref="Exception">Invalid data type</exception>
    public static async Task<IDataValue> ReadDataValue(IConsoleConnection connection, uint address, IDataValue value) {
        switch (value.DataType) {
            case DataType.Byte:      return new DataValueByte(await connection.ReadByte(address).ConfigureAwait(false));
            case DataType.Int16:     return new DataValueInt16(await connection.ReadValue<short>(address).ConfigureAwait(false));
            case DataType.Int32:     return new DataValueInt32(await connection.ReadValue<int>(address).ConfigureAwait(false));
            case DataType.Int64:     return new DataValueInt64(await connection.ReadValue<long>(address).ConfigureAwait(false));
            case DataType.Float:     return new DataValueFloat(await connection.ReadValue<float>(address).ConfigureAwait(false));
            case DataType.Double:    return new DataValueDouble(await connection.ReadValue<double>(address).ConfigureAwait(false));
            case DataType.String:    return new DataValueString(await connection.ReadString(address, ((DataValueString) value).Value.Length, ((DataValueString) value).StringType.ToEncoding()).ConfigureAwait(false), ((DataValueString) value).StringType);
            case DataType.ByteArray: return new DataValueByteArray(await connection.ReadBytes(address, ((DataValueByteArray) value).Value.Length).ConfigureAwait(false));
            default:                 throw new Exception("Value contains an invalid data type");
        }
    }

    /// <summary>
    /// Writes a data value to the given address
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <param name="address">The address to write the value at</param>
    /// <param name="dt"></param>
    /// <param name="value"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static async Task WriteDataValue(IConsoleConnection connection, uint address, IDataValue value, bool appendNullCharForString = true) {
        switch (value.DataType) {
            case DataType.Byte:   await connection.WriteByte(address, ((DataValueByte) value).Value).ConfigureAwait(false); break;
            case DataType.Int16:  await connection.WriteValue(address, ((DataValueInt16) value).Value).ConfigureAwait(false); break;
            case DataType.Int32:  await connection.WriteValue(address, ((DataValueInt32) value).Value).ConfigureAwait(false); break;
            case DataType.Int64:  await connection.WriteValue(address, ((DataValueInt64) value).Value).ConfigureAwait(false); break;
            case DataType.Float:  await connection.WriteValue(address, ((DataValueFloat) value).Value).ConfigureAwait(false); break;
            case DataType.Double: await connection.WriteValue(address, ((DataValueDouble) value).Value).ConfigureAwait(false); break;
            case DataType.String: {
                byte[] array = new byte[value.ByteCount + (appendNullCharForString ? 1 : 0)];
                value.GetBytes(array);
                await connection.WriteBytes(address, array).ConfigureAwait(false);
                break;
            }
            case DataType.ByteArray: await connection.WriteBytes(address, ((DataValueByteArray) value).Value).ConfigureAwait(false); break;
            default:                 throw new InvalidOperationException("Data value's data type is invalid: " + value.DataType);
        }
    }

    // A helper class deriving TCS that allows an external cancellation signal to mark it as completed
    private class CancellableTaskCompletionSource : TaskCompletionSource, IDisposable {
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
        public volatile MemoryEngine? myEngine;
#if DEBUG
        public readonly string? stackTrace; // debugging stack trace, just in case the app locks up then the source is likely in here 
#endif

        public BusyToken(MemoryEngine engine) {
            Debug.Assert(engine.activeToken == null, "Active token already non-null");
            this.myEngine = engine;
            engine.OnTokenCreatedUnderLock();
#if DEBUG
            this.stackTrace = new StackTrace(true).ToString();
#endif
        }

        public void Dispose() {
            // we're being omega thread safe here
            MemoryEngine? engine = Interlocked.Exchange(ref this.myEngine, null);
            if (engine == null) {
                return;
            }

            lock (engine.connectionLock) {
                Debug.Assert(engine.activeToken == this, "Different active token references");
                engine.OnTokenDisposedUnderLock();
            }
        }
    }
}

public enum NumericDisplayType {
    /// <summary>
    /// The normal representation for the data types. Integers (except byte) are signed,
    /// and floating point numbers are displayed as decimal numbers
    /// </summary>
    Normal,

    /// <summary>
    /// Displays integers as unsigned. Does not affect floating point types nor byte (since it's always unsigned)
    /// </summary>
    Unsigned,

    /// <summary>
    /// Displays numbers as hexadecimal. Floating point numbers have their binary data reinterpreted as an
    /// integer and that is shown as hexadecimal (<see cref="BitConverter.Int32BitsToSingle"/>)
    /// </summary>
    Hexadecimal,
}

public static class NumericDisplayTypeExtensions {
    /// <summary>
    /// Displays the value as a string using the given numeric display type. Is it assumed that integer values are signed except for <see cref="byte"/>
    /// </summary>
    /// <param name="dt">The numeric display type</param>
    /// <param name="type">The data type, for performance reasons</param>
    /// <param name="value">The value. Must be byte, short, int, long, float, double or string. Only byte can be an unsigned integer type</param>
    /// <returns>The value as a string</returns>
    /// <exception cref="ArgumentOutOfRangeException">Unknown data type</exception>
    public static string AsString(this NumericDisplayType dt, DataType type, object value) {
        bool hex = dt == NumericDisplayType.Hexadecimal, unsigned = dt == NumericDisplayType.Unsigned;
        switch (type) {
            case DataType.Byte:      return hex ? ((byte) value).ToString("X2") : value.ToString()!;
            case DataType.Int16:     return hex ? ((short) value).ToString("X4") : (unsigned ? ((ushort) (short) value).ToString() : value.ToString()!);
            case DataType.Int32:     return hex ? ((int) value).ToString("X8") : (unsigned ? ((uint) (int) value).ToString() : value.ToString()!);
            case DataType.Int64:     return hex ? ((long) value).ToString("X16") : (unsigned ? ((ulong) (long) value).ToString() : value.ToString()!);
            case DataType.Float:     return hex ? BitConverter.SingleToUInt32Bits((float) value).ToString("X4") : ((float) value).ToString("F6");
            case DataType.Double:    return hex ? BitConverter.DoubleToUInt64Bits((double) value).ToString("X8") : ((double) value).ToString("F12");
            case DataType.String:    return value.ToString()!;
            case DataType.ByteArray: return NumberUtils.BytesToHexAscii((byte[]) value);
            default:                 throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}