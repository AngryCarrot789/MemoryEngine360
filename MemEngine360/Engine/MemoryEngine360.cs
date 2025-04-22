using System.Globalization;
using MemEngine360.Connections;
using MemEngine360.Engine.Modes;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Engine;

public delegate void MemoryEngine360EventHandler(MemoryEngine360 sender);

public delegate void MemoryEngine360ConnectionChangedEventHandler(MemoryEngine360 sender, IConsoleConnection? oldConnection, IConsoleConnection? newConnection);

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
        set {
            IConsoleConnection? oldConnection = this.connection;
            if (oldConnection == value) {
                return;
            }

            if (oldConnection != null && this.IsConnectionBusy) {
                throw new InvalidOperationException("Cannot change connection because we are currently busy");
            }
            
            this.connection = value;
            this.ConnectionChanged?.Invoke(this, oldConnection, value);
        }
    }

    /// <summary>
    /// Gets or sets if the memory engine is currently busy, e.g. reading or writing data.
    /// </summary>
    public bool IsConnectionBusy => this.isBusyCount > 0;

    public ScanningProcessor ScanningProcessor { get; }

    public event MemoryEngine360ConnectionChangedEventHandler? ConnectionChanged;
    
    /// <summary>
    /// Fired when the <see cref="IsConnectionBusy"/> state changes. It is crucial that no 'busy' operations
    /// are performed in the event handlers, otherwise, a deadlock could occur
    /// </summary>
    public event MemoryEngine360EventHandler? IsBusyChanged;

    public MemoryEngine360() {
        this.ScanningProcessor = new ScanningProcessor(this);
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
            case DataType.Int16:  obj = await connection.ReadInt16(address); break;
            case DataType.Int32:  obj = await connection.ReadInt32(address); break;
            case DataType.Int64:  obj = await connection.ReadInt64(address); break;
            case DataType.Float:  obj = await connection.ReadFloat(address); break;
            case DataType.Double: obj = await connection.ReadDouble(address); break;
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

    public static Task WriteAsText(IConsoleConnection connection, uint address, DataType type, NumericDisplayType displayType, string value, uint stringLength) {
        NumberStyles style = displayType == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        switch (type) {
            case DataType.Byte:   return connection.WriteByte(address, byte.Parse(value, style, null)); break;
            case DataType.Int16:  return connection.WriteInt16(address, short.Parse(value, style, null)); break;
            case DataType.Int32:  return connection.WriteInt32(address, int.Parse(value, style, null)); break;
            case DataType.Int64:  return connection.WriteInt64(address, long.Parse(value, style, null)); break;
            case DataType.Float:  return connection.WriteFloat(address, displayType == NumericDisplayType.Hexadecimal ? BitConverter.Int32BitsToSingle(int.Parse(value, style, null)) : float.Parse(value, style, null)); break;
            case DataType.Double: return connection.WriteDouble(address, displayType == NumericDisplayType.Hexadecimal ? BitConverter.Int64BitsToDouble(long.Parse(value, style, null)) : double.Parse(value, style, null)); break;
            case DataType.String: return connection.WriteString(address, value.Substring(0, Math.Min(Math.Max((int) stringLength, 0), value.Length))); break;
            default:              throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private class BusyToken : IDisposable {
        private readonly MemoryEngine360 engine;

        public BusyToken(MemoryEngine360 engine) {
            this.engine = engine;
            if (Interlocked.Increment(ref this.engine.isBusyCount) == 1) {
                this.engine.IsBusyChanged?.Invoke(this.engine);
            }
        }

        public void Dispose() {
            lock (this.engine.busyCounterLock) {
                if (Interlocked.Decrement(ref this.engine.isBusyCount) == 0) {
                    this.engine.IsBusyChanged?.Invoke(this.engine);
                }   
            }
        }
    }
}