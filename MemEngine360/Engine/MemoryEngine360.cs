using System.Diagnostics;
using System.Globalization;
using MemEngine360.Connections;
using MemEngine360.Engine.Modes;
using PFXToolKitUI;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Engine;

public delegate void MemoryEngine360EventHandler(MemoryEngine360 sender);
public delegate void MemoryEngine360ConnectionChangedEventHandler(MemoryEngine360 sender, IConsoleConnection? oldConnection, IConsoleConnection? newConnection, ConnectionChangeCause cause);

/// <summary>
/// The main manager class for a MemEngine360 window
/// </summary>
public class MemoryEngine360 {
    public static readonly DataKey<MemoryEngine360> DataKey = DataKey<MemoryEngine360>.Create("MemoryEngine360");

    private IConsoleConnection? connection;
    private readonly object busyCounterLock = new object();
    private volatile int isBusyCount;

    /// <summary>
    /// Gets or sets the current console connection
    /// </summary>
    public IConsoleConnection? Connection {
        get => this.connection;
        set => this.SetConnection(value, ConnectionChangeCause.Custom);
    }

    /// <summary>
    /// Gets or sets if the memory engine is currently busy, e.g. reading or writing data.
    /// </summary>
    public bool IsConnectionBusy => this.isBusyCount > 0;

    public ScanningProcessor ScanningProcessor { get; }

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
            while (true) {
                IConsoleConnection? conn = Volatile.Read(ref this.connection);
                if (conn != null && !conn.IsConnected) {
                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                        if (conn == this.Connection) {
                            this.CheckConnection();
                        }
                    });
                }
                
                this.ScanningProcessor.RefreshSavedAddresses();
                await Task.Delay(2000);
            }
        });
    }

    /// <summary>
    /// Sets the current connection, with the given cause. Must be called on main thread
    /// </summary>
    /// <param name="newConnection">The new connection object</param>
    /// <param name="cause">The cause for connection change</param>
    /// <exception cref="InvalidOperationException">Engine is busy</exception>
    /// <exception cref="ArgumentException">New connection is non-null when cause is <see cref="ConnectionChangeCause.LostConnection"/></exception>
    public void SetConnection(IConsoleConnection? newConnection, ConnectionChangeCause cause) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        
        IConsoleConnection? oldConnection = this.connection;
        if (oldConnection == newConnection) {
            return;
        }

        lock (this.busyCounterLock) {
            if (this.IsConnectionBusy) {
                throw new InvalidOperationException("Cannot change connection because we are currently busy");
            }

            if (cause == ConnectionChangeCause.LostConnection && newConnection != null) {
                throw new ArgumentException("Cause cannot be " + nameof(ConnectionChangeCause.LostConnection) + " when setting connection to a non-null value");
            }
            
            this.connection = newConnection;
            this.ConnectionChanged?.Invoke(this, oldConnection, newConnection, cause);   
        }
    }

    /// <summary>
    /// Waits for all busy operations to finish, then sets <see cref="Connection"/> to null
    /// with the cause. Must be called on main thread
    /// </summary>
    public async Task WaitAndDisconnectAsync(ConnectionChangeCause cause, CancellationToken token) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        
        token.ThrowIfCancellationRequested();
        
        // we take the token so that we can win the race in the cleanest way possible
        IDisposable? disposer = this.BeginBusyOperation();
        while (disposer == null) {
            await Task.Delay(500, token);
            disposer = this.BeginBusyOperation();
        }

        lock (this.busyCounterLock) {
            // release token, but since we lock before hand, no other thread can snatch more tokens
            disposer.Dispose();
            Debug.Assert(!this.IsConnectionBusy, "Expected " + nameof(this.IsConnectionBusy) + " to be false since we got the token and the lock???");
            
            this.SetConnection(null, cause);
        }
    }

    /// <summary>
    /// Begins a busy operation that uses the <see cref="Connection"/>. Dispose to finish the busy operation
    /// </summary>
    /// <returns></returns>
    public IDisposable? BeginBusyOperation() {
        if (this.connection == null)
            throw new InvalidOperationException("No connection is present. Cannot begin busy operation");

        lock (this.busyCounterLock) {
            return this.isBusyCount == 0 ? new BusyToken(this) : null;
        }
    }

    public enum NumericDisplayType {
        Normal,
        Unsigned,
        Hexadecimal,
    }

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

        switch (type) {
            case DataType.Byte:   return displayType == NumericDisplayType.Hexadecimal ? ((byte) obj).ToString("X") : obj.ToString()!;
            case DataType.Int16:  return displayType == NumericDisplayType.Hexadecimal ? ((short) obj).ToString("X2") : (displayType == NumericDisplayType.Unsigned ? ((ushort) (short) obj).ToString() : obj.ToString()!);
            case DataType.Int32:  return displayType == NumericDisplayType.Hexadecimal ? ((int) obj).ToString("X4") : (displayType == NumericDisplayType.Unsigned ? ((uint) (int) obj).ToString() : obj.ToString()!);
            case DataType.Int64:  return displayType == NumericDisplayType.Hexadecimal ? ((long) obj).ToString("X8") : (displayType == NumericDisplayType.Unsigned ? ((ulong) (long) obj).ToString() : obj.ToString()!);
            case DataType.Float:  return displayType == NumericDisplayType.Hexadecimal ? BitConverter.SingleToInt32Bits((float) obj).ToString("X4") : obj.ToString()!;
            case DataType.Double: return displayType == NumericDisplayType.Hexadecimal ? BitConverter.DoubleToInt64Bits((double) obj).ToString("X8") : obj.ToString()!;
            case DataType.String: return obj.ToString() ?? "";
            default:              throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public static ValueTask WriteAsText(IConsoleConnection connection, uint address, DataType type, NumericDisplayType displayType, string value, uint stringLength) {
        NumberStyles style = displayType == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        switch (type) {
            case DataType.Byte:   return connection.WriteByte(address, byte.Parse(value, style, null)); break;
            case DataType.Int16:
                short int16 = short.Parse(value, style, null);
                return connection.WriteValue(address, int16); break;
            case DataType.Int32:
                int int32 = int.Parse(value, style, null);
                return connection.WriteValue(address, int32); break;
            case DataType.Int64:
                long int64 = long.Parse(value, style, null);
                return connection.WriteValue(address, int64); break;
            case DataType.Float:
                float f = displayType == NumericDisplayType.Hexadecimal ? BitConverter.Int32BitsToSingle(int.Parse(value, style, null)) : float.Parse(value, style, null);
                return connection.WriteValue(address, f); break;
            case DataType.Double:
                double d = displayType == NumericDisplayType.Hexadecimal ? BitConverter.Int64BitsToDouble(long.Parse(value, style, null)) : double.Parse(value, style, null);
                return connection.WriteValue(address, d); break;
            case DataType.String: return connection.WriteString(address, value.Substring(0, Math.Min(Math.Max((int) stringLength, 0), value.Length))); break;
            default:              throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private class BusyToken : IDisposable {
        private MemoryEngine360? engine;

        public BusyToken(MemoryEngine360 engine) {
            this.engine = engine;
            if (Interlocked.Increment(ref this.engine.isBusyCount) == 1) {
                this.engine.IsBusyChanged?.Invoke(this.engine);
            }
        }

        public void Dispose() {
            if (this.engine == null) {
                return;
            }

            lock (this.engine.busyCounterLock) {
                if (Interlocked.Decrement(ref this.engine.isBusyCount) == 0) {
                    this.engine.IsBusyChanged?.Invoke(this.engine);
                }

                this.engine = null;
            }
        }
    }

    /// <summary>
    /// Returns true if the connection is valid and is still actually connected. Returns false if not.
    /// If false and connection is non-null, it will be set to null using the cause of <see cref="ConnectionChangeCause.LostConnection"/>
    /// </summary>
    /// <returns></returns>
    public bool CheckConnection() {
        if (this.connection == null)
            return false;
        
        if (this.connection.IsConnected)
            return true;
        
        if (!this.IsConnectionBusy)
            this.SetConnection(null, ConnectionChangeCause.LostConnection);

        return false;
    }
}