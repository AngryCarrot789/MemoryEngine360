using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using MemEngine360.Connections.Impl.Threads;

namespace MemEngine360.Connections.Impl;

// https://github.com/XeClutch/Cheat-Engine-For-Xbox-360
public class PhantomRTMConsoleConnection : IConsoleConnection {
    private readonly struct BusyToken : IDisposable {
        private readonly PhantomRTMConsoleConnection connection;

        public BusyToken(PhantomRTMConsoleConnection connection) {
            this.connection = connection;
            this.connection.busyStack++;
        }

        public void Dispose() {
            this.connection.busyStack--;
        }
    }

    // Structures

    // Variables
    private readonly TcpClient client;
    private readonly StreamReader stream;
    private bool isConnected;
    private int busyStack;

    public EndPoint? EndPoint => this.client.Connected ? this.client.Client.RemoteEndPoint : null;

    public bool IsReallyConnected => this.client.Connected;

    public bool IsBusy => this.busyStack > 0;

    public PhantomRTMConsoleConnection(TcpClient client, StreamReader stream) {
        this.client = client;
        this.stream = stream;
        this.isConnected = true;
    }

    private BusyToken CreateBusyToken() => new(this);
    
    private void EnsureNotBusy() {
        if (this.busyStack > 0) {
            throw new InvalidOperationException("Busy performing another operation");
        }
    }

    public void Dispose() {
        this.EnsureNotBusy();
        byte[] bytes = Encoding.ASCII.GetBytes("bye\r\n");
        this.client.GetStream().Write(bytes, 0, bytes.Length);
        this.isConnected = false;
        this.client.Client.Close();
    }

    private async ValueTask<ConsoleResponse> ReadResponseCore() {
        string responseText = await this.stream.ReadLineAsync() ?? "";
        return ConsoleResponse.FromFirstLine(responseText);
    }

    private async Task<ConsoleResponse> SendCommandCore(string command) {
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

    public async Task<ConsoleResponse> SendCommand(string command) {
        if (!this.isConnected)
            throw new ObjectDisposedException("Connection is closed");

        this.EnsureNotBusy();
        using BusyToken x = CreateBusyToken();

        return await this.SendCommandCore(command);
    }

    public async Task<List<string>> SendCommandAndReceiveLines(string Text) {
        this.EnsureNotBusy();
        using BusyToken x = CreateBusyToken();

        ConsoleResponse response = await this.SendCommandCore(Text);
        if (response.ResponseType != ResponseType.UnknownCommand && response.ResponseType != ResponseType.MultiResponse) {
            return new List<string>();
        }

        List<string> list = new List<string>();
        string? line;
        while ((line = await this.stream.ReadLineAsync()) != "." && line != null) {
            list.Add(line);
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

    public async Task Reboot() {
        await this.SendCommand("magicboot cold");
        this.Dispose();
    }

    public async Task ShutdownConsole() {
        await this.SendCommand("shutdown");
        this.Dispose();
    }

    public async Task Eject() {
        await this.SendCommand("dvdeject");
    }

    public async Task DebugFreeze() {
        await this.SendCommand("stop");
    }

    public async Task DebugUnFreeze() {
        await this.SendCommand("go");
    }

    public async Task DeleteFile(string Path) {
        string[] lines = Path.Split("\\".ToCharArray());
        string Directory = "";
        for (int i = 0; i < (lines.Length - 1); i++)
            Directory += lines[i] + "\\";
        await this.SendCommand("delete title=\"" + Path + "\" dir=\"" + Directory + "\"");
    }

    public async Task LaunchFile(string Path) {
        string[] lines = Path.Split("\\".ToCharArray());
        string Directory = "";
        for (int i = 0; i < lines.Length - 1; i++)
            Directory += lines[i] + "\\";
        await this.SendCommand("magicboot title=\"" + Path + "\" directory=\"" + Directory + "\"");
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

    public async Task<uint> GetTitleAddress() {
        uint value = uint.Parse((await this.SendCommand("altaddr")).Message.Substring(5).Replace("0x", ""), NumberStyles.HexNumber);
        return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    public async Task<bool> ReadBool(uint Offset) {
        return await this.ReadByte(Offset) != 0;
    }

    public async Task<byte> ReadByte(uint Offset) {
        return (await this.ReadBytes(Offset, 1))[0];
    }

    public async Task<byte[]> ReadBytes(uint address, uint count) {
        if (!this.isConnected)
            throw new ObjectDisposedException("Connection is closed");

        this.EnsureNotBusy();
        using BusyToken x = CreateBusyToken();

        ConsoleResponse response = await this.SendCommandCore("getmem addr=0x" + address.ToString("X8") + " length=0x" + count.ToString("X8") + "\r\n");
        if (response.ResponseType != ResponseType.MultiResponse) {
            return new byte[count];
        }

        // Using the dual task technique does not necessarily improve performance, since
        // most of the time we are just waiting for the xbox to send more data over tcp.
        // long begin = Time.GetSystemTicks();
        // CancellationTokenSource cts = new CancellationTokenSource();
        // ConcurrentQueue<string> lines = new ConcurrentQueue<string>();
        // Task<byte[]> processLinesTask = Task.Run(async () => {
        //     byte[] output = new byte[count];
        //     byte[]? lineBytes = null;
        //     int offset = 0;
        //     while (true) {
        //         if (cts.IsCancellationRequested && lines.IsEmpty) {
        //             break;
        //         }
        //         
        //         while (lines.TryDequeue(out string? line)) {
        //             int hexTextCount = line.Length / 2; // typically 128 when reading big chunks
        //             if (lineBytes == null || lineBytes.Length != hexTextCount) {
        //                 lineBytes = new byte[hexTextCount];
        //             }
        //             
        //             for (int i = 0, j = 0; i < hexTextCount; i++, j += 2) {
        //                 if (line[j] == '?')
        //                     continue; // protected memory maybe?
        //                 lineBytes[i] = byte.TryParse(line.AsSpan(j, 2), NumberStyles.HexNumber, null, out byte b) ? b : (byte) 0;
        //             }
        //             
        //             Array.Copy(lineBytes, 0, output, offset, hexTextCount);
        //             offset += hexTextCount;
        //         }
        //         
        //         await Task.Yield();
        //     }
        //     return output;
        // });
        // 
        // string? hexText;
        // while ((hexText = await this.stream.ReadLineAsync(CancellationToken.None)) != ".") {
        //     int available = this.client.Available;
        //     lines.Enqueue(hexText!);
        // }
        // 
        // await cts.CancelAsync();

        byte[] output = new byte[count];
        byte[]? lineBytes = null;
        int offset = 0;
        string? hexText;
        while ((hexText = await this.stream.ReadLineAsync(CancellationToken.None)) != ".") {
            int hexTextCount = hexText!.Length / 2; // typically 128 when reading big chunks
            if (lineBytes == null || lineBytes.Length != hexTextCount) {
                lineBytes = new byte[hexTextCount];
            }

            for (int i = 0, j = 0; i < hexTextCount; i++, j += 2) {
                if (hexText[j] == '?')
                    continue; // protected memory maybe? 

                lineBytes[i] = byte.TryParse(hexText.AsSpan(j, 2), NumberStyles.HexNumber, null, out byte b) ? b : (byte) 0;
            }

            Array.Copy(lineBytes, 0, output, offset, hexTextCount);
            offset += hexTextCount;
        }

        ConsoleResponse failedResponse = await this.ReadResponseCore();
        if (failedResponse.ResponseType != ResponseType.UnknownCommand) {
            Debug.Assert(false, "What is this bullshit who patched this bug???");
        }

        return output;
        // return await processLinesTask;
    }

    // Cannot get it to work nicely
    public async Task<byte[]> ReadBytesEx_BARELY_WORKS(uint address, uint count) {
        if (!this.isConnected)
            throw new ObjectDisposedException("Connection is closed");

        this.EnsureNotBusy();
        using BusyToken x = CreateBusyToken();
        
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

                ushort value = BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(output, 0, 2));
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

                ushort value = BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(output, 0, 2));
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

    public async Task<char> ReadChar(uint Offset) {
        return (char) await this.ReadByte(Offset);
    }

    public async Task<double> ReadDouble(uint Offset) {
        byte[] buffer = await this.ReadBytes(Offset, 8);
        Array.Reverse(buffer);
        return BitConverter.ToDouble(buffer, 0);
    }

    public async Task<float> ReadFloat(uint Offset) {
        byte[] buffer = await this.ReadBytes(Offset, 4);
        Array.Reverse(buffer);
        return BitConverter.ToSingle(buffer, 0);
    }

    public async Task<short> ReadInt16(uint Offset) {
        byte[] buffer = await this.ReadBytes(Offset, 2);
        Array.Reverse(buffer);
        return BitConverter.ToInt16(buffer, 0);
    }

    public async Task<int> ReadInt32(uint Offset) {
        byte[] buffer = await this.ReadBytes(Offset, 4);
        Array.Reverse(buffer);
        return BitConverter.ToInt32(buffer, 0);
    }

    public async Task<long> ReadInt64(uint Offset) {
        byte[] buffer = await this.ReadBytes(Offset, 8);
        Array.Reverse(buffer);
        return BitConverter.ToInt64(buffer, 0);
    }

    public async Task<sbyte> ReadSByte(uint Offset) {
        return (sbyte) await this.ReadByte(Offset);
    }

    public async Task<string> ReadString(uint Offset, uint Length) {
        byte[] buffer = await this.ReadBytes(Offset, Length);
        string str = "";
        foreach (byte b in buffer)
            str += b.ToString("X2");
        return HexString2Ascii(str);
    }

    public async Task<ushort> ReadUInt16(uint Offset) {
        byte[] buffer = await this.ReadBytes(Offset, 2);
        Array.Reverse(buffer);
        return BitConverter.ToUInt16(buffer, 0);
    }

    public async Task<uint> ReadUInt32(uint Offset) {
        byte[] buffer = await this.ReadBytes(Offset, 4);
        Array.Reverse(buffer);
        return BitConverter.ToUInt32(buffer, 0);
    }

    public async Task<ulong> ReadUInt64(uint Offset) {
        byte[] buffer = await this.ReadBytes(Offset, 8);
        Array.Reverse(buffer);
        return BitConverter.ToUInt64(buffer, 0);
    }

    public async Task<Vector2> ReadVector2(uint Offset) {
        Vector2 vec;
        vec.X = await this.ReadFloat(Offset);
        vec.Y = await this.ReadFloat(Offset + 4);
        return vec;
    }

    public async Task<Vector3> ReadVector3(uint Offset) {
        Vector3 vec;
        vec.X = await this.ReadFloat(Offset);
        vec.Y = await this.ReadFloat(Offset + 4);
        vec.Z = await this.ReadFloat(Offset + 8);
        return vec;
    }

    public async Task SetConsoleColor(ConsoleColor Color) {
        await this.SendCommand("setcolor name=" + Color.ToString().ToLower());
    }

    public async Task SetDebugName(string DebugName) {
        await this.SendCommand("dbgname name=" + DebugName);
    }

    public Task WriteBool(uint Offset, bool Bool) {
        return this.WriteByte(Offset, (byte) (Bool ? 0x01 : 0x00));
    }

    public Task WriteByte(uint Offset, byte Byte) {
        return this.WriteByte(Offset, [Byte]);
    }

    public async Task WriteByte(uint Offset, byte[] Bytes) {
        if (!this.isConnected)
            throw new ObjectDisposedException("This connection has been closed");
        
        this.EnsureNotBusy();
        using BusyToken x = CreateBusyToken();
        
        string str = "setmem addr=0x" + Offset.ToString("X8") + " data=";
        foreach (byte b in Bytes)
            str += b.ToString("X2");
        str += "\r\n";

        byte[] buffer = Encoding.ASCII.GetBytes(str);
        await this.client.GetStream().WriteAsync(buffer);
        ConsoleResponse response = await this.ReadResponseCore();
    }

    public Task WriteChar(uint Offset, char Char) {
        return this.WriteByte(Offset, (byte) Char);
    }

    public Task WriteDouble(uint Offset, double Double) {
        byte[] bytes = BitConverter.GetBytes(Double);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return this.WriteByte(Offset, bytes);
    }

    public Task WriteFile(uint Offset, string Path) {
        return this.WriteByte(Offset, File.ReadAllBytes(Path));
    }

    public Task WriteFloat(uint Offset, float Float) {
        byte[] bytes = BitConverter.GetBytes(Float);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return this.WriteByte(Offset, bytes);
    }

    public Task WriteHook(uint Offset, uint Destination, bool Linked) {
        uint[] Func = new uint[4];
        if ((Destination & 0x8000) != 0)
            Func[0] = 0x3D600000 + (((Destination >> 16) & 0xFFFF) + 1);
        else
            Func[0] = 0x3D600000 + ((Destination >> 16) & 0xFFFF);
        Func[1] = 0x396B0000 + (Destination & 0xFFFF);
        Func[2] = 0x7D6903A6;
        Func[3] = 0x4E800420;
        if (Linked)
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
        return this.WriteByte(Offset, buffer);
    }

    public Task WriteInt16(uint Offset, short Int16) {
        byte[] bytes = BitConverter.GetBytes(Int16);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return this.WriteByte(Offset, bytes);
    }

    public Task WriteInt32(uint Offset, int Int32) {
        byte[] bytes = BitConverter.GetBytes(Int32);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return this.WriteByte(Offset, bytes);
    }

    public Task WriteInt64(uint Offset, long Int64) {
        byte[] bytes = BitConverter.GetBytes(Int64);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return this.WriteByte(Offset, bytes);
    }

    public Task WriteNOP(uint Offset) {
        return this.WriteByte(Offset, [0x60, 0x00, 0x00, 0x00]);
    }

    public Task WriteString(uint Offset, string String) {
        return this.WriteByte(Offset, Encoding.ASCII.GetBytes(String));
    }

    public Task WriteSByte(uint Offset, sbyte SByte) {
        return this.WriteByte(Offset, [(byte) SByte]);
    }

    public Task WriteUInt16(uint Offset, ushort UInt16) {
        byte[] bytes = BitConverter.GetBytes(UInt16);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return this.WriteByte(Offset, bytes);
    }

    public Task WriteUInt32(uint Offset, uint UInt32) {
        byte[] bytes = BitConverter.GetBytes(UInt32);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return this.WriteByte(Offset, bytes);
    }

    public Task WriteUInt64(uint Offset, ulong UInt64) {
        byte[] bytes = BitConverter.GetBytes(UInt64);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return this.WriteByte(Offset, bytes);
    }

    public Task WriteVector2(uint Offset, Vector2 Vector2) {
        byte[] bytes = new byte[8];
        byte[] x = BitConverter.GetBytes(Vector2.X);
        byte[] y = BitConverter.GetBytes(Vector2.Y);
        if (BitConverter.IsLittleEndian) {
            Array.Reverse(x);
            Array.Reverse(y);
        }

        Array.Copy(x, 0, bytes, 0, 4);
        Array.Copy(y, 0, bytes, 4, 4);
        return this.WriteByte(Offset, bytes);
    }

    public Task WriteVector3(uint Offset, Vector3 Vector3) {
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
        return this.WriteByte(Offset, bytes);
    }

    private static string HexString2Ascii(string hexString) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i <= hexString.Length - 2; i += 2)
            sb.Append((char) (uint) int.Parse(hexString.AsSpan(i, 2), NumberStyles.HexNumber));
        return sb.ToString();
    }
}