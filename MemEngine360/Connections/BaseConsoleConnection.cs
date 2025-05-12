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
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Connections;

/// <summary>
/// The base class for a console connection. This class implements the basic read/write value methods
/// </summary>
public abstract class BaseConsoleConnection : IConsoleConnection {
    protected readonly byte[] sharedOneByteArray = new byte[1];
    private volatile int busyStack;
    protected bool isClosed;

    public abstract RegisteredConsoleType ConsoleType { get; }

    public bool IsConnected => !this.isClosed && this.IsConnectedCore;

    protected abstract bool IsConnectedCore { get; }

    protected BaseConsoleConnection() {
    }

    public void Close() {
        if (this.isClosed) {
            return;
        }

        using BusyToken x = this.CreateBusyToken();
        try {
            this.CloseCore();
        }
        finally {
            this.isClosed = true;
        }
    }

    protected abstract void CloseCore();

    public async Task<uint> ReadBytes(uint address, byte[] buffer, int offset, uint count) {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        return await this.ReadBytesCore(address, buffer, offset, count).ConfigureAwait(false);
    }

    public Task ReadBytes(uint address, byte[] buffer, int offset, uint count, uint chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default) {
        if (count == chunkSize)
            return this.ReadBytes(address, buffer, offset, count);
        return this.ReadBytesInChunksWithCancellation(address, buffer, offset, count, chunkSize, cancellationToken, completion);
    }

    public async Task<byte[]> ReadBytes(uint address, uint count) {
        byte[] buffer = new byte[count];
        await this.ReadBytes(address, buffer, 0, count).ConfigureAwait(false);
        return buffer;
    }

    public async Task<byte> ReadByte(uint Offset) {
        await this.ReadBytes(Offset, this.sharedOneByteArray, 0, 1).ConfigureAwait(false);
        return this.sharedOneByteArray[0];
    }

    public async Task<bool> ReadBool(uint address) => await this.ReadByte(address).ConfigureAwait(false) != 0;

    public async Task<char> ReadChar(uint address) => (char) await this.ReadByte(address).ConfigureAwait(false);

    public async Task<T> ReadValue<T>(uint address) where T : unmanaged {
        byte[] buffer = await this.ReadBytes(address, (uint) Unsafe.SizeOf<T>()).ConfigureAwait(false);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(buffer);

        return MemoryMarshal.Read<T>(buffer);
    }

    public async Task<T> ReadStruct<T>(uint address, params int[] fields) where T : unmanaged {
        if (!BitConverter.IsLittleEndian) {
            return await this.ReadValue<T>(address).ConfigureAwait(false);
        }

        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        int offset = 0;
        byte[] buffer = new byte[Unsafe.SizeOf<T>()];
        foreach (int cbField in fields) {
            await this.ReadBytesCore((uint) (address + offset), buffer, offset, (uint) cbField).ConfigureAwait(false);
            Array.Reverse(buffer, offset, cbField);
            offset += cbField;

            Debug.Assert(offset >= 0, "Integer overflow during " + nameof(this.ReadString));
        }

        if (offset > buffer.Length) {
            Debugger.Break();
        }

        return Unsafe.ReadUnaligned<T>(ref buffer[0]);
    }

    public async Task<string> ReadString(uint address, uint count, bool removeNull = true) {
        byte[] buffer = await this.ReadBytes(address, count).ConfigureAwait(false);

        if (removeNull) {
            int j = 0, k = 0;
            for (; k < count; k++) {
                if (buffer[k] != 0) {
                    buffer[j++] = buffer[k];
                }
            }

            count = (uint) j;
        }

        return Encoding.ASCII.GetString(buffer, 0, (int) count);
    }

    public async Task WriteBytes(uint address, byte[] buffer) {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        await this.WriteBytesAndGetResponseInternal(address, buffer, 0, (uint) buffer.Length, null, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task WriteBytes(uint address, byte[] buffer, int offset, uint count, uint chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default) {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        // we ignore chunkSize because we literally write in chunks of 64 bytes so there's no reason to write less per second
        await this.WriteBytesAndGetResponseInternal(address, buffer, offset, count, completion, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteByte(uint address, byte value) {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        this.sharedOneByteArray[0] = value;
        await this.WriteBytesAndGetResponseInternal(address, this.sharedOneByteArray, 0, 1, null, CancellationToken.None).ConfigureAwait(false);
    }

    public Task WriteBool(uint address, bool value) {
        return this.WriteByte(address, (byte) (value ? 0x01 : 0x00));
    }

    public Task WriteChar(uint address, char value) {
        return this.WriteByte(address, (byte) value);
    }

    public Task WriteValue<T>(uint address, T value) where T : unmanaged {
        byte[] bytes = new byte[Unsafe.SizeOf<T>()];
        Unsafe.As<byte, T>(ref bytes[0]) = value;
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);

        return this.WriteBytes(address, bytes);
    }

    public async Task WriteStruct<T>(uint address, T value, params int[] fields) where T : unmanaged {
        if (!BitConverter.IsLittleEndian) {
            // TODO: I don't have a big endian computer nor a big enough brain to know if this works
            await this.WriteValue(address, value).ConfigureAwait(false);
        }

        int offset = 0;
        byte[] buffer = new byte[Unsafe.SizeOf<T>()];
        foreach (int cbField in fields) {
            Unsafe.As<byte, T>(ref buffer[offset]) = value;
            Array.Reverse(buffer, offset, cbField);
            offset += cbField;

            Debug.Assert(offset >= 0, "Integer overflow during " + nameof(this.ReadString));
        }

        if (offset > buffer.Length) {
            Debugger.Break();
        }

        await this.WriteBytes(address, buffer).ConfigureAwait(false);
    }

    public Task WriteString(uint address, string value) {
        return this.WriteBytes(address, Encoding.ASCII.GetBytes(value));
    }

    public async Task WriteFile(uint address, string filePath) {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        byte[] buffer = await File.ReadAllBytesAsync(filePath);
        await this.WriteBytesAndGetResponseInternal(address, buffer, 0, (uint) buffer.Length, null, CancellationToken.None);
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

    protected async Task ReadBytesInChunksWithCancellation(uint address, byte[] buffer, int offset, uint count, uint chunkSize, CancellationToken cancellationToken, CompletionState? completion) {
        cancellationToken.ThrowIfCancellationRequested();
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        // just in case
        if (chunkSize > count)
            chunkSize = count;

        int remaining = (int) count;
        using PopCompletionStateRangeToken? token = completion?.PushCompletionRange(0.0, 1.0 / remaining);
        while (remaining > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            int cbRead = (int) Math.Min(chunkSize, remaining);
            await this.ReadBytesCore(address, buffer, offset, (uint) cbRead).ConfigureAwait(false);
            remaining -= cbRead;
            offset += cbRead;
            address += (uint) cbRead;

            completion?.OnProgress(cbRead);
        }

        if (remaining < 0)
            throw new Exception("Error: got more bytes that we wanted");
    }

    protected async Task WriteBytesAndGetResponseInternal(uint address, byte[] bytes, int offset, uint count, CompletionState? completion, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        using PopCompletionStateRangeToken? token = completion?.PushCompletionRange(0.0, 1.0 / count);

        while (count > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            uint cbWrite = Math.Min(count, 64);
            await this.WriteBytesCore(address, bytes, offset, cbWrite).ConfigureAwait(false);

            address += cbWrite;
            offset += (int) cbWrite;
            count -= cbWrite;
            completion?.OnProgress(cbWrite);
        }
    }

    /// <summary>
    /// Reads bytes from the connection. This is invoked under busy token
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <param name="dstBuffer">The array to write the bytes into</param>
    /// <param name="offset">The offset within <see cref="dstBuffer"/> to begin writing into</param>
    /// <param name="count">The amount of bytes to read</param>
    /// <returns>The amount of bytes actually read</returns>
    protected abstract Task<uint> ReadBytesCore(uint address, byte[] dstBuffer, int offset, uint count);

    /// <summary>
    /// Writes bytes to the connection. This is invoked under busy token
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="srcBuffer">The array to read the bytes from</param>
    /// <param name="offset">The offset within <see cref="srcBuffer"/> to begin reading from</param>
    /// <param name="count">The amount of bytes to write to the console</param>
    /// <returns>The amount of bytes actually written</returns>
    protected abstract Task<uint> WriteBytesCore(uint address, byte[] srcBuffer, int offset, uint count);

    protected readonly struct BusyToken(BaseConsoleConnection connection) : IDisposable {
        public void Dispose() {
            int value = Interlocked.Decrement(ref connection.busyStack);
            if (value < 0)
                Debugger.Break();
        }
    }
}