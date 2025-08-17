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

using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using MemEngine360.Connections.Features;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Services;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Connections;

/// <summary>
/// The base class for a console connection. This class implements the basic read/write value methods
/// </summary>
public abstract class BaseConsoleConnection : IConsoleConnection {
    protected readonly byte[] sharedByteArray8 = new byte[8];
    private readonly ServiceManager featureManager;
    private volatile int busyStack;
    private volatile int isClosedState;

    public abstract RegisteredConnectionType ConnectionType { get; }

    public bool IsClosed => this.isClosedState != 0;

    public abstract bool IsLittleEndian { get; }

    /// <summary>
    /// Gets the supported addressing range. Reading/writing outside this range will yield invalid data.
    /// <para>
    /// This value can change at any time and there's no way to tell when, so beware of that.
    /// </para>
    /// </summary>
    public abstract AddressRange AddressableRange { get; }

    public event ConsoleConnectionEventHandler? Closed;

    protected BaseConsoleConnection() {
        this.featureManager = new ServiceManager();
    }

    ~BaseConsoleConnection() {
        if (this.isClosedState == 0)
            AppLogger.Instance.WriteLine("Destructor called on " + nameof(BaseConsoleConnection) + " when still open");
    }
    
    public virtual bool TryGetFeature<T>([NotNullWhen(true)] out T? feature) where T : class, IConsoleFeature {
        return this.featureManager.TryGetService(out feature);
    }

    public virtual bool HasFeature<T>() where T : class, IConsoleFeature => this.HasFeature(typeof(T));

    public virtual bool HasFeature(Type typeOfFeature) {
        if (!typeof(IConsoleFeature).IsAssignableFrom(typeOfFeature))
            throw new ArgumentException("Feature type is not assignable to " + nameof(IConsoleFeature));
        
        return this.featureManager.HasService(typeOfFeature);
    }

    /// <summary>
    /// Registers a feature with our internal service manager
    /// </summary>
    /// <param name="feature">The feature instance</param>
    /// <typeparam name="T">The feature type</typeparam>
    protected void RegisterFeature<T>(T feature) where T : class, IConsoleFeature {
        this.featureManager.RegisterConstant(feature);
    }
    
    /// <summary>
    /// Registers a feature, creating it when required.
    /// </summary>
    /// <param name="feature">The feature factory</param>
    /// <typeparam name="T">The feature type</typeparam>
    protected void RegisterFeatureLazy<T>(Func<T> feature) where T : class, IConsoleFeature {
        this.featureManager.RegisterLazy<T>(feature);
    }

    public abstract Task<bool?> IsMemoryInvalidOrProtected(uint address, uint count);

    public void Close() {
        if (Interlocked.CompareExchange(ref this.isClosedState, 1, 0) != 0) {
            return; // already closed
        }
        
#pragma warning disable CA1816
        GC.SuppressFinalize(this);
#pragma warning restore CA1816

        ExceptionDispatchInfo? closeException = null, eventException = null;
        try {
            this.CloseOverride();
        }
        catch (Exception e) {
            closeException = ExceptionDispatchInfo.Capture(e);
        }

        try {
            this.Closed?.Invoke(this);
        }
        catch (Exception e) {
            eventException = ExceptionDispatchInfo.Capture(e);
        }

        if (closeException != null) {
            if (eventException != null)
                throw new AggregateException("Exception occurred while closing connection and also raising " + nameof(this.Closed) + " event", closeException.SourceException, eventException.SourceException);
            
            closeException.Throw();
        }
        else if (eventException != null) {
            eventException.Throw();
        }
    }

    protected virtual void CloseOverride() {
    }

    public async Task ReadBytes(uint address, byte[] buffer, int offset, int count) {
        if (count < 0) 
            throw new ArgumentOutOfRangeException(nameof(count), count, nameof(count) + " cannot be negative");
        if (offset < 0) 
            throw new ArgumentOutOfRangeException(nameof(offset), offset, nameof(offset) + " cannot be negative");
        if (count == 0) 
            return;

        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        try {
            await this.ReadBytesCore(address, buffer, offset, count).ConfigureAwait(false);
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }
    }

    public async Task ReadBytes(uint address, byte[] buffer, int offset, int count, uint chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default) {
        if (count == 0)
            return;
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, nameof(count) + " cannot be negative");
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, nameof(offset) + " cannot be negative");

        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        try {
            await this.ReadBytesInChunksUnderBusyLock(address, buffer, offset, count, chunkSize, completion, cancellationToken);
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }
    }

    public async Task<byte[]> ReadBytes(uint address, int count) {
        byte[] buffer = new byte[count];
        await this.ReadBytes(address, buffer, 0, count).ConfigureAwait(false);
        return buffer;
    }

    public async Task<byte> ReadByte(uint address) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        try {
            await this.ReadBytesCore(address, this.sharedByteArray8, 0, 1).ConfigureAwait(false);
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }

        return this.sharedByteArray8[0];
    }

    public async Task<bool> ReadBool(uint address) => await this.ReadByte(address).ConfigureAwait(false) != 0;

    public async Task<char> ReadChar(uint address) => (char) await this.ReadByte(address).ConfigureAwait(false);

    public async Task<T> ReadValue<T>(uint address) where T : unmanaged {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        byte[] buffer = new byte[Unsafe.SizeOf<T>()];
        
        try {
            await this.ReadBytesCore(address, buffer, 0, buffer.Length).ConfigureAwait(false);
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }

        if (BitConverter.IsLittleEndian != this.IsLittleEndian) {
            Array.Reverse(buffer);
        }

        return MemoryMarshal.Read<T>(buffer);
    }

    public async Task<T> ReadStruct<T>(uint address, params int[] fields) where T : unmanaged {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        int offset = 0;
        byte[] buffer = new byte[Unsafe.SizeOf<T>()];

        try {
            foreach (int cbField in fields) {
                Debug.Assert(cbField >= 0, "Field was negative");

                this.EnsureNotClosed();
                await this.ReadBytesCore((uint) (address + offset), buffer, offset, cbField).ConfigureAwait(false);
                if (BitConverter.IsLittleEndian != this.IsLittleEndian)
                    Array.Reverse(buffer, offset, cbField);

                offset += cbField;

                Debug.Assert(offset >= 0, "Integer overflow during " + nameof(this.ReadStruct));
            }
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }

        if (offset > buffer.Length) {
            Debugger.Break();
        }

        return Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetArrayDataReference(buffer));
    }

    public async Task<string> ReadStringASCII(uint address, int count, bool removeNull = true) {
        byte[] buffer = await this.ReadBytes(address, count).ConfigureAwait(false);

        if (removeNull) {
            int j = 0, k = 0;
            for (; k < count; k++) {
                if (buffer[k] != 0) {
                    buffer[j++] = buffer[k];
                }
            }

            count = j;
        }

        return Encoding.ASCII.GetString(buffer, 0, count);
    }

    public async Task<string> ReadString(uint address, int count, Encoding encoding) {
        int cbMaxLength = encoding.GetMaxByteCount(count);
        byte[] buffer = new byte[cbMaxLength];
        await this.ReadBytes(address, buffer, 0, cbMaxLength).ConfigureAwait(false);

        Decoder decoder = encoding.GetDecoder();
        char[] charBuffer = new char[count];

        try {
            decoder.Convert(buffer, 0, cbMaxLength, charBuffer, 0, count, true, out _, out int charsUsed, out _);
            return new string(charBuffer, 0, charsUsed);
        }
        catch {
            return "";
        }
    }

    public Task WriteBytes(uint address, byte[] buffer) => this.WriteBytes(address, buffer, 0, buffer.Length);

    public async Task WriteBytes(uint address, byte[] buffer, int offset, int count) {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Value must be within the bounds of the array");
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Count cannot be negative");
        if (count == 0)
            return;
        
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        try {
            await this.WriteBytesCore(address, buffer, offset, count).ConfigureAwait(false);
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }
    }

    public async Task WriteBytes(uint address, byte[] buffer, int offset, int count, uint chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default) {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, nameof(count) + " cannot be negative");
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, nameof(offset) + " cannot be negative");
        if (count == 0)
            return;

        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        await this.WriteBytesInChunksUnderBusyLock(address, buffer, offset, count, chunkSize, completion, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteByte(uint address, byte value) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        this.sharedByteArray8[0] = value;
        await this.WriteBytesCore(address, this.sharedByteArray8, 0, 1).ConfigureAwait(false);
    }

    public Task WriteBool(uint address, bool value) => this.WriteByte(address, (byte) (value ? 0x01 : 0x00));

    public Task WriteChar(uint address, char value) => this.WriteByte(address, (byte) value);

    public Task WriteValue<T>(uint address, T value) where T : unmanaged {
        int cbValue = Unsafe.SizeOf<T>();
        byte[] bytes = cbValue > 8 ? new byte[cbValue] : this.sharedByteArray8;
        Unsafe.As<byte, T>(ref MemoryMarshal.GetArrayDataReference(bytes)) = value;
        if (BitConverter.IsLittleEndian != this.IsLittleEndian) {
            Array.Reverse(bytes);
        }

        return this.WriteBytes(address, bytes);
    }

    public async Task WriteStruct<T>(uint address, T value, params int[] fields) where T : unmanaged {
        this.EnsureNotClosed();

        int offset = 0;
        byte[] buffer = new byte[Unsafe.SizeOf<T>()];
        foreach (int cbField in fields) {
            Debug.Assert(cbField >= 0, "Field was negative");

            Unsafe.As<byte, T>(ref buffer[offset]) = value;
            if (BitConverter.IsLittleEndian != this.IsLittleEndian)
                Array.Reverse(buffer, offset, cbField);

            offset += cbField;
            Debug.Assert(offset >= 0, "Integer overflow during " + nameof(this.WriteStruct));
        }

        if (offset > buffer.Length) {
            Debugger.Break();
        }

        await this.WriteBytes(address, buffer).ConfigureAwait(false);
    }

    public Task WriteString(uint address, string value) => this.WriteString(address, value, Encoding.ASCII);

    public Task WriteString(uint address, string value, Encoding encoding) {
        return this.WriteBytes(address, encoding.GetBytes(value));
    }

    public Task<uint?> FindPattern(uint address, uint count, MemoryPattern pattern, CompletionState? completion = null, CancellationToken cancellationToken = default) {
        return Task.FromResult<uint?>(null);
    }

    // Use a dedicated resolve pointer to improve performance as much as possible,
    // since reading from the console takes a long time, relative to 
    public async Task<uint?> ResolvePointer(uint baseAddress, ImmutableArray<int> offsets) {
        // Even if Offsets.Length is zero, still check disposed and take busy token to try and catch unsynchronized access
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();
        
        long ptr = baseAddress;
        if (offsets.Length > 0) {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(sizeof(uint));
            try {
                bool reverse = BitConverter.IsLittleEndian != this.IsLittleEndian;

                foreach (int offset in offsets) {
                    this.EnsureNotClosed();
                    await this.ReadBytesCore((uint) ptr, buffer, 0, sizeof(uint)).ConfigureAwait(false);
                    if (reverse)
                        Array.Reverse(buffer, 0, sizeof(uint));

                    uint deref = MemoryMarshal.Read<uint>(new ReadOnlySpan<byte>(buffer, 0, sizeof(uint)));
                    ptr = Math.Max((long) deref + offset, 0);
                    if (ptr <= 0 || ptr > uint.MaxValue) {
                        return null;
                    }
                }
            }
            finally {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // The final pointer, which points to hopefully an effective value (e.g. float or literally anything)
        return (uint) ptr;
    }

    protected BusyToken CreateBusyToken() {
        if (Interlocked.CompareExchange(ref this.busyStack, 1, 0) != 0)
            throw new InvalidOperationException("Already busy performing another operation");
        return new BusyToken(this);
    }

    protected void EnsureNotClosed() {
        if (this.isClosedState != 0) {
            throw new IOException("Connection is closed");
        }
    }

    public Task WriteVector2(uint address, Vector2 vec2) {
        return this.WriteStruct(address, vec2, 4, 4);
    }

    public Task WriteVector3(uint Offset, Vector3 vec3) {
        return this.WriteStruct(Offset, vec3, 4, 4, 4);
    }

    protected async Task ReadBytesInChunksUnderBusyLock(uint address, byte[] buffer, int offset, int count, uint chunkSize, CompletionState? completion, CancellationToken cancellationToken) {
        Debug.Assert(count >= 0);
        cancellationToken.ThrowIfCancellationRequested();
        using PopCompletionStateRangeToken? token = completion?.PushCompletionRange(0.0, 1.0 / count);

        try {
            do {
                this.EnsureNotClosed();
                cancellationToken.ThrowIfCancellationRequested();
                int cbRead = (int) Math.Min((uint) count, chunkSize);
                await this.ReadBytesCore(address, buffer, offset, cbRead).ConfigureAwait(false);

                address += (uint) cbRead;
                offset += cbRead;
                count -= cbRead;

                completion?.OnProgress(cbRead);
            } while (count > 0);
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }
    }

    protected async Task WriteBytesInChunksUnderBusyLock(uint address, byte[] bytes, int offset, int count, uint chunkSize, CompletionState? completion, CancellationToken cancellationToken) {
        Debug.Assert(count >= 0);
        cancellationToken.ThrowIfCancellationRequested();
        using PopCompletionStateRangeToken? token = completion?.PushCompletionRange(0.0, 1.0 / count);

        try {
            do {
                this.EnsureNotClosed();
                cancellationToken.ThrowIfCancellationRequested();
                int cbWrite = (int) Math.Min((uint) count, chunkSize);
                await this.WriteBytesCore(address, bytes, offset, cbWrite).ConfigureAwait(false);

                address += (uint) cbWrite;
                offset += cbWrite;
                count -= cbWrite;

                completion?.OnProgress(cbWrite);
            } while (count > 0);
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }
    }

    /// <summary>
    /// Reads an exact amount of bytes from the connection. This is invoked under busy token
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <param name="dstBuffer">The array to write the bytes into</param>
    /// <param name="offset">The offset within <see cref="dstBuffer"/> to begin writing into. Should always be greather than or equal to 0</param>
    /// <param name="count">The amount of bytes to read. This should always be greater than 0</param>
    protected abstract Task ReadBytesCore(uint address, byte[] dstBuffer, int offset, int count);

    /// <summary>
    /// Writes an exact amount of bytes to the connection. This is invoked under busy token
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="srcBuffer">The array to read the bytes from</param>
    /// <param name="offset">The offset within <see cref="srcBuffer"/> to begin reading from. Should always be greather than or equal to 0</param>
    /// <param name="count">The amount of bytes to write to the console. This should always be greater than 0</param>
    protected abstract Task WriteBytesCore(uint address, byte[] srcBuffer, int offset, int count);

    /// <summary>
    /// A token used to safeguard against concurrent read/write operations
    /// </summary>
    /// <param name="connection">The connection</param>
    protected readonly struct BusyToken(BaseConsoleConnection connection) : IDisposable {
        public void Dispose() {
            int value = Interlocked.Decrement(ref connection.busyStack);
            if (value < 0)
                Debugger.Break();
        }
    }
}