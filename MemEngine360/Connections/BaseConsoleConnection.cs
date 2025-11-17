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

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MemEngine360.Connections.Features;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Connections;

/// <summary>
/// The base class for a console connection. This class implements the basic read/write value methods
/// </summary>
public abstract class BaseConsoleConnection : IConsoleConnection {
    protected readonly byte[] sharedByteArray8 = new byte[8];
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
    }

    ~BaseConsoleConnection() {
        if (this.isClosedState == 0) {
            AppLogger.Instance.WriteLine("Destructor called on " + nameof(BaseConsoleConnection) + " when still open");
            Debug.Fail("Oops");
        }
    }

    public virtual int GetRecommendedReadChunkSize(int readTotal) {
        return readTotal;
    }

    public virtual bool TryGetFeature<T>([NotNullWhen(true)] out T? feature) where T : class, IConsoleFeature {
        feature = null;
        return false;
    }

    public bool HasFeature<T>() where T : class, IConsoleFeature {
        return this.HasFeature(typeof(T));
    }

    public virtual bool HasFeature(Type typeOfFeature) {
        if (!typeof(IConsoleFeature).IsAssignableFrom(typeOfFeature))
            throw new ArgumentException("Feature type is not assignable to " + nameof(IConsoleFeature));

        return false;
    }

    public abstract Task<bool?> IsMemoryInvalidOrProtected(uint address, int count);

    public void Close() {
        // Should we use a closing and closed state? Or stick with closed?
        if (Interlocked.CompareExchange(ref this.isClosedState, 1, 0) != 0) {
            return; // already closed
        }

#pragma warning disable CA1816
        GC.SuppressFinalize(this); // we use a finalizer but do not implement IDisposable
#pragma warning restore CA1816

        using ErrorList list = new ErrorList("One or more exceptions occurred while closing connection", throwOnDispose: true, tryUseFirstException: false);
        try {
            this.CloseOverride();
        }
        catch (Exception e) {
            list.Add(e);
        }

        try {
            this.Closed?.Invoke(this);
        }
        catch (Exception e) {
            list.Add(e);
        }
    }

    protected virtual void CloseOverride() {
    }

    // Use a dedicated resolve pointer to improve performance as much as possible,
    // since reading from the console takes a long time, relative to 
    public async Task<uint?> ResolvePointer(uint baseAddress, ImmutableArray<int> offsets) {
        // Even if Offsets.Length is zero, still check disposed and take busy token to try and catch unsynchronized access
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        long address = baseAddress;
        if (offsets.Length > 0) {
            bool reverse = BitConverter.IsLittleEndian != this.IsLittleEndian;
            foreach (int offset in offsets) {
                this.EnsureNotClosed();
                await this.ReadBytesCoreWithNetworkThrowHelper((uint) address, this.sharedByteArray8, 0, sizeof(uint)).ConfigureAwait(false);

                Span<byte> span = this.sharedByteArray8.AsSpan(0, sizeof(uint));
                if (reverse)
                    span.Reverse();

                uint deref = Unsafe.ReadUnaligned<uint>(ref MemoryMarshal.GetReference(span));
                address = Math.Max((long) deref + offset, 0);
                if (address <= 0 || address > uint.MaxValue) {
                    return null;
                }
            }
        }

        // The final pointer, which points to hopefully an effective value (e.g. float or literally anything)
        return (uint) address;
    }
    
    public async Task ReadBytes(uint address, byte[] dstBuffer, int offset, int count) {
        ArrayUtils.ThrowIfOutOfBounds(dstBuffer, offset, count);

        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        await this.ReadBytesCoreWithNetworkThrowHelper(address, dstBuffer, offset, count);
    }

    public async Task ReadBytes(uint address, byte[] dstBuffer, int offset, int count, int chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default) {
        ArrayUtils.ThrowIfOutOfBounds(dstBuffer, offset, count);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(chunkSize, 0);

        // Check+Obtain token before checking if count is zero, to maintain fail-fast behaviour
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();
        if (count == 0) {
            return;
        }

        Debug.Assert(count >= 0);
        cancellationToken.ThrowIfCancellationRequested();
        using PopCompletionStateRangeToken? token = completion?.PushCompletionRange(0.0, 1.0 / count);

        do {
            this.EnsureNotClosed();
            cancellationToken.ThrowIfCancellationRequested();
            int cbRead = (int) Math.Min((uint) count, (uint) chunkSize);
            await this.ReadBytesCoreWithNetworkThrowHelper(address, dstBuffer, offset, cbRead).ConfigureAwait(false);

            address += (uint) cbRead;
            offset += cbRead;
            count -= cbRead;

            completion?.OnProgress(cbRead);
        } while (count > 0);
    }

    public async Task<byte[]> ReadBytes(uint address, int count) {
        byte[] buffer = new byte[count];
        await this.ReadBytes(address, buffer, 0, count).ConfigureAwait(false);
        return buffer;
    }

    public async Task<byte> ReadByte(uint address) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        await this.ReadBytesCoreWithNetworkThrowHelper(address, this.sharedByteArray8, 0, 1).ConfigureAwait(false);
        return this.sharedByteArray8[0];
    }

    public async Task<bool> ReadBool(uint address) => await this.ReadByte(address).ConfigureAwait(false) != 0;

    public async Task<char> ReadChar(uint address) => (char) await this.ReadByte(address).ConfigureAwait(false);

    public async Task<T> ReadValue<T>(uint address) where T : unmanaged {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        int sizeOfT = Unsafe.SizeOf<T>();
        using (RentHelper.RentArray(sizeOfT, out byte[] buffer)) {
            await this.ReadBytesCoreWithNetworkThrowHelper(address, buffer, 0, sizeOfT).ConfigureAwait(false);

            if (BitConverter.IsLittleEndian != this.IsLittleEndian) {
                Array.Reverse(buffer, 0, sizeOfT);
            }

            return Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetArrayDataReference(buffer));
        }
    }

    public async Task<T> ReadStruct<T>(uint address, params int[] fieldSizes) where T : unmanaged {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        int offset = 0;
        int sizeOfT = Unsafe.SizeOf<T>();
        using (RentHelper.RentArray(sizeOfT, out byte[] buffer)) {
            foreach (int cbField in fieldSizes) {
                Debug.Assert(cbField >= 0, "Field was negative");

                this.EnsureNotClosed();
                await this.ReadBytesCoreWithNetworkThrowHelper((uint) (address + offset), buffer, offset, cbField).ConfigureAwait(false);
                if (BitConverter.IsLittleEndian != this.IsLittleEndian)
                    Array.Reverse(buffer, offset, cbField);

                offset += cbField;

                Debug.Assert(offset >= 0, "Integer overflow during " + nameof(this.ReadStruct));
            }

            if (offset > sizeOfT) {
                Debugger.Break();
            }

            return Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetArrayDataReference(buffer));
        }
    }

    public async Task<string> ReadStringASCII(uint address, int count, bool removeNull = true) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        return await this.InternalReadStringASCII(address, count, removeNull);
    }

    protected internal async Task<string> InternalReadStringASCII(uint address, int count, bool removeNull = true) {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0) {
            return "";
        }

        using (RentHelper.RentArray(count, out byte[] buffer)) {
            await this.ReadBytesCoreWithNetworkThrowHelper(address, buffer, 0, count).ConfigureAwait(false);
            if (removeNull) {
                int j = 0, k = 0;
                for (; k < count; ++k) {
                    if (buffer[k] == 0)
                        continue;
                    if (j != k)
                        buffer[j] = buffer[k];
                    ++j;
                }

                count = j;
            }

            return Encoding.ASCII.GetString(buffer, 0, count);
        }
    }

    public async Task<string> ReadString(uint address, int charCount, Encoding encoding) {
        int byteCount = encoding.GetMaxByteCount(charCount);
        using (RentHelper.RentArray(byteCount, out byte[] buffer)) {
            await this.ReadBytes(address, buffer, 0, byteCount).ConfigureAwait(false);

            Decoder decoder = encoding.GetDecoder();
            char[] charBuffer = new char[charCount];

            try {
                decoder.Convert(buffer, 0, byteCount, charBuffer, 0, charCount, true, out _, out int charsUsed, out _);
                return new string(charBuffer, 0, charsUsed);
            }
            catch {
                return "";
            }
        }
    }

    public async Task<string> ReadCString(uint address, CancellationToken cancellationToken = default) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();
        
        StringBuilder sb = new StringBuilder(32);
        
        // Read in chunks of 32 chars
        using (RentHelper.RentArray(32, out byte[] buffer)) {
            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                
                await this.ReadBytes(address, buffer, 0, 32).ConfigureAwait(false);
                for (int i = 0; i < 32; i++) {
                    byte b = buffer[i];
                    if (b == 0)
                        return sb.ToString();
                    
                    sb.Append((char) b);
                }
            }
        }
    }

    public Task WriteBytes(uint address, byte[] srcBuffer) => this.WriteBytes(address, srcBuffer, 0, srcBuffer.Length);

    public async Task WriteBytes(uint address, byte[] srcBuffer, int offset, int count) {
        ArrayUtils.ThrowIfOutOfBounds(srcBuffer, offset, count);

        // Check+Obtain token before checking if count is zero, to maintain fail-fast behaviour
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        if (count > 0) {
            await this.WriteBytesCoreWithNetworkThrowHelper(address, srcBuffer, offset, count).ConfigureAwait(false);
        }
    }

    public async Task WriteBytes(uint address, byte[] srcBuffer, int offset, int count, int chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default) {
        ArrayUtils.ThrowIfOutOfBounds(srcBuffer, offset, count);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(chunkSize, 0);

        // Check+Obtain token before checking if count is zero, to maintain fail-fast behaviour
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        if (count == 0) {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        using PopCompletionStateRangeToken? token = completion?.PushCompletionRange(0.0, 1.0 / count);

        do {
            this.EnsureNotClosed();
            cancellationToken.ThrowIfCancellationRequested();
            int cbWrite = (int) Math.Min((uint) count, (uint) chunkSize);
            await this.WriteBytesCoreWithNetworkThrowHelper(address, srcBuffer, offset, cbWrite).ConfigureAwait(false);

            address += (uint) cbWrite;
            offset += cbWrite;
            count -= cbWrite;

            completion?.OnProgress(cbWrite);
        } while (count > 0);
    }

    public async Task WriteByte(uint address, byte value) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        this.sharedByteArray8[0] = value;
        await this.WriteBytesCoreWithNetworkThrowHelper(address, this.sharedByteArray8, 0, 1).ConfigureAwait(false);
    }

    public Task WriteBool(uint address, bool value) => this.WriteByte(address, (byte) (value ? 0x01 : 0x00));

    public Task WriteChar(uint address, char value) => this.WriteByte(address, (byte) value);

    public Task WriteValue<T>(uint address, T value) where T : unmanaged {
        int sizeOfT = Unsafe.SizeOf<T>();
        using (RentHelper.RentArray(sizeOfT, out byte[] buffer)) {
            Unsafe.As<byte, T>(ref MemoryMarshal.GetArrayDataReference(buffer)) = value;
            if (BitConverter.IsLittleEndian != this.IsLittleEndian) {
                Array.Reverse(buffer, 0, sizeOfT);
            }

            return this.WriteBytes(address, buffer, 0, sizeOfT);
        }
    }

    public async Task WriteStruct<T>(uint address, T value, params int[] fieldSizes) where T : unmanaged {
        this.EnsureNotClosed();

        int sizeOfT = Unsafe.SizeOf<T>(), offset = 0;
        using (RentHelper.RentArray(sizeOfT, out byte[] dstBuffer)) {
            Span<byte> srcData = MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref value), sizeOfT);
            bool reverse = BitConverter.IsLittleEndian != this.IsLittleEndian;
            using (this.CreateBusyToken()) {
                Span<byte> dstSpan = dstBuffer.AsSpan(0, sizeOfT);
                foreach (int cbField in fieldSizes) {
                    Debug.Assert(cbField >= 0, "Field was negative");
                    if ((cbField + offset) > sizeOfT) {
                        throw new ArgumentException("Summation of field sizes exceeds the size of the value");
                    }

                    Span<byte> dstFieldSpan = dstSpan.Slice(offset, cbField);
                    srcData.Slice(offset, cbField).CopyTo(dstFieldSpan);
                    if (reverse) {
                        dstFieldSpan.Reverse();
                    }

                    offset += cbField;
                    Debug.Assert(offset >= 0, "Integer overflow during " + nameof(this.WriteStruct));
                }
            }

            await this.WriteBytes(address, dstBuffer, 0, offset).ConfigureAwait(false);
        }
    }

    public Task WriteString(uint address, string value) => this.WriteString(address, value, Encoding.ASCII);

    public Task WriteString(uint address, string value, Encoding encoding) {
        return this.WriteBytes(address, encoding.GetBytes(value));
    }
    
    public Task WriteVector2(uint address, Vector2 vec2) {
        return this.WriteStruct(address, vec2, sizeof(float), sizeof(float));
    }

    public Task WriteVector3(uint Offset, Vector3 vec3) {
        return this.WriteStruct(Offset, vec3, sizeof(float), sizeof(float), sizeof(float));
    }

    protected async Task ReadBytesCoreWithNetworkThrowHelper(uint address, byte[] dstBuffer, int offset, int count) {
        try {
            await this.ReadBytesCore(address, dstBuffer, offset, count).ConfigureAwait(false);
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            this.Close();
            throw;
        }
    }

    protected async Task WriteBytesCoreWithNetworkThrowHelper(uint address, byte[] srcBuffer, int offset, int count) {
        try {
            await this.WriteBytesCore(address, srcBuffer, offset, count).ConfigureAwait(false);
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
    /// <param name="count">The amount of bytes to read. This value is always greater than 0</param>
    protected abstract Task ReadBytesCore(uint address, byte[] dstBuffer, int offset, int count);

    /// <summary>
    /// Writes an exact amount of bytes to the connection. This is invoked under busy token
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="srcBuffer">The array to read the bytes from</param>
    /// <param name="offset">The offset within <see cref="srcBuffer"/> to begin reading from. Should always be greather than or equal to 0</param>
    /// <param name="count">The amount of bytes to write to the console. This value is always greater than 0</param>
    protected abstract Task WriteBytesCore(uint address, byte[] srcBuffer, int offset, int count);

    /// <summary>
    /// Creates a busy token
    /// </summary>
    /// <exception cref="InvalidOperationException">Another operation already in progress</exception>
    protected BusyToken CreateBusyToken() {
        if (Interlocked.CompareExchange(ref this.busyStack, 1, 0) != 0)
            throw new InvalidOperationException("Already busy performing another operation");
        return new BusyToken(this);
    }

    /// <summary>
    /// Throws <see cref="IOException"/> if closed
    /// </summary>
    protected void EnsureNotClosed() {
        if (this.isClosedState != 0) {
            throw new IOException("Connection is closed");
        }
    }

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