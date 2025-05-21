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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.Sequencing;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine;

public delegate void MemoryEngine360EventHandler(MemoryEngine360 sender);

public delegate Task MemoryEngine360ConnectionChangingEventHandler(MemoryEngine360 sender, IActivityProgress progress);

public delegate void MemoryEngine360ConnectionChangedEventHandler(MemoryEngine360 sender, IConsoleConnection? oldConnection, IConsoleConnection? newConnection, ConnectionChangeCause cause);

/// <summary>
/// The main manager class for a MemEngine360 window
/// </summary>
public class MemoryEngine360 {
    public static readonly DataKey<MemoryEngine360> DataKey = DataKey<MemoryEngine360>.Create("MemoryEngine360");

    private volatile IConsoleConnection? connection; // our connection object -- volatile in case JIT plays dirty tricks, i ain't no expert in wtf volatile does though
    private readonly object connectionLock = new object(); // used to synchronize busy token creation and also connection change
    private volatile int isBusyCount; // this is a boolean. 0 = no token, 1 = token acquired. any other value is invalid
    private BusyToken? activeToken;
    private bool isShuttingDown;
    private bool? isForcedLittleEndian;

    private readonly LinkedList<CancellableTaskCompletionSource> busyLockAsyncWaiters = new LinkedList<CancellableTaskCompletionSource>();

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
    /// Gets or sets if the memory engine is currently busy, e.g. reading or writing data.
    /// This will never be true when <see cref="Connection"/> is null
    /// </summary>
    public bool IsConnectionBusy => this.isBusyCount > 0;

    public ScanningProcessor ScanningProcessor { get; }

    public TaskSequencerManager TaskSequencerManager { get; }

    public AddressTableManager AddressTableManager { get; }

    /// <summary>
    /// Gets or sets if the memory engine is in the process of shutting down. Prevents scanning working
    /// </summary>
    public bool IsShuttingDown {
        get => this.isShuttingDown;
        set {
            if (this.isShuttingDown != value) {
                this.isShuttingDown = value;
                this.IsShuttingDownChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether byte order of the search value and displayed values is treated as little endian (true) or big endian (false), or
    /// automatic (null) to use the current connection's endianness (see <see cref="IConsoleConnection.IsLittleEndian"/>).
    /// <para>
    /// This only affects the scanning engine, scan results' values and saved addresses' values
    /// </para>
    /// </summary>
    public bool? IsForcedLittleEndian {
        get => this.isForcedLittleEndian;
        set {
            if (this.isForcedLittleEndian != value) {
                this.isForcedLittleEndian = value;
                this.IsForcedLittleEndianChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// An event fired when a connection is most likely about to change. This can be used by custom activities
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
    /// potential timeout/IO exceptions from inconveniencing the user
    /// </para>
    /// </summary>
    public event MemoryEngine360ConnectionChangingEventHandler? ConnectionAboutToChange;

    /// <summary>
    /// An event fired when <see cref="IsShuttingDown"/> changes. Ideally this should only be fired once per instance of <see cref="MemoryEngine360"/>
    /// </summary>
    public event MemoryEngine360EventHandler? IsShuttingDownChanged;

    /// <summary>
    /// Fired when <see cref="Connection"/> changes. It is crucial that no 'busy' operations
    /// are performed in the event handlers, otherwise, a deadlock could occur
    /// </summary>
    public event MemoryEngine360ConnectionChangedEventHandler? ConnectionChanged;

    /// <summary>
    /// Fired when the <see cref="IsConnectionBusy"/> state changes. It is crucial that no 'busy' operations
    /// are performed in the event handlers, otherwise, a deadlock could occur. It's also important that exceptions
    /// are not thrown in the handlers, because they will be swallowed and never see the light of day
    /// </summary>
    public event MemoryEngine360EventHandler? IsBusyChanged;

    public event MemoryEngine360EventHandler? IsForcedLittleEndianChanged;
    
    public MemoryEngine360() {
        this.ScanningProcessor = new ScanningProcessor(this);
        this.AddressTableManager = new AddressTableManager(this);
        this.TaskSequencerManager = new TaskSequencerManager(this);
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
    /// Returns our forced endianness state, or the connection's <see cref="IConsoleConnection.IsLittleEndian"/> state when automatic
    /// </summary>
    /// <param name="conn">The connection</param>
    /// <returns>True to use little endian byte ordering, false to use big endian</returns>
    public bool IsLittleEndianHelper(IConsoleConnection conn) => this.IsForcedLittleEndian ?? conn.IsLittleEndian;

    /// <summary>
    /// Returns whether things like integers, floats, etc. should have their endianness reversed again when the
    /// data has already been reversed by the <see cref="IConsoleConnection"/>. This is not needed when reading
    /// chunks of bytes, since they do not necessarily represent a data type, so byte order doesn't matter
    /// </summary>
    /// <param name="conn"></param>
    /// <returns></returns>
    public bool ShouldReverseEndianness(IConsoleConnection conn) {
        // When we force an endianness, reverse when they don't match
        if (this.IsForcedLittleEndian is bool isLittleEndian) {
            // e.g. say connection is xbox (big endian), if we want to show as LE,
            // (true != false) == true, so data must be flipped
            return isLittleEndian != conn.IsLittleEndian;
        }
        
        // no forced endianness, so just leave everything as is. The connection will
        // correct the endianness for data values automatically
        return false;
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
    /// This method will not throw any exceptions encountered during the event handlers, not even <see cref="OperationCanceledException"/>
    /// </summary>
    public async Task BroadcastConnectionAboutToChange(IActivityProgress progress) {
        Delegate[]? list = this.ConnectionAboutToChange?.GetInvocationList();
        if (list != null) {
            Task[] tasks = new Task[list.Length];
            for (int i = 0; i < list.Length; i++) {
                MemoryEngine360ConnectionChangingEventHandler handler = (MemoryEngine360ConnectionChangingEventHandler) list[i];
                tasks[i] = Task.Run(async () => {
                    try {
                        await handler(this, progress);
                    }
                    catch (OperationCanceledException) {
                        // ignored
                    }
                    catch (Exception ex) {
                        ApplicationPFX.Instance.Dispatcher.Post(() => throw ex);
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
    /// <param name="newConnection">The new connection object</param>
    /// <param name="cause">The cause for connection change</param>
    /// <exception cref="InvalidOperationException">Token is invalid</exception>
    /// <exception cref="ArgumentException">New connection is non-null when cause is <see cref="ConnectionChangeCause.LostConnection"/></exception>
    public void SetConnection(IDisposable busyToken, IConsoleConnection? newConnection, ConnectionChangeCause cause) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        this.ValidateToken(busyToken);
        if (cause == ConnectionChangeCause.LostConnection && newConnection != null) {
            throw new ArgumentException("Cause cannot be " + nameof(ConnectionChangeCause.LostConnection) + " when setting connection to a non-null value");
        }

        // ConnectionChanged is invoked under the lock to enforce busy operation rules
        lock (this.connectionLock) {
            // we don't necessarily need to access connection under lock since if we have
            // a valid busy token then nothing can modify it, but better be safe than sorry
            IConsoleConnection? oldConnection = this.connection;
            if (ReferenceEquals(oldConnection, newConnection)) {
                throw new ArgumentException("Cannot set the connection to the same value");
            }

            this.connection = newConnection;
            this.ConnectionChanged?.Invoke(this, oldConnection, newConnection, cause);
        }
    }

    /// <summary>
    /// Convenience method to wait for all busy operations to finish, then
    /// sets <see cref="Connection"/> to null with the cause. Must be called on main thread
    /// </summary>
    /// <exception cref="TaskCanceledException">
    /// Operation cancelled. Only thrown before any modification occurs; once token is gotten, this will not be thrown
    /// </exception>
    public async Task WaitAndDisconnectAsync(ConnectionChangeCause cause, CancellationToken token) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();

        token.ThrowIfCancellationRequested();

        // post connection changing before obtaining busy token, because background
        // activites may be running and may have the token
        await this.BroadcastConnectionAboutToChange(EmptyActivityProgress.Instance);

        // we take the token so that we can win the race in the cleanest way possible
        using IDisposable? busyToken = await this.BeginBusyOperationAsync(token);
        if (busyToken == null) {
            return; // cancelled or disconnected
        }

        lock (this.connectionLock) {
            this.SetConnection(busyToken, null, cause);
        }
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

            if (this.isBusyCount == 0)
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
                try {
                    await Task.Delay(10, cancellationToken);
                }
                catch (OperationCanceledException) {
                    return null;
                }
            }
            else {
                try {
                    await tcs.Value.Task;
                }
                catch (OperationCanceledException) {
                    // We only need to remove on cancelled, because when it
                    // completes normally, the list gets cleared anyway
                    lock (this.connectionLock) {
                        tcs.List!.Remove(tcs);
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

    /// <summary>
    /// Reads a specific data type from the console, and formats it according to the display type.
    /// When reading strings, the <see cref="stringLength"/> parameter is how many chars to read
    /// </summary>
    /// <param name="conn">The connection to read from</param>
    /// <param name="address">The absolute address of the value</param>
    /// <param name="type">The type of value to read</param>
    /// <param name="displayType">The display type for numeric data types</param>
    /// <param name="stringLength">The amount of chars to read for the string data type</param>
    /// <param name="forceLittleEndian">The forced endianness state</param>
    /// <returns>A task representing the formatted value that was read</returns>
    /// <exception cref="ArgumentOutOfRangeException">Invalid data type</exception>
    public static async Task<string> ReadAsText(IConsoleConnection conn, uint address, DataType type, NumericDisplayType displayType, uint stringLength, bool? forceLittleEndian = null) {
        object obj;
        switch (type) {
            case DataType.Byte:   obj = await conn.ReadByte(address).ConfigureAwait(false); break;
            case DataType.Int16:  obj = CorrectEndianness(await conn.ReadValue<short>(address).ConfigureAwait(false), conn, forceLittleEndian); break;
            case DataType.Int32:  obj = CorrectEndianness(await conn.ReadValue<int>(address).ConfigureAwait(false), conn, forceLittleEndian); break;
            case DataType.Int64:  obj = CorrectEndianness(await conn.ReadValue<long>(address).ConfigureAwait(false), conn, forceLittleEndian); break;
            case DataType.Float:  obj = CorrectEndianness(await conn.ReadValue<float>(address).ConfigureAwait(false), conn, forceLittleEndian); break;
            case DataType.Double: obj = CorrectEndianness(await conn.ReadValue<double>(address).ConfigureAwait(false), conn, forceLittleEndian); break;
            case DataType.String: obj = await conn.ReadString(address, stringLength).ConfigureAwait(false); break;
            default:              throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        return displayType.AsString(type, obj);
    }

    private static T CorrectEndianness<T>(T input, IConsoleConnection connection, bool? isLittleEndian) where T : unmanaged {
        if (isLittleEndian.HasValue && connection.IsLittleEndian != isLittleEndian.Value) {
            byte[] buffer = new byte[Unsafe.SizeOf<T>()]; // allocate value on heap
            ref byte refBuf = ref MemoryMarshal.GetArrayDataReference(buffer); // get ref to elem 0, same as ref buffer[0] but cooler
            Unsafe.As<byte, T>(ref refBuf) = input; // write input to buffer
            Array.Reverse(buffer); // reverse bytes
            input = Unsafe.As<byte, T>(ref refBuf); // write buffer to input
        }

        return input;
    }

    public static async Task WriteAsText(IConsoleConnection connection, uint address, DataType type, NumericDisplayType displayType, string value, uint stringLength, bool? isForcedLittleEndian = null) {
        NumberStyles style = displayType == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        switch (type) {
            case DataType.Byte: {
                await connection.WriteByte(address, byte.Parse(value, style, null)).ConfigureAwait(false);
                break;
            }
            case DataType.Int16: {
                await connection.WriteValue(address, displayType == NumericDisplayType.Unsigned ? CorrectEndianness(ushort.Parse(value, style, null), connection, isForcedLittleEndian) : CorrectEndianness((ushort) short.Parse(value, style, null), connection, isForcedLittleEndian)).ConfigureAwait(false);
                break;
            }
            case DataType.Int32: {
                await connection.WriteValue(address, displayType == NumericDisplayType.Unsigned ? CorrectEndianness(uint.Parse(value, style, null), connection, isForcedLittleEndian) : CorrectEndianness((uint) int.Parse(value, style, null), connection, isForcedLittleEndian)).ConfigureAwait(false);
                break;
            }
            case DataType.Int64: {
                await connection.WriteValue(address, displayType == NumericDisplayType.Unsigned ? CorrectEndianness(ulong.Parse(value, style, null), connection, isForcedLittleEndian) : CorrectEndianness((ulong) long.Parse(value, style, null), connection, isForcedLittleEndian)).ConfigureAwait(false);
                break;
            }
            case DataType.Float: {
                float f = displayType == NumericDisplayType.Hexadecimal ? BitConverter.UInt32BitsToSingle(CorrectEndianness(uint.Parse(value, NumberStyles.HexNumber, null), connection, isForcedLittleEndian)) : CorrectEndianness(float.Parse(value), connection, isForcedLittleEndian);
                await connection.WriteValue(address, f).ConfigureAwait(false);
                break;
            }
            case DataType.Double: {
                double d = displayType == NumericDisplayType.Hexadecimal ? BitConverter.UInt64BitsToDouble(CorrectEndianness(ulong.Parse(value, NumberStyles.HexNumber, null), connection, isForcedLittleEndian)) : CorrectEndianness(double.Parse(value), connection, isForcedLittleEndian);
                await connection.WriteValue(address, d).ConfigureAwait(false);
                break;
            }
            case DataType.String: {
                await connection.WriteString(address, value.Substring(0, (int) Maths.Clamp(stringLength, 0, (uint) value.Length))).ConfigureAwait(false);
                break;
            }
            default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private void ValidateToken(IDisposable token) {
        if (!(token is BusyToken busy))
            throw new InvalidOperationException("Token is invalid");

        // since myEngine can be atomically exchanged, we read it first
        MemoryEngine360? engine = busy.myEngine;
        if (engine == null)
            throw new InvalidOperationException("Token has already been disposed");
        if (engine != this)
            throw new InvalidOperationException("Token is not associated with this engine");
    }

    private class BusyToken : IDisposable {
        public volatile MemoryEngine360? myEngine;
#if DEBUG
        public readonly string? stackTrace;
#endif

        public BusyToken(MemoryEngine360 engine) {
            this.myEngine = engine;
            if (Interlocked.Increment(ref engine.isBusyCount) == 1) {
                try {
                    engine.IsBusyChanged?.Invoke(engine);
                }
                catch {
                    Debugger.Break(); // exceptions are swallowed because it's the user's fault for not catching errors :D
                }
            }

#if DEBUG
            this.stackTrace = new StackTrace().ToString();
#endif
        }

        public void Dispose() {
            // we're being omega thread safe here
            MemoryEngine360? engine = Interlocked.Exchange(ref this.myEngine, null);
            if (engine == null) {
                return;
            }

            lock (engine.connectionLock) {
                Debug.Assert(engine.activeToken == this, "Different active token references");

                engine.activeToken = null;
                if (Interlocked.Decrement(ref engine.isBusyCount) == 0) {
                    try {
                        engine.IsBusyChanged?.Invoke(engine);
                    }
                    catch {
                        Debugger.Break(); // exceptions are swallowed because it's the user's fault for not catching errors :D
                    }
                }

                engine.OnTokenDisposedUnderLock();
            }
        }
    }

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
            this.registration.Dispose();
        }
    }

    private LinkedListNode<CancellableTaskCompletionSource>? EnqueueAsyncWaiter(CancellationToken token) {
        bool lockTaken = false;
        try {
            Monitor.TryEnter(this.connectionLock, 0, ref lockTaken);
            if (!lockTaken)
                return null;

            return this.busyLockAsyncWaiters.AddLast(new CancellableTaskCompletionSource(token));
        }
        finally {
            if (lockTaken)
                Monitor.Exit(this.connectionLock);
        }
    }

    private void OnTokenDisposedUnderLock() {
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

    public void CheckConnection() {
        // CheckConnection is just a helpful method to clear connection if it's 
        // disconnected internally, therefore, we don't need over the top synchronization,
        // because any code that actually tries to read/write will be async and can handle
        // the timeout exceptions
        IConsoleConnection? conn = this.connection;
        if (conn == null || conn.IsConnected) {
            return;
        }

        using (IDisposable? token1 = this.BeginBusyOperation()) {
            if (token1 != null && this.TryDisconnectInternal(token1)) {
                return;
            }
        }

        ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
            using IDisposable? token2 = this.BeginBusyOperation();
            if (token2 != null)
                this.TryDisconnectInternal(token2);
        }, DispatchPriority.Background);
    }

    private bool TryDisconnectInternal(IDisposable token) {
        IConsoleConnection? conn = this.connection;
        if (conn != null && !conn.IsConnected) {
            this.SetConnection(token, null, ConnectionChangeCause.LostConnection);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Validation args version of <see cref="CanParseTextAsNumber(PFXToolKitUI.Services.UserInputs.ValidationArgs,MemEngine360.Engine.Modes.DataType,MemEngine360.Engine.NumericDisplayType)"/>.
    /// Adds an error saying "Invalid (data type)" the the errors list
    /// </summary>
    /// <param name="args">The args containing the input text</param>
    /// <param name="dataType">The data type to try to parse to</param>
    /// <param name="ndt">The format/display type of the input string</param>
    /// <returns>True when parsed successfully, False when failed</returns>
    /// <exception cref="ArgumentOutOfRangeException">Data type is not numeric</exception>
    public static bool CanParseTextAsNumber(ValidationArgs args, DataType dataType, NumericDisplayType ndt) {
        if (CanParseTextAsNumber(args.Input, dataType, ndt)) {
            return true;
        }

        switch (dataType) {
            case DataType.Byte:   args.Errors.Add("Invalid Byte"); break;
            case DataType.Int16:  args.Errors.Add("Invalid " + (ndt == NumericDisplayType.Unsigned ? "UInt16" : "Int16")); break;
            case DataType.Int32:  args.Errors.Add("Invalid " + (ndt == NumericDisplayType.Unsigned ? "UInt32" : "Int32")); break;
            case DataType.Int64:  args.Errors.Add("Invalid " + (ndt == NumericDisplayType.Unsigned ? "UInt64" : "Int64")); break;
            case DataType.Float:  args.Errors.Add("Invalid float"); break;
            case DataType.Double: args.Errors.Add("Invalid double"); break;
            default:              throw new ArgumentOutOfRangeException();
        }

        return false;
    }

    /// <summary>
    /// Attempts to parse the input as a numeric data type
    /// </summary>
    /// <param name="input">The input string</param>
    /// <param name="dataType">The data type to try to parse to</param>
    /// <param name="ndt">The format/display type of the input string</param>
    /// <returns>True when parsed successfully, False when failed</returns>
    /// <exception cref="ArgumentOutOfRangeException">Data type is not numeric</exception>
    public static bool CanParseTextAsNumber(string input, DataType dataType, NumericDisplayType ndt) {
        NumberStyles nsInt = ndt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        switch (dataType) {
            case DataType.Byte: {
                if (!byte.TryParse(input, nsInt, null, out _))
                    return false;
                break;
            }
            case DataType.Int16: {
                if (!(ndt == NumericDisplayType.Unsigned ? ushort.TryParse(input, nsInt, null, out _) : short.TryParse(input, nsInt, null, out _)))
                    return false;
                break;
            }
            case DataType.Int32: {
                if (!(ndt == NumericDisplayType.Unsigned ? uint.TryParse(input, nsInt, null, out _) : int.TryParse(input, nsInt, null, out _)))
                    return false;
                break;
            }
            case DataType.Int64: {
                if (!(ndt == NumericDisplayType.Unsigned ? ulong.TryParse(input, nsInt, null, out _) : long.TryParse(input, nsInt, null, out _)))
                    return false;
                break;
            }
            case DataType.Float: {
                if (!(ndt == NumericDisplayType.Hexadecimal ? uint.TryParse(input, NumberStyles.HexNumber, null, out _) : float.TryParse(input, out _)))
                    return false;
                break;
            }
            case DataType.Double: {
                if (!(ndt == NumericDisplayType.Hexadecimal ? ulong.TryParse(input, NumberStyles.HexNumber, null, out _) : double.TryParse(input, out _)))
                    return false;
                break;
            }
            default: throw new ArgumentOutOfRangeException();
        }

        return true;
    }

    public static bool TryParseTextAsDataValue(ValidationArgs args, DataType dataType, NumericDisplayType ndt, StringType stringType, [NotNullWhen(true)] out IDataValue? value) {
        return (value = TryParseTextAsDataValue(args, dataType, ndt, stringType)) != null;
    }

    private static void AddErrorMessage<T>(ValidationArgs args, bool isUnsigned, NumberStyles numberStyles) where T : INumber<T>, IMinMaxValue<T> {
        if (isUnsigned && args.Input.TrimStart().StartsWith('-')) {
            args.Errors.Add($"{typeof(T).Name} cannot be negative. Range is {T.MinValue}-{T.MaxValue}");
        }
        else if (typeof(T) != typeof(ulong) && ulong.TryParse(args.Input, numberStyles, null, out _)) {
            args.Errors.Add($"Text is too big for {typeof(T).Name}. Range is {T.MinValue}-{T.MaxValue}");
        }
        else {
            args.Errors.Add("Text is not numeric");
        }
    }

    private static IDataValue? TryParseTextAsDataValue(ValidationArgs args, DataType dataType, NumericDisplayType ndt, StringType stringType) {
        NumberStyles nsInt = ndt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        switch (dataType) {
            case DataType.Byte: {
                if (byte.TryParse(args.Input, nsInt, null, out byte val))
                    return new DataValueByte(val);
                AddErrorMessage<byte>(args, true, nsInt);
                break;
            }
            case DataType.Int16:
            case DataType.Int32:
            case DataType.Int64: {
                if (ndt == NumericDisplayType.Unsigned) {
                    switch (dataType) {
                        case DataType.Int16: {
                            if (ushort.TryParse(args.Input, nsInt, null, out ushort val))
                                return new DataValueInt16((short) val);
                            AddErrorMessage<ushort>(args, true, nsInt);
                            break;
                        }
                        case DataType.Int32: {
                            if (uint.TryParse(args.Input, nsInt, null, out uint val))
                                return new DataValueInt32((int) val);
                            AddErrorMessage<uint>(args, true, nsInt);
                            break;
                        }
                        case DataType.Int64: {
                            if (ulong.TryParse(args.Input, nsInt, null, out ulong val))
                                return new DataValueInt64((long) val);
                            AddErrorMessage<ulong>(args, true, nsInt);
                            break;
                        }
                        default: throw new Exception("Memory corruption");
                    }
                }
                else {
                    switch (dataType) {
                        case DataType.Int16: {
                            if (short.TryParse(args.Input, nsInt, null, out short val))
                                return new DataValueInt16(val);
                            AddErrorMessage<short>(args, false, nsInt);
                            break;
                        }
                        case DataType.Int32: {
                            if (int.TryParse(args.Input, nsInt, null, out int val))
                                return new DataValueInt32(val);
                            AddErrorMessage<int>(args, false, nsInt);
                            break;
                        }
                        case DataType.Int64: {
                            if (long.TryParse(args.Input, nsInt, null, out long val))
                                return new DataValueInt64(val);
                            AddErrorMessage<long>(args, false, nsInt);
                            break;
                        }
                        default: throw new Exception("Memory corruption");
                    }
                }

                break;
            }
            case DataType.Float: {
                if (ndt == NumericDisplayType.Hexadecimal) {
                    if (uint.TryParse(args.Input, NumberStyles.HexNumber, null, out uint val)) {
                        return new DataValueFloat(Unsafe.As<uint, float>(ref val));
                    }

                    args.Errors.Add("Invalid unsigned integer as the float bits");
                }
                else if (float.TryParse(args.Input, out float val)) {
                    return new DataValueFloat(val);
                }

                args.Errors.Add("Invalid float/single");
                break;
            }
            case DataType.Double: {
                if (ndt == NumericDisplayType.Hexadecimal) {
                    if (ulong.TryParse(args.Input, NumberStyles.HexNumber, null, out ulong val)) {
                        return new DataValueDouble(Unsafe.As<ulong, double>(ref val));
                    }

                    args.Errors.Add("Invalid unsigned long as the double bits");
                }
                else if (double.TryParse(args.Input, out double val)) {
                    return new DataValueDouble(val);
                }

                args.Errors.Add("Invalid double");
                break;
            }
            case DataType.String: return new DataValueString(args.Input, stringType);
            default:              throw new ArgumentOutOfRangeException();
        }

        return null;
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
    /// Displays the value as a string using the given numeric display type
    /// </summary>
    /// <param name="dt">The numeric display type</param>
    /// <param name="type">The data type, for performance reasons</param>
    /// <param name="value">The value. Must be byte, short, int, long, float, double or string. Only byte can be an unsigned integer type</param>
    /// <returns>The value as a string</returns>
    /// <exception cref="ArgumentOutOfRangeException">Unknown data type</exception>
    public static string AsString(this NumericDisplayType dt, DataType type, object value) {
        bool hex = dt == NumericDisplayType.Hexadecimal, unsigned = dt == NumericDisplayType.Unsigned;
        switch (type) {
            case DataType.Byte:   return hex ? ((byte) value).ToString("X2") : value.ToString()!;
            case DataType.Int16:  return hex ? ((short) value).ToString("X4") : (unsigned ? ((ushort) (short) value).ToString() : value.ToString()!);
            case DataType.Int32:  return hex ? ((int) value).ToString("X8") : (unsigned ? ((uint) (int) value).ToString() : value.ToString()!);
            case DataType.Int64:  return hex ? ((long) value).ToString("X16") : (unsigned ? ((ulong) (long) value).ToString() : value.ToString()!);
            case DataType.Float:  return hex ? BitConverter.SingleToUInt32Bits((float) value).ToString("X4") : value.ToString()!;
            case DataType.Double: return hex ? BitConverter.DoubleToUInt64Bits((double) value).ToString("X8") : value.ToString()!;
            case DataType.String: return value.ToString()!;
            default:              throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}