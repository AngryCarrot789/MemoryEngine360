using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MemEngine360.Connections;
using XDevkit;
using PFXToolKitUI.Tasks;
using MemoryRegion = MemEngine360.Connections.MemoryRegion;

namespace MemEngine360.Xbox360XDevkit;

public class Devkit360Connection : IConsoleConnection, IHaveIceCubes, IHaveMemoryRegions {
    private readonly struct BusyToken : IDisposable {
        private readonly Devkit360Connection connection;

        public BusyToken(Devkit360Connection connection) {
            this.connection = connection;
        }

        public void Dispose() {
            int value = Interlocked.Decrement(ref this.connection.busyStack);
            if (value < 0)
                Debugger.Break();
        }
    }

    private static readonly byte[] ONE_BYTE = new byte[1]; // Since access is synchronized, it's safe to do this

    private volatile int busyStack;
    private bool isClosed;

    private readonly XboxManager manager;
    private readonly XboxConsole console;
    private bool hasSetupSysCall;
    private uint bufferAddress;
    private uint stringPointer, floatPointer, bytePointer;
    private readonly byte[] nulled = new byte[100];

    public XboxConsole Console => this.console;
    
    public RegisteredConsoleType ConsoleType => ConsoleTypeXbox360XDevkit.Instance;
    public bool IsConnected => !this.isClosed;

    public bool IsBusy => this.busyStack > 0;

    public Devkit360Connection(XboxManager manager, XboxConsole console) {
        this.manager = manager;
        this.console = console;
    }

    public void Close(bool sendGoodbyte = true) {
        if (this.isClosed) {
            return;
        }

        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();
        if (this.IsConnected) {
            this.console.DebugTarget.DisconnectAsDebugger();
        }

        this.isClosed = true;
    }

    public async Task RebootConsole(bool cold = true) {
        this.Close();
    }

    public async Task ShutdownConsole() {
        this.Close();
    }

    public async Task OpenDiskTray() {
    }

    public async Task DebugFreeze() {
        this.console.DebugTarget.Stop(out bool isAlreadyStopped);
    }

    public async Task DebugUnFreeze() {
        this.console.DebugTarget.Go(out bool isAlreadyGoing);
    }

    public async Task<List<MemoryRegion>> GetMemoryRegions() {
        List<MemoryRegion> regionList = new List<MemoryRegion>();
        IXboxMemoryRegions regions = this.console.DebugTarget.MemoryRegions;
        for (int i = 0, count = regions.Count; i < count; i++) {
            IXboxMemoryRegion region = regions[i];
            regionList.Add(new MemoryRegion((uint) region.BaseAddress, (uint) region.RegionSize, (uint) region.Flags, 0));
        }

        return regionList;
    }

    public async Task<uint> GetProcessID() {
        uint value = this.console.RunningProcessInfo.ProcessId;
        return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    public async Task<IPAddress> GetTitleIPAddress() {
        uint value = this.console.IPAddressTitle;
        return new IPAddress(BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }

    public async Task<int> ReadBytes(uint address, byte[] buffer, int offset, uint count) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        return await this.ReadBytesInTaskInternal(address, buffer, offset, count);
    }

    public Task ReadBytes(uint address, byte[] buffer, int offset, uint count, uint chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default) {
        if (count == chunkSize)
            return this.ReadBytes(address, buffer, offset, count);
        return this.ReadBytesInChunksWithCancellation(address, buffer, offset, count, chunkSize, cancellationToken, completion);
    }

    public async Task<byte[]> ReadBytes(uint address, uint count) {
        byte[] buffer = new byte[count];
        await this.ReadBytes(address, buffer, 0, count);
        return buffer;
    }

    public async Task<byte> ReadByte(uint Offset) {
        await this.ReadBytes(Offset, ONE_BYTE, 0, 1);
        return ONE_BYTE[0];
    }

    public async Task<bool> ReadBool(uint address) => await this.ReadByte(address) != 0;

    public async Task<char> ReadChar(uint address) => (char) await this.ReadByte(address);

    public async Task<T> ReadValue<T>(uint address) where T : unmanaged {
        byte[] buffer = await this.ReadBytes(address, (uint) Unsafe.SizeOf<T>());
        if (BitConverter.IsLittleEndian)
            Array.Reverse(buffer);

        return MemoryMarshal.Read<T>(buffer);
    }

    public async Task<T> ReadStruct<T>(uint address, params int[] fields) where T : unmanaged {
        if (!BitConverter.IsLittleEndian) {
            return await this.ReadValue<T>(address);
        }

        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        int offset = 0;
        byte[] buffer = new byte[Unsafe.SizeOf<T>()];
        foreach (int cbField in fields) {
            await this.ReadBytesInTaskInternal((uint) (address + offset), buffer, offset, (uint) cbField);
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
        byte[] buffer = await this.ReadBytes(address, count);

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
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        await this.WriteBytesAndGetResponseInternal(address, buffer, 0, (uint) buffer.Length, (uint) buffer.Length, null, CancellationToken.None);
    }

    public async Task WriteBytes(uint address, byte[] buffer, int offset, uint count, uint chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        await this.WriteBytesAndGetResponseInternal(address, buffer, offset, count, chunkSize, completion, cancellationToken);
    }

    public async Task WriteByte(uint address, byte value) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        ONE_BYTE[0] = value;
        await this.WriteBytesAndGetResponseInternal(address, ONE_BYTE, 0, 1, uint.MaxValue, null, CancellationToken.None);
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
            await this.WriteValue(address, value);
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

        await this.WriteBytes(address, buffer);
    }

    public Task WriteString(uint address, string value) {
        return this.WriteBytes(address, Encoding.ASCII.GetBytes(value));
    }

    public async Task WriteFile(uint address, string filePath) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        byte[] buffer = await File.ReadAllBytesAsync(filePath);
        await this.WriteBytesAndGetResponseInternal(address, buffer, 0, (uint) buffer.Length, (uint) buffer.Length, null, CancellationToken.None);
    }

    public Task WriteHook(uint address, uint destination, bool isLinked) {
        uint[] Func = new uint[4];
        if ((destination & 0x8000) != 0)
            Func[0] = 0x3D600000 + (((destination >> 16) & 0xFFFF) + 1);
        else
            Func[0] = 0x3D600000 + ((destination >> 16) & 0xFFFF);
        Func[1] = 0x396B0000 + (destination & 0xFFFF);
        Func[2] = 0x7D6903A6;
        Func[3] = 0x4E800420;
        if (isLinked)
            Func[3]++;
        byte[] buffer = new byte[0x10];
        byte[] f1 = BitConverter.GetBytes(Func[0]);
        byte[] f2 = BitConverter.GetBytes(Func[1]);
        byte[] f3 = BitConverter.GetBytes(Func[2]);
        byte[] f4 = BitConverter.GetBytes(Func[3]);
        if (BitConverter.IsLittleEndian) {
            Array.Reverse(f1);
            Array.Reverse(f2);
            Array.Reverse(f3);
            Array.Reverse(f4);
        }

        for (int i = 0; i < 4; i++)
            buffer[i] = f1[i];
        for (int i = 4; i < 8; i++)
            buffer[i] = f2[i - 4];
        for (int i = 8; i < 0xC; i++)
            buffer[i] = f3[i - 8];
        for (int i = 0xC; i < 0x10; i++)
            buffer[i] = f4[i - 0xC];
        return this.WriteBytes(address, buffer);
    }

    private BusyToken CreateBusyToken() {
        Interlocked.Increment(ref this.busyStack);
        return new BusyToken(this);
    }

    private void EnsureNotBusy() {
        if (this.busyStack > 0) {
            throw new InvalidOperationException("Busy performing operation");
        }
    }

    private void EnsureNotDisposed() {
        if (this.isClosed) {
            throw new ObjectDisposedException(nameof(Devkit360Connection), "Connection is disposed");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CharToInteger(char c) => c <= '9' ? (c - '0') : ((c & ~0x20 /* LOWER TO UPPER CASE */) - 'A' + 10);

    private async Task ReadBytesInChunksWithCancellation(uint address, byte[] buffer, int offset, uint count, uint chunkSize, CancellationToken cancellationToken, CompletionState? completion) {
        cancellationToken.ThrowIfCancellationRequested();
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        // just in case
        if (chunkSize > count)
            chunkSize = count;

        int length = (int) count;
        using PopCompletionStateRangeToken? token = completion?.PushCompletionRange(0.0, 1.0 / length);
        while (length > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            int cbRead = await this.ReadBytesInTaskInternal((uint) (address + offset), buffer, offset, (uint) Math.Min(chunkSize, length));
            length -= cbRead;
            offset += cbRead;

            completion?.OnProgress(cbRead);
        }

        if (length < 0)
            throw new Exception("Error: got more bytes that we wanted");
    }

    private async Task<int> ReadBytesInTaskInternal(uint address, byte[] buffer, int offset, uint count, bool invalidateCaches = true) {
        if (count < 1) {
            return 0;
        }

        return (int) await Task.Run(() => {
            uint bytesRead = 0;
            if (offset == 0) {
                this.console.DebugTarget.GetMemory(address, count, buffer, out bytesRead);
                if (invalidateCaches)
                    this.console.DebugTarget.InvalidateMemoryCache(true, address, bytesRead);
                return bytesRead;
            }
            else {
                Span<byte> newBuffer = stackalloc byte[0x1000]; // not too huge
                uint length = count;
                while (length > 0) {
                    uint cbRead = Math.Min(length, 0x1000);
                    this.console.DebugTarget.GetMemory_cpp(address, cbRead, MemoryMarshal.GetReference(newBuffer), out uint cbActuallyRead);
                    newBuffer.Slice(0, (int) cbRead).CopyTo(new Span<byte>(buffer, offset, (int) cbRead));
                    offset += (int) cbRead;
                    length -= cbRead;
                    address += cbRead;
                    bytesRead += cbActuallyRead;
                }

                if (invalidateCaches)
                    this.console.DebugTarget.InvalidateMemoryCache(true, address, count);

                return bytesRead;
            }
        });
    }

    private async Task WriteBytesAndGetResponseInternal(uint address, byte[] bytes, int offset, uint count, uint chunkSize, CompletionState? completion, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        int remaining = (int) count;
        using PopCompletionStateRangeToken? token = completion?.PushCompletionRange(0.0, 1.0 / remaining);
        while (remaining > 0) {
            uint cbWrite = Math.Min(chunkSize, (uint) remaining);
            await Task.Run(() => {
                // cbBytesWritten may be zero if writing into protected memory, therefore, we don't want to use it
                this.console.DebugTarget.SetMemory_cpp(address, cbWrite, Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(bytes), offset), out uint cbBytesWritten);
            }, cancellationToken);

            address += cbWrite;
            offset += (int) cbWrite;
            remaining -= (int) cbWrite;
            completion?.OnProgress(cbWrite);
        }
    }
}