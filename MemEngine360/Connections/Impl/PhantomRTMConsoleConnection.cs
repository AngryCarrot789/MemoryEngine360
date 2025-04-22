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
using System.Text;
using MemEngine360.Connections.Impl.Threads;

namespace MemEngine360.Connections.Impl;

// Rewrite with fixes and performance improvements, based on:
// https://github.com/XeClutch/Cheat-Engine-For-Xbox-360/blob/master/Cheat%20Engine%20for%20Xbox%20360/PhantomRTM.cs

public class PhantomRTMConsoleConnection : IConsoleConnection {
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
    private bool isDisposed;

    public EndPoint? EndPoint => this.client.Connected ? this.client.Client.RemoteEndPoint : null;

    public bool IsConnected => this.client.Connected;

    public bool IsBusy => this.busyStack > 0;

    public PhantomRTMConsoleConnection(TcpClient client, StreamReader stream) {
        this.client = client;
        this.stream = stream;
    }

    public void Dispose() {
        this.EnsureNotBusy();
        byte[] bytes = Encoding.ASCII.GetBytes("bye\r\n");
        this.client.GetStream().Write(bytes, 0, bytes.Length);
        this.isDisposed = true;
        this.client.Client.Close();
    }

    public async ValueTask<ConsoleResponse> SendCommand(string command) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        return await this.SendCommandCore(command);
    }

    public async ValueTask<List<string>> SendCommandAndReceiveLines(string command) {
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        ConsoleResponse response = await this.SendCommandCore(command);
        if (response.ResponseType == ResponseType.UnknownCommand) {
            throw new Exception("Unknown command: " + command);
        }

        if (response.ResponseType != ResponseType.MultiResponse) {
            return new List<string>();
        }

        List<string> list = new List<string>();
        string? line;
        while ((line = await this.stream.ReadLineAsync()) != "." && line != null) {
            list.Add(line);
        }

        return list;
    }

    public async ValueTask<List<KeyValuePair<string, string>>> SendCommandAndReceiveLines2(string Text) {
        return (await this.SendCommandAndReceiveLines(Text)).Select(x => {
            int split = x.IndexOf(':');
            return new KeyValuePair<string, string>(
                split == -1 ? x : x.AsSpan(0, split).Trim().ToString(),
                split == -1 ? "" : x.AsSpan(split + 1).Trim().ToString());
        }).ToList();
    }

    public async ValueTask<List<int>> GetThreads() {
        List<string> list = await this.SendCommandAndReceiveLines("threads");
        return list.Select(int.Parse).ToList();
        // foreach (string line in await this.SendCommandAndReceiveLines("threads")) {
        //     List<string> info = await this.SendCommandAndReceiveLines($"threadinfo thread=0x{int.Parse(line):X8}");
        //     info.Clear();
        // }
    }

    public async ValueTask<List<ConsoleThread>> GetThreadDump() {
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

    public async ValueTask RebootConsole(bool cold = true) {
        await this.SendCommand("magicboot" + (cold ? " cold" : ""));
        this.Dispose();
    }

    public async ValueTask ShutdownConsole() {
        await this.SendCommand("shutdown");
        this.Dispose();
    }

    public async ValueTask OpenDiskTray() {
        await this.SendCommand("dvdeject");
    }

    public async ValueTask DebugFreeze() {
        await this.SendCommand("stop");
    }

    public async ValueTask DebugUnFreeze() {
        await this.SendCommand("go");
    }

    public async ValueTask DeleteFile(string path) {
        string[] lines = path.Split("\\".ToCharArray());
        string Directory = "";
        for (int i = 0; i < (lines.Length - 1); i++)
            Directory += lines[i] + "\\";
        await this.SendCommand("delete title=\"" + path + "\" dir=\"" + Directory + "\"");
    }

    public async ValueTask LaunchFile(string path) {
        string[] lines = path.Split("\\".ToCharArray());
        string Directory = "";
        for (int i = 0; i < lines.Length - 1; i++)
            Directory += lines[i] + "\\";
        await this.SendCommand("magicboot title=\"" + path + "\" directory=\"" + Directory + "\"");
    }

    public async ValueTask<string> GetConsoleID() {
        return (await this.SendCommand("getconsoleid")).Message.Substring(10);
    }

    public async ValueTask<string> GetCPUKey() {
        return (await this.SendCommand("getcpukey")).Message;
    }

    public async ValueTask<string> GetDebugName() {
        return (await this.SendCommand("dbgname")).Message;
    }

    public async ValueTask<ExecutionState> GetExecutionState() {
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

    public async ValueTask<HardwareInfo> GetHardwareInfo() {
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

    public async ValueTask<uint> GetProcessID() {
        uint value = uint.Parse((await this.SendCommand("getpid")).Message.Substring(4).Replace("0x", ""), NumberStyles.HexNumber);
        return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    public async ValueTask<IPAddress> GetTitleIPAddress() {
        uint value = uint.Parse((await this.SendCommand("altaddr")).Message.Substring(5).Replace("0x", ""), NumberStyles.HexNumber);
        return new IPAddress(BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }

    public async ValueTask SetConsoleColor(ConsoleColor colour) {
        await this.SendCommand("setcolor name=" + colour.ToString().ToLower());
    }

    public async ValueTask SetDebugName(string newName) {
        await this.SendCommand("dbgname name=" + newName);
    }

    public async ValueTask ReadBytes(uint address, byte[] buffer, int offset, uint count) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        ConsoleResponse response = await this.SendCommandCore("getmem addr=0x" + address.ToString("X8") + " length=0x" + count.ToString("X8") + "\r\n");
        if (response.ResponseType != ResponseType.MultiResponse) {
            throw new Exception($"Xbox responded to getmem without {nameof(ResponseType.MultiResponse)}, which is unexpected");
        }

        byte[]? lineBytes = null;
        string? data;
        while ((data = await this.stream.ReadLineAsync(CancellationToken.None)) != ".") {
            int cbData = data!.Length / 2; // typically 128 when reading big chunks
            if (lineBytes == null || lineBytes.Length != cbData) {
                lineBytes = new byte[cbData];
            }

            for (int i = 0, j = 0; i < cbData; i++, j += 2) {
                if (data[j] == '?') {
                    lineBytes[i] = 0; // protected memory maybe?
                }
                else {
                    lineBytes[i] = (byte) ((CharToInteger(data[j]) << 4) | CharToInteger(data[j + 1]));
                }
            }

            Array.Copy(lineBytes, 0, buffer, offset, cbData);
            offset += cbData;
        }

        ConsoleResponse failedResponse = await this.ReadResponseCore();
        if (failedResponse.ResponseType != ResponseType.UnknownCommand) {
            Debug.Assert(false, "What is this bullshit who patched this bug???");
        }
    }

    public async ValueTask<byte[]> ReadBytes(uint address, uint count) {
        byte[] buffer = new byte[count];
        await this.ReadBytes(address, buffer, 0, count);
        return buffer;
    }

    public async ValueTask<byte> ReadByte(uint Offset) {
        await this.ReadBytes(Offset, ONE_BYTE, 0, 1);
        return ONE_BYTE[0];
    }

    public async ValueTask<bool> ReadBool(uint address) => await this.ReadByte(address) != 0;

    public async ValueTask<char> ReadChar(uint address) => (char) await this.ReadByte(address);

    public async ValueTask<T> ReadValue<T>(uint address) where T : unmanaged {
        byte[] buffer = await this.ReadBytes(address, (uint) Unsafe.SizeOf<T>());
        if (BitConverter.IsLittleEndian)
            Array.Reverse(buffer);
        return Unsafe.ReadUnaligned<T>(ref buffer[0]);
    }

    public async ValueTask<T> ReadStruct<T>(uint address, params int[] fields) where T : unmanaged {
        if (!BitConverter.IsLittleEndian) {
            // TODO: I don't have a big endian computer nor a big enough brain to know if this works
            return await this.ReadValue<T>(address);
        }

        int offset = 0;
        byte[] buffer = new byte[Unsafe.SizeOf<T>()];
        foreach (int cbField in fields) {
            await this.ReadBytes((uint) (address + offset), buffer, offset, (uint) cbField);
            Array.Reverse(buffer, offset, cbField);
            offset += cbField;
        }

        if (offset != buffer.Length) {
            Debugger.Break();
        }

        return Unsafe.ReadUnaligned<T>(ref buffer[0]);
    }

    public async ValueTask<string> ReadString(uint address, uint count) {
        byte[] buffer = await this.ReadBytes(address, count);
        return Encoding.ASCII.GetString(buffer);
    }

    public async ValueTask WriteBytes(uint address, byte[] bytes) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        await this.WriteBytesAndGetResponseInternal(address, bytes);
    }

    public ValueTask WriteByte(uint address, byte value) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        ONE_BYTE[0] = value;
        return this.WriteBytes(address, ONE_BYTE);
    }

    public ValueTask WriteBool(uint address, bool value) {
        return this.WriteByte(address, (byte) (value ? 0x01 : 0x00));
    }

    public ValueTask WriteChar(uint address, char value) {
        return this.WriteByte(address, (byte) value);
    }

    public ValueTask WriteValue<T>(uint address, T value) where T : unmanaged {
        byte[] bytes = new byte[Unsafe.SizeOf<T>()];
        Unsafe.As<byte, T>(ref bytes[0]) = value;
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);

        return this.WriteBytes(address, bytes);
    }

    public async ValueTask WriteStruct<T>(uint address, T value, params int[] fields) where T : unmanaged {
        if (!BitConverter.IsLittleEndian) {
            // TODO: I don't have a big endian computer nor a big enough brain to know if this works
            await this.WriteValue<T>(address, value);
        }

        int offset = 0;
        byte[] buffer = new byte[Unsafe.SizeOf<T>()];
        foreach (int cbField in fields) {
            Unsafe.As<byte, T>(ref buffer[offset]) = value;
            Array.Reverse(buffer, offset, cbField);
            offset += cbField;
        }

        if (offset != buffer.Length) {
            Debugger.Break();
        }

        await this.WriteBytes(address, buffer);
    }

    public ValueTask WriteString(uint address, string value) {
        return this.WriteBytes(address, Encoding.ASCII.GetBytes(value));
    }

    public async ValueTask WriteFile(uint address, string filePath) {
        this.EnsureNotDisposed();
        this.EnsureNotBusy();
        using BusyToken x = this.CreateBusyToken();

        byte[] buffer = await File.ReadAllBytesAsync(filePath);
        await this.WriteBytesAndGetResponseInternal(address, buffer);
    }

    public ValueTask WriteHook(uint address, uint destination, bool isLinked) {
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

    public ValueTask WriteNOP(uint address) {
        return this.WriteBytes(address, [0x60, 0x00, 0x00, 0x00]);
    }

    private async ValueTask<ConsoleResponse> ReadResponseCore() {
        string responseText = await this.stream.ReadLineAsync() ?? "";
        return ConsoleResponse.FromFirstLine(responseText);
    }

    private async ValueTask WriteBytesAndGetResponseInternal(uint address, byte[] bytes) {
        string str = "setmem addr=0x" + address.ToString("X8") + " data=";
        foreach (byte b in bytes)
            str += b.ToString("X2");
        str += "\r\n";

        byte[] buffer = Encoding.ASCII.GetBytes(str);
        await this.client.GetStream().WriteAsync(buffer);
        ConsoleResponse response = await this.ReadResponseCore();
        if (response.ResponseType != ResponseType.SingleResponse) {
            throw new Exception($"Xbox responded to setmem without {nameof(ResponseType.SingleResponse)}, which is unexpected");
        }
    }

    private async ValueTask<ConsoleResponse> SendCommandCore(string command) {
        byte[] buffer = Encoding.ASCII.GetBytes(command + "\r\n");
        await this.client.GetStream().WriteAsync(buffer);
        ConsoleResponse response = await this.ReadResponseCore();
        if (response.ResponseType == ResponseType.UnknownCommand) {
            // Sometimes the xbox randomly says unknown command for specific things
            if (this.client.Available > 0) {
                string responseText = await this.stream.ReadLineAsync() ?? "";
                response = ConsoleResponse.FromFirstLine(responseText);
            }
        }

        return response;
    }

    private BusyToken CreateBusyToken() {
        Interlocked.Increment(ref this.busyStack);
        return new BusyToken(this);
    }

    private void EnsureNotBusy() {
        if (this.busyStack > 0) {
            throw new InvalidOperationException("Busy performing another operation");
        }
    }

    private void EnsureNotDisposed() {
        if (this.isDisposed) {
            throw new ObjectDisposedException(nameof(PhantomRTMConsoleConnection), "Connection is disposed");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CharToInteger(char c) => c <= '9' ? (c - '0') : ((c & ~0x20 /* LOWER TO UPPER CASE */) - 'A' + 10);

    public ValueTask WriteVector2(uint Offset, Vector2 Vector2) {
        byte[] bytes = new byte[8];
        byte[] x = BitConverter.GetBytes(Vector2.X);
        byte[] y = BitConverter.GetBytes(Vector2.Y);
        if (BitConverter.IsLittleEndian) {
            Array.Reverse(x);
            Array.Reverse(y);
        }

        Array.Copy(x, 0, bytes, 0, 4);
        Array.Copy(y, 0, bytes, 4, 4);
        return this.WriteBytes(Offset, bytes);
    }

    public ValueTask WriteVector3(uint Offset, Vector3 Vector3) {
        byte[] bytes = new byte[12];
        byte[] x = BitConverter.GetBytes(Vector3.X);
        byte[] y = BitConverter.GetBytes(Vector3.Y);
        byte[] z = BitConverter.GetBytes(Vector3.Z);
        if (BitConverter.IsLittleEndian) {
            Array.Reverse(x);
            Array.Reverse(y);
            Array.Reverse(z);
        }

        Array.Copy(x, 0, bytes, 0, 4);
        Array.Copy(y, 0, bytes, 4, 4);
        Array.Copy(z, 0, bytes, 8, 4);
        return this.WriteBytes(Offset, bytes);
    }

    // Is getmemex even for reading RAM? it could be reading shit from usb which
    // is why when you request 0x400 bytes, you can only read like 80 or something arbitrary 
    public async ValueTask<byte[]> ReadBytesEx_BARELY_WORKS_ReadMemoryInDataOrSomething(uint address, uint count) {
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
                
                ConsoleResponse response = await this.SendCommandCore(command);
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
                ConsoleResponse response = await this.SendCommandCore(command);
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
}