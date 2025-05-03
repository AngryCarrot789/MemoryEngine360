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

using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MemEngine360.Connections;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Xbox360XBDM.Consoles.Xbdm;

// Rewrite with fixes and performance improvements, based on:
// https://github.com/XeClutch/Cheat-Engine-For-Xbox-360/blob/master/Cheat%20Engine%20for%20Xbox%20360/PhantomRTM.cs

public class PhantomRTMConsoleConnection : IXbox360Connection {
    private readonly struct BusyToken : IDisposable {
        private readonly PhantomRTMConsoleConnection connection;

        public BusyToken(PhantomRTMConsoleConnection connection) {
            this.connection = connection;
        }

        public void Dispose() {
            int value = Interlocked.Decrement(ref this.connection.busyStack);
            if (value < 0)
                Debugger.Break();
        }
    }

    private static readonly byte[] ONE_BYTE = new byte[1]; // Since access is synchronized, it's safe to do this

    private readonly TcpClient client;
    private readonly StreamReader stream;
    private volatile int busyStack;
    private bool isClosed;

    public EndPoint? EndPoint => this.IsConnected ? this.client.Client.RemoteEndPoint : null;


    public string RegisteredConsoleTypeId => ConsoleTypeXbox360Xbdm.TheID;

    public RegisteredConsoleType ConsoleType => ConsoleTypeXbox360Xbdm.Instance;
    public bool IsConnected => !this.isClosed && this.client.Connected;

    public bool IsBusy => this.busyStack > 0;

    public PhantomRTMConsoleConnection(TcpClient client, StreamReader stream) {
        this.client = client;
        this.stream = stream;
    }

    public void Close(bool sendGoodbyte = true) {
        if (this.isClosed) {
            return;
        }

        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();
        if (this.IsConnected) {
            this.client.GetStream().Write("bye\r\n"u8);
        }

        this.isClosed = true;
        this.client.Client.Close();
    }

    public async Task<ConsoleResponse> SendCommand(string command) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        return await this.SendCommandAndGetResponse(command);
    }

    private async ValueTask<string> ReadLineFromStream(CancellationToken token = default) {
        string? result;
        try {
            result = await this.stream.ReadLineAsync(token);
        }
        catch (IOException e) {
            this.client.Client.Close();
            this.isClosed = true;
            throw new IOException("IOError while reading bytes", e);
        }

        if (result == null) {
            throw new EndOfStreamException("No more bytes to read");
        }

        return result;
    }

    public async Task<List<string>> SendCommandAndReceiveLines(string command) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        ConsoleResponse response = await this.SendCommandAndGetResponse(command);
        if (response.ResponseType == ResponseType.UnknownCommand) {
            throw new Exception("Unknown command: " + command);
        }

        if (response.ResponseType != ResponseType.MultiResponse) {
            return new List<string>();
        }

        List<string> list = new List<string>();
        try {
            string line;
            while ((line = await this.ReadLineFromStream()) != ".") {
                list.Add(line);
            }
        }
        catch (IOException e) {
            this.client.Client.Close();
            this.isClosed = true;
            throw new IOException("IOError while reading bytes", e);
        }

        return list;
    }

    public async Task<List<KeyValuePair<string, string>>> SendCommandAndReceiveLines2(string Text) {
        return (await this.SendCommandAndReceiveLines(Text)).Select(x => {
            int split = x.IndexOf(':');
            return new KeyValuePair<string, string>(
                split == -1 ? x : x.AsSpan(0, split).Trim().ToString(),
                split == -1 ? "" : x.AsSpan(split + 1).Trim().ToString());
        }).ToList();
    }

    public async Task<List<int>> GetThreads() {
        List<string> list = await this.SendCommandAndReceiveLines("threads");
        return list.Select(int.Parse).ToList();
        // foreach (string line in await this.SendCommandAndReceiveLines("threads")) {
        //     List<string> info = await this.SendCommandAndReceiveLines($"threadinfo thread=0x{int.Parse(line):X8}");
        //     info.Clear();
        // }
    }

    public async Task<List<ConsoleThread>> GetThreadDump() {
        List<string> list = await this.SendCommandAndReceiveLines("threads");
        List<ConsoleThread> threads = new List<ConsoleThread>(list.Count);
        foreach (string threadId in list) {
            ConsoleThread tdInfo = new ConsoleThread();
            List<string> info = await this.SendCommandAndReceiveLines($"threadinfo thread=0x{int.Parse(threadId):X8}");
            Dictionary<string, object> map = new Dictionary<string, object>();
            foreach (string part in info[0].Split(' ')) {
                int split = part.IndexOf('=');
                object value;
                string val = part.Substring(split + 1);
                if (val.StartsWith("0x"))
                    value = int.Parse(val.Substring(2), NumberStyles.HexNumber);
                else
                    value = int.Parse(val);

                map[part.Substring(0, split)] = value;
            }

            tdInfo.suspendCount = map.TryGetValue("suspend", out object? obj) ? (int) obj : 0;
            tdInfo.priority = map.TryGetValue("priority", out obj) ? (int) obj : 0;
            tdInfo.tlsBaseAddress = map.TryGetValue("tlsbase", out obj) ? (int) obj : 0;
            tdInfo.baseAddress = map.TryGetValue("base", out obj) ? (int) obj : 0;
            tdInfo.limit = map.TryGetValue("limit", out obj) ? (int) obj : 0;
            tdInfo.slack = map.TryGetValue("slack", out obj) ? (int) obj : 0;
            tdInfo.nameAddress = map.TryGetValue("nameaddr", out obj) ? (int) obj : 0;
            tdInfo.nameLength = map.TryGetValue("namelen", out obj) ? (int) obj : 0;
            tdInfo.currentProcessor = map.TryGetValue("proc", out obj) ? (int) obj : 0;
            tdInfo.lastError = map.TryGetValue("proc", out obj) ? (int) obj : 0;
            threads.Add(tdInfo);
        }

        for (int i = 0; i < threads.Count; i++) {
            ConsoleThread tdInfo = threads[i];
            if (tdInfo.nameAddress != 0 && tdInfo.nameLength > 0) {
                tdInfo.readableName = await this.ReadString((uint) tdInfo.nameAddress, (uint) tdInfo.nameLength);
                threads[i] = tdInfo;
            }
        }

        return threads;
    }

    public async Task RebootConsole(bool cold = true) {
        await this.SendCommand("magicboot" + (cold ? " cold" : ""));
        this.Close();
    }

    public async Task ShutdownConsole() {
        await this.WriteCommandText("shutdown");
        this.Close();
    }

    public async Task OpenDiskTray() {
        await this.SendCommand("dvdeject");
    }

    public async Task DebugFreeze() {
        await this.SendCommand("stop");
    }

    public async Task DebugUnFreeze() {
        await this.SendCommand("go");
    }

    public async Task DeleteFile(string path) {
        string[] lines = path.Split("\\".ToCharArray());
        string Directory = "";
        for (int i = 0; i < (lines.Length - 1); i++)
            Directory += lines[i] + "\\";
        await this.SendCommand("delete title=\"" + path + "\" dir=\"" + Directory + "\"");
    }

    public async Task LaunchFile(string path) {
        string[] lines = path.Split("\\".ToCharArray());
        string Directory = "";
        for (int i = 0; i < lines.Length - 1; i++)
            Directory += lines[i] + "\\";
        await this.SendCommand("magicboot title=\"" + path + "\" directory=\"" + Directory + "\"");
    }

    public async Task<string> GetConsoleID() {
        return (await this.SendCommand("getconsoleid")).Message.Substring(10);
    }

    public async Task<string> GetCPUKey() {
        return (await this.SendCommand("getcpukey")).Message;
    }

    public async Task<string> GetDebugName() {
        return (await this.SendCommand("dbgname")).Message;
    }

    public async Task<string?> GetXbeInfo(string? executable) {
        List<string> result = await this.SendCommandAndReceiveLines($"xbeinfo {(executable != null ? ("name=" + executable) : "running")}");
        foreach (string line in result) {
            if (line.StartsWith("name=")) {
                return line.Substring(6, line.Length - 7);
            }
        }

        return null;
    }

    public async Task<List<MemoryRegion>> GetMemoryRegions() {
        List<string> list = await this.SendCommandAndReceiveLines("walkmem");
        return list.Select(line => {
            // base=0x00000000 size=0x00000000 protect=0x00000000 phys=0x00000000
            uint propBase = uint.Parse(line.AsSpan(7, 8), NumberStyles.HexNumber);
            uint propSize = uint.Parse(line.AsSpan(23, 8), NumberStyles.HexNumber);
            uint propProt = uint.Parse(line.AsSpan(42, 8), NumberStyles.HexNumber);
            uint propPhys = uint.Parse(line.AsSpan(58, 8), NumberStyles.HexNumber);
            return new MemoryRegion(propBase, propSize, propProt, propPhys);
        }).ToList();
    }

    public async Task<ExecutionState> GetExecutionState() {
        string str = (await this.SendCommand("getexecstate")).Message;
        switch (str) {
            case "pending":       return ExecutionState.Pending;
            case "reboot":        return ExecutionState.Reboot;
            case "start":         return ExecutionState.Start;
            case "stop":          return ExecutionState.Stop;
            case "pending_title": return ExecutionState.TitlePending;
            case "reboot_title":  return ExecutionState.TitleReboot;
            default:              return ExecutionState.Unknown;
        }
    }

    public async Task<HardwareInfo> GetHardwareInfo() {
        List<KeyValuePair<string, string>> lines = await this.SendCommandAndReceiveLines2("hwinfo");
        HardwareInfo info;
        info.Flags = uint.Parse(lines[0].Value.AsSpan(2, 8), NumberStyles.HexNumber);
        info.NumberOfProcessors = byte.Parse(lines[1].Value.AsSpan(2, 2), NumberStyles.HexNumber);
        info.PCIBridgeRevisionID = byte.Parse(lines[2].Value.AsSpan(2, 2), NumberStyles.HexNumber);

        string rbStr = lines[3].Value;
        info.ReservedBytes = new byte[6];
        info.ReservedBytes[0] = byte.Parse(rbStr.AsSpan(3, 2), NumberStyles.HexNumber);
        info.ReservedBytes[1] = byte.Parse(rbStr.AsSpan(6, 2), NumberStyles.HexNumber);
        info.ReservedBytes[2] = byte.Parse(rbStr.AsSpan(9, 2), NumberStyles.HexNumber);
        info.ReservedBytes[3] = byte.Parse(rbStr.AsSpan(12, 2), NumberStyles.HexNumber);
        info.ReservedBytes[4] = byte.Parse(rbStr.AsSpan(15, 2), NumberStyles.HexNumber);
        info.ReservedBytes[5] = byte.Parse(rbStr.AsSpan(18, 2), NumberStyles.HexNumber);
        info.BldrMagic = ushort.Parse(lines[4].Value.AsSpan(2, 4), NumberStyles.HexNumber);
        info.BldrFlags = ushort.Parse(lines[5].Value.AsSpan(2, 4), NumberStyles.HexNumber);
        return info;
    }

    public async Task<uint> GetProcessID() {
        uint value = uint.Parse((await this.SendCommand("getpid")).Message.Substring(4).Replace("0x", ""), NumberStyles.HexNumber);
        return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    public async Task<IPAddress> GetTitleIPAddress() {
        uint value = uint.Parse((await this.SendCommand("altaddr")).Message.Substring(5).Replace("0x", ""), NumberStyles.HexNumber);
        return new IPAddress(BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }

    public async Task SetConsoleColor(ConsoleColor colour) {
        await this.SendCommand("setcolor name=" + colour.ToString().ToLower());
    }

    public async Task SetDebugName(string newName) {
        await this.SendCommand("dbgname name=" + newName);
    }

    public async Task<int> ReadBytes(uint address, byte[] buffer, int offset, uint count) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        return await this.ReadBytesInternal(address, buffer, offset, count);
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
            await this.ReadBytesInternal((uint) (address + offset), buffer, offset, (uint) cbField);
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

        await this.WriteBytesAndGetResponseInternal(address, buffer, 0, (uint) buffer.Length, null, CancellationToken.None);
    }

    public async Task WriteBytes(uint address, byte[] buffer, int offset, uint count, CompletionState? completion = null, CancellationToken cancellationToken = default) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        await this.WriteBytesAndGetResponseInternal(address, buffer, offset, count, completion, cancellationToken);
    }

    public async Task WriteByte(uint address, byte value) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        ONE_BYTE[0] = value;
        await this.WriteBytesAndGetResponseInternal(address, ONE_BYTE, 0, 1, null, CancellationToken.None);
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
        await this.WriteBytesAndGetResponseInternal(address, buffer, 0, (uint) buffer.Length, null, CancellationToken.None);
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

    public Task WriteNOP(uint address) {
        return this.WriteBytes(address, [0x60, 0x00, 0x00, 0x00]);
    }

    private async Task<ConsoleResponse> ReadResponseCore() {
        string responseText = await this.ReadLineFromStream();
        return ConsoleResponse.FromFirstLine(responseText);
    }

    private async Task WriteCommandText(string command) {
        byte[] buffer = Encoding.ASCII.GetBytes(command + "\r\n");
        try {
            await this.client.GetStream().WriteAsync(buffer);
        }
        catch (IOException e) {
            this.client.Client.Close();
            this.isClosed = true;
            throw new IOException("IOError while writing bytes", e);
        }
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
            throw new ObjectDisposedException(nameof(PhantomRTMConsoleConnection), "Connection is disposed");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CharToInteger(char c) => c <= '9' ? (c - '0') : ((c & ~0x20 /* LOWER TO UPPER CASE */) - 'A' + 10);

    public Task WriteVector2(uint address, Vector2 vec2) {
        return this.WriteStruct(address, vec2, 4, 4);
        // byte[] bytes = new byte[8];
        // byte[] x = BitConverter.GetBytes(vec2.X);
        // byte[] y = BitConverter.GetBytes(vec2.Y);
        // if (BitConverter.IsLittleEndian) {
        //     Array.Reverse(x);
        //     Array.Reverse(y);
        // }
        // 
        // Array.Copy(x, 0, bytes, 0, 4);
        // Array.Copy(y, 0, bytes, 4, 4);
        // return this.WriteBytes(Offset, bytes);
    }

    public Task WriteVector3(uint Offset, Vector3 vec3) {
        return this.WriteStruct(Offset, vec3, 4, 4, 4);

        // byte[] bytes = new byte[12];
        // byte[] x = BitConverter.GetBytes(vec3.X);
        // byte[] y = BitConverter.GetBytes(vec3.Y);
        // byte[] z = BitConverter.GetBytes(vec3.Z);
        // if (BitConverter.IsLittleEndian) {
        //     Array.Reverse(x);
        //     Array.Reverse(y);
        //     Array.Reverse(z);
        // }
        // 
        // Array.Copy(x, 0, bytes, 0, 4);
        // Array.Copy(y, 0, bytes, 4, 4);
        // Array.Copy(z, 0, bytes, 8, 4);
        // return this.WriteBytes(Offset, bytes);
    }

    // Is getmemex even for reading RAM? it could be reading shit from usb which
    // is why when you request 0x400 bytes, you can only read like 80 or something arbitrary 
    public async Task<byte[]> ReadBytesEx_BARELY_WORKS_ReadMemoryInDataOrSomething(uint address, uint count) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        byte[] output = new byte[count];
        try {
            string? command = null;
            int length = (int) count, offset = 0;
            while (length >= 0x400) {
                if (command == null)
                    command = "getmemex addr=0x" + address.ToString("X8") + " length=0x400" + "\r\n";

                ConsoleResponse response = await this.SendCommandAndGetResponse(command);
                if (response.ResponseType != ResponseType.BinaryResponse) {
                    return output;
                }

                int cbRead = await this.client.GetStream().ReadAsync(output, offset, 2);
                if (cbRead != 2) {
                    return output;
                }

                ushort flag = BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(output, 0, 2));
                cbRead = await this.client.GetStream().ReadAsync(output, offset, 0x400);
                if (cbRead == 22) { // 22 is the message length of UnknownCommand
                    offset += 22;
                    length -= 22;
                    continue;
                }

                if (cbRead != 0x400) {
                    return output;
                }

                length -= 0x400;
                offset += 0x400;

                ConsoleResponse failedResponse = await this.ReadResponseCore();
                if (failedResponse.ResponseType != ResponseType.UnknownCommand) {
                    Debug.Assert(false, "What is this bullshit who patched this bug???");
                }
            }

            if (length > 0) {
                command = "getmemex addr=0x" + address.ToString("X8") + " length=0x" + length.ToString("X8") + "\r\n";
                ConsoleResponse response = await this.SendCommandAndGetResponse(command);
                if (response.ResponseType != ResponseType.BinaryResponse) {
                    return output;
                }

                int cbRead = await this.client.GetStream().ReadAsync(output, offset, 2);
                if (cbRead != 2) {
                    return output;
                }

                ushort flag = BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(output, 0, 2));
                cbRead = await this.client.GetStream().ReadAsync(output, offset, length);
                if (cbRead != 22 && cbRead == length) {
                    ConsoleResponse failedResponse = await this.ReadResponseCore();
                    if (failedResponse.ResponseType != ResponseType.UnknownCommand) {
                        Debug.Assert(false, "What is this bullshit who patched this bug???");
                    }
                }
            }

            return output;
        }
        catch (Exception e) {
            return output;
        }
    }

    private async Task<ConsoleResponse> SendCommandAndGetResponse(string command) {
        await this.WriteCommandText(command);
        ConsoleResponse response = await this.ReadResponseCore();
        if (response.ResponseType == ResponseType.UnknownCommand) {
            // Sometimes the xbox randomly says unknown command for specific things
            if (this.client.Available > 0) {
                string responseText = await this.ReadLineFromStream() ?? "";
                response = ConsoleResponse.FromFirstLine(responseText);
            }
        }

        return response;
    }

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
            int cbRead = await this.ReadBytesInternal((uint) (address + offset), buffer, offset, (uint) Math.Min(chunkSize, length));
            length -= cbRead;
            offset += cbRead;

            completion?.OnProgress(cbRead);
        }

        if (length < 0)
            throw new Exception("Error: got more bytes that we wanted");
    }

    private async Task<int> ReadBytesInternal(uint address, byte[] buffer, int offset, uint count) {
        ConsoleResponse response = await this.SendCommandAndGetResponse("getmem addr=0x" + address.ToString("X8") + " length=0x" + count.ToString("X8") + "\r\n");
        if (response.ResponseType != ResponseType.MultiResponse) {
            throw new Exception($"Xbox responded to getmem without {nameof(ResponseType.MultiResponse)}, which is unexpected");
        }

        int cbRead = 0;
        byte[]? lineBytes = null;
        string line;
        while ((line = await this.ReadLineFromStream()) != ".") {
            int cbLine = line.Length / 2; // typically 128 when reading big chunks
            if (lineBytes == null || lineBytes.Length != cbLine) {
                lineBytes = new byte[cbLine];
            }

            for (int i = 0, j = 0; i < cbLine; i++, j += 2) {
                if (line[j] == '?') {
                    lineBytes[i] = 0; // protected memory maybe?
                }
                else {
                    lineBytes[i] = (byte) ((CharToInteger(line[j]) << 4) | CharToInteger(line[j + 1]));
                }
            }

            Array.Copy(lineBytes, 0, buffer, offset + cbRead, cbLine);
            cbRead += cbLine;
        }

        ConsoleResponse failedResponse = await this.ReadResponseCore();
        if (failedResponse.ResponseType != ResponseType.UnknownCommand) {
            Debug.Assert(false, "What is this bullshit who patched this bug???");
        }

        return cbRead;
    }

    private async Task WriteBytesAndGetResponseInternal(uint address, byte[] bytes, int offset, uint count, CompletionState? completion, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        using PopCompletionStateRangeToken? token = completion?.PushCompletionRange(0.0, 1.0 / count);
        const string HexChars = "0123456789ABCDEF";
        char[] buffer = new char[128];
        while (count > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            string cmdPrefix = "setmem addr=0x" + address.ToString("X8") + " data=";
            uint cbWrite = Math.Min(count, 64);
            for (int i = 0; i < cbWrite; i++) {
                byte b = bytes[offset + i];
                buffer[i * 2] = HexChars[b >> 4];
                buffer[i * 2 + 1] = HexChars[b & 0xF];
            }

            await this.WriteCommandText(cmdPrefix + new string(buffer, 0, (int) (cbWrite << 1)));
            ConsoleResponse response = await this.ReadResponseCore();
            if (response.ResponseType != ResponseType.SingleResponse) {
                throw new Exception($"Xbox responded to setmem without {nameof(ResponseType.SingleResponse)}, which is unexpected");
            }

            address += cbWrite;
            offset += (int) cbWrite;
            count -= cbWrite;
            completion?.OnProgress(cbWrite);
        }
    }
}