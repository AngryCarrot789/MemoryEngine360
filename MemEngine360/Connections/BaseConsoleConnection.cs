﻿// 
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
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MemEngine360.Engine.Addressing;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Connections;

/// <summary>
/// The base class for a console connection. This class implements the basic read/write value methods
/// </summary>
public abstract class BaseConsoleConnection : IConsoleConnection {
    protected readonly byte[] sharedOneByteArray = new byte[1];
    private volatile int busyStack;
    protected volatile bool isClosed;

    public abstract RegisteredConnectionType ConnectionType { get; }

    public bool IsConnected => !this.isClosed && this.IsConnectedCore;

    public bool IsClosed => this.isClosed;

    protected abstract bool IsConnectedCore { get; }

    public abstract bool IsLittleEndian { get; }

    /// <summary>
    /// Gets the supported addressing range. Reading/writing outside this range will yield invalid data.
    /// <para>
    /// This value can change at any time and there's no way to tell when, so beware of that.
    /// </para>
    /// </summary>
    public abstract AddressRange AddressableRange { get; }

    protected BaseConsoleConnection() {
    }

    ~BaseConsoleConnection() {
        if (!this.isClosed)
            AppLogger.Instance.WriteLine("Destructor called on " + nameof(BaseConsoleConnection) + " when still open");
    }

    public abstract Task<bool?> IsMemoryInvalidOrProtected(uint address, uint count);

    public async Task Close() {
        if (!this.isClosed) {
            using BusyToken x = this.CreateBusyToken();
            try {
                await this.CloseCore();
            }
            finally {
                this.isClosed = true;
            }
        }
    }

    protected abstract Task CloseCore();

    public async Task ReadBytes(uint address, byte[] buffer, int offset, int count) {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, nameof(count) + " cannot be negative");
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, nameof(offset) + " cannot be negative");
        if (count == 0)
            return;

        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        await this.ReadBytesCore(address, buffer, offset, count).ConfigureAwait(false);
    }

    public async Task ReadBytes(uint address, byte[] buffer, int offset, int count, uint chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default) {
        if (count == 0)
            return;
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, nameof(count) + " cannot be negative");
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, nameof(offset) + " cannot be negative");

        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        await this.ReadBytesInChunksUnderBusyLock(address, buffer, offset, count, chunkSize, completion, cancellationToken);
    }

    public async Task<byte[]> ReadBytes(uint address, int count) {
        byte[] buffer = new byte[count];
        await this.ReadBytes(address, buffer, 0, count).ConfigureAwait(false);
        return buffer;
    }

    public async Task<byte> ReadByte(uint address) {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        await this.ReadBytesCore(address, this.sharedOneByteArray, 0, 1).ConfigureAwait(false);
        return this.sharedOneByteArray[0];
    }

    public async Task<bool> ReadBool(uint address) => await this.ReadByte(address).ConfigureAwait(false) != 0;

    public async Task<char> ReadChar(uint address) => (char) await this.ReadByte(address).ConfigureAwait(false);

    public async Task<T> ReadValue<T>(uint address) where T : unmanaged {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        byte[] buffer = new byte[Unsafe.SizeOf<T>()];
        await this.ReadBytesCore(address, buffer, 0, buffer.Length).ConfigureAwait(false);
        if (BitConverter.IsLittleEndian != this.IsLittleEndian) {
            Array.Reverse(buffer);
        }

        return MemoryMarshal.Read<T>(buffer);
    }

    public async Task<T> ReadStruct<T>(uint address, params int[] fields) where T : unmanaged {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        int offset = 0;
        byte[] buffer = new byte[Unsafe.SizeOf<T>()];
        foreach (int cbField in fields) {
            Debug.Assert(cbField >= 0, "Field was negative");

            await this.ReadBytesCore((uint) (address + offset), buffer, offset, cbField).ConfigureAwait(false);
            if (BitConverter.IsLittleEndian != this.IsLittleEndian)
                Array.Reverse(buffer, offset, cbField);

            offset += cbField;

            Debug.Assert(offset >= 0, "Integer overflow during " + nameof(this.ReadStruct));
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

    public async Task WriteBytes(uint address, byte[] buffer) {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        await this.WriteBytesCore(address, buffer, 0, buffer.Length).ConfigureAwait(false);
    }

    public async Task WriteBytes(uint address, byte[] buffer, int offset, int count, uint chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default) {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, nameof(count) + " cannot be negative");
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, nameof(offset) + " cannot be negative");
        if (count == 0)
            return;

        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        await this.WriteBytesInChunksUnderBusyLock(address, buffer, offset, count, chunkSize, completion, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteByte(uint address, byte value) {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        this.sharedOneByteArray[0] = value;
        await this.WriteBytesCore(address, this.sharedOneByteArray, 0, 1).ConfigureAwait(false);
    }

    public Task WriteBool(uint address, bool value) => this.WriteByte(address, (byte) (value ? 0x01 : 0x00));

    public Task WriteChar(uint address, char value) => this.WriteByte(address, (byte) value);

    public Task WriteValue<T>(uint address, T value) where T : unmanaged {
        byte[] bytes = new byte[Unsafe.SizeOf<T>()];
        Unsafe.As<byte, T>(ref MemoryMarshal.GetArrayDataReference(bytes)) = value;
        if (BitConverter.IsLittleEndian != this.IsLittleEndian) {
            Array.Reverse(bytes);
        }

        return this.WriteBytes(address, bytes);
    }

    public async Task WriteStruct<T>(uint address, T value, params int[] fields) where T : unmanaged {
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

    public async Task<uint?> FindPattern(uint address, uint count, MemoryPattern pattern, CompletionState? completion = null, CancellationToken cancellationToken = default) {
        return null;
    }

    // Use a dedicated resolve pointer to improve performance as much as possible,
    // since reading from the console takes a long time, relative to 
    public async Task<uint?> ResolvePointer(DynamicAddress address) {
        // Even if Offsets.Length is zero, still check disposed and take busy token to try and catch unsynchronized access
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();
        
        long ptr = address.BaseAddress;
        ImmutableArray<int> offsets = address.Offsets;
        
        if (offsets.Length > 0) {
            byte[] buffer = new byte[sizeof(uint)];
            bool reverse = BitConverter.IsLittleEndian != this.IsLittleEndian;

            foreach (int offset in offsets) {
                await this.ReadBytesCore((uint) ptr, buffer, 0, buffer.Length).ConfigureAwait(false);
                if (reverse)
                    Array.Reverse(buffer);

                uint deref = MemoryMarshal.Read<uint>(buffer);
                ptr = Math.Max((long) deref + offset, 0);
                if (ptr <= 0 || ptr > uint.MaxValue) {
                    return null;
                }
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

    protected void EnsureNotDisposed() {
        if (this.isClosed) {
            throw new ObjectDisposedException(nameof(BaseConsoleConnection), "Connection is disposed");
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
        do {
            cancellationToken.ThrowIfCancellationRequested();
            int cbRead = (int) Math.Min((uint) count, chunkSize);
            await this.ReadBytesCore(address, buffer, offset, cbRead).ConfigureAwait(false);

            address += (uint) cbRead;
            offset += cbRead;
            count -= cbRead;

            completion?.OnProgress(cbRead);
        } while (count > 0);
    }

    protected async Task WriteBytesInChunksUnderBusyLock(uint address, byte[] bytes, int offset, int count, uint chunkSize, CompletionState? completion, CancellationToken cancellationToken) {
        Debug.Assert(count >= 0);
        cancellationToken.ThrowIfCancellationRequested();
        using PopCompletionStateRangeToken? token = completion?.PushCompletionRange(0.0, 1.0 / count);
        do {
            cancellationToken.ThrowIfCancellationRequested();
            int cbWrite = (int) Math.Min((uint) count, chunkSize);
            await this.WriteBytesCore(address, bytes, offset, cbWrite).ConfigureAwait(false);

            address += (uint) cbWrite;
            offset += cbWrite;
            count -= cbWrite;

            completion?.OnProgress(cbWrite);
        } while (count > 0);
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