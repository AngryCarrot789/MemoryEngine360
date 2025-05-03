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
using System.Globalization;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Engine.Modes;
using PFXToolKitUI;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine;

public delegate void MemoryEngine360EventHandler(MemoryEngine360 sender);

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

    /// <summary>
    /// Gets or sets if the memory engine is in the process of shutting down. Prevents scanning working
    /// </summary>
    public bool IsShuttingDown { get; set; }
    
    /// <summary>
    /// Fired when <see cref="Connection"/> changes. It is crucial that no 'busy' operations
    /// are performed in the event handlers, otherwise, a deadlock could occur
    /// </summary>
    public event MemoryEngine360ConnectionChangedEventHandler? ConnectionChanged;

    /// <summary>
    /// Fired when the <see cref="IsConnectionBusy"/> state changes. It is crucial that no 'busy' operations
    /// are performed in the event handlers, otherwise, a deadlock could occur
    /// </summary>
    public event MemoryEngine360EventHandler? IsBusyChanged;

    public MemoryEngine360() {
        this.ScanningProcessor = new ScanningProcessor(this);
        Task.Run(async () => {
            long timeSinceRefreshedAddresses = DateTime.Now.Ticks;
            BasicApplicationConfiguration cfg = BasicApplicationConfiguration.Instance;
            while (true) {
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
                long ticksNow = DateTime.Now.Ticks;
                if ((ticksNow - timeSinceRefreshedAddresses) >= (cfg.RefreshRateMillis * Time.TICK_PER_MILLIS)) {
                    this.ScanningProcessor.RefreshSavedAddressesLater();
                    timeSinceRefreshedAddresses = ticksNow;
                }
            }
        });
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
    /// Sets the current connection, with the given cause. Must be called on main thread
    /// </summary>
    /// <param name="token">The busy operation token that is valid</param>
    /// <param name="newConnection">The new connection object</param>
    /// <param name="cause">The cause for connection change</param>
    /// <exception cref="InvalidOperationException">Engine is busy</exception>
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
    /// <param name="sleepMilliseconds">The amount of milliseconds to wait inbetween calls to <see cref="BeginBusyOperation"/></param>
    /// <returns>The acquired token, or null if the task was cancelled</returns>
    /// <exception cref="TaskCanceledException">Operation was cancelled, didn't get token in time</exception>
    public async Task<IDisposable?> BeginBusyOperationAsync(CancellationToken cancellationToken, int sleepMilliseconds = 10) {
        IDisposable? token = this.BeginBusyOperation();
        while (token == null) {
            try {
                await Task.Delay(sleepMilliseconds, cancellationToken);
            }
            catch (OperationCanceledException) {
                return null;
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
    /// <param name="connection">The connection to read from</param>
    /// <param name="address">The absolute address of the value</param>
    /// <param name="type">The type of value to read</param>
    /// <param name="displayType">The display type for numeric data types</param>
    /// <param name="stringLength">The amount of chars to read for the string data type</param>
    /// <returns>A task representing the formatted value that was read</returns>
    /// <exception cref="ArgumentOutOfRangeException">Invalid data type</exception>
    public static async Task<string> ReadAsText(IConsoleConnection connection, uint address, DataType type, NumericDisplayType displayType, uint stringLength) {
        object obj;
        switch (type) {
            case DataType.Byte:   obj = await connection.ReadByte(address); break;
            case DataType.Int16:  obj = await connection.ReadValue<short>(address); break;
            case DataType.Int32:  obj = await connection.ReadValue<int>(address); break;
            case DataType.Int64:  obj = await connection.ReadValue<long>(address); break;
            case DataType.Float:  obj = await connection.ReadValue<float>(address); break;
            case DataType.Double: obj = await connection.ReadValue<double>(address); break;
            case DataType.String: obj = await connection.ReadString(address, stringLength); break;
            default:              throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        return displayType.AsString(type, obj);
    }

    public static async Task WriteAsText(IConsoleConnection connection, uint address, DataType type, NumericDisplayType displayType, string value, uint stringLength) {
        NumberStyles style = displayType == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        switch (type) {
            case DataType.Byte: {
                await connection.WriteByte(address, byte.Parse(value, style, null));
                break;
            }
            case DataType.Int16: {
                await connection.WriteValue(address, displayType == NumericDisplayType.Unsigned ? ushort.Parse(value, style, null) : (ushort) short.Parse(value, style, null));
                break;
            }
            case DataType.Int32: {
                await connection.WriteValue(address, displayType == NumericDisplayType.Unsigned ? uint.Parse(value, style, null) : (uint) int.Parse(value, style, null));
                break;
            }
            case DataType.Int64: {
                await connection.WriteValue(address, displayType == NumericDisplayType.Unsigned ? ulong.Parse(value, style, null) : (ulong) long.Parse(value, style, null));
                break;
            }
            case DataType.Float: {
                float f = displayType == NumericDisplayType.Hexadecimal ? BitConverter.Int32BitsToSingle(int.Parse(value, NumberStyles.HexNumber, null)) : float.Parse(value);
                await connection.WriteValue(address, f);
                break;
            }
            case DataType.Double: {
                double d = displayType == NumericDisplayType.Hexadecimal ? BitConverter.Int64BitsToDouble(long.Parse(value, NumberStyles.HexNumber, null)) : double.Parse(value);
                await connection.WriteValue(address, d);
                break;
            }
            case DataType.String: {
                await connection.WriteString(address, value.Substring(0, (int) Maths.Clamp(stringLength, 0, (uint) value.Length)));
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
                    engine!.IsBusyChanged?.Invoke(engine);
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
            }
        }
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
}

public enum NumericDisplayType {
    /// <summary>
    /// The normal representation for the data types. Integers (except byte) are signed,
    /// and floating point numbers are displayed as decimal numbers
    /// </summary>
    Normal,
    /// <summary>
    /// Displays integers as unsigned. Does not affect floating point types nor byte since it's always unsigned
    /// </summary>
    Unsigned,
    /// <summary>
    /// Displays integers as hexadecimal. Floating point numbers have their binary data reinterpreted as an
    /// integer and that is converted into hexadecimal (<see cref="BitConverter.Int32BitsToSingle"/>)
    /// </summary>
    Hexadecimal,
}

public static class NumericDisplayTypeExtensions {
    public static string AsString(this NumericDisplayType displayType, DataType type, object value) {
        switch (type) {
            case DataType.Byte:   return displayType == NumericDisplayType.Hexadecimal ? ((byte) value).ToString("X2") : value.ToString()!;
            case DataType.Int16:  return displayType == NumericDisplayType.Hexadecimal ? ((short) value).ToString("X4") : (displayType == NumericDisplayType.Unsigned ? ((ushort) (short) value).ToString() : value.ToString()!);
            case DataType.Int32:  return displayType == NumericDisplayType.Hexadecimal ? ((int) value).ToString("X8") : (displayType == NumericDisplayType.Unsigned ? ((uint) (int) value).ToString() : value.ToString()!);
            case DataType.Int64:  return displayType == NumericDisplayType.Hexadecimal ? ((long) value).ToString("X16") : (displayType == NumericDisplayType.Unsigned ? ((ulong) (long) value).ToString() : value.ToString()!);
            case DataType.Float:  return displayType == NumericDisplayType.Hexadecimal ? BitConverter.SingleToInt32Bits((float) value).ToString("X4") : value.ToString()!;
            case DataType.Double: return displayType == NumericDisplayType.Hexadecimal ? BitConverter.DoubleToInt64Bits((double) value).ToString("X8") : value.ToString()!;
            case DataType.String: return value.ToString()!;
            default:              throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}