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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MemEngine360.Connections;
using MemEngine360.XboxBase;
using PFXToolKitUI.Utils;

namespace MemEngine360.Xbox360XBDM.Consoles.Xbdm;

// Rewrite with fixes and performance improvements, based on:
// https://github.com/XeClutch/Cheat-Engine-For-Xbox-360/blob/master/Cheat%20Engine%20for%20Xbox%20360/PhantomRTM.cs

public class PhantomRTMConsoleConnection : BaseConsoleConnection, IXbdmConnection, IHavePowerFunctions, IXboxDebuggable {
    private readonly byte[] sharedSetMemCommandBuffer = new byte[14 + 8 + 6 + 128 + 2];
    private readonly byte[] sharedGetMemCommandBuffer = new byte[14 + 8 + 10 + 8 + 2];

    private readonly TcpClient client;
    private readonly StreamReader stream;

    public EndPoint? EndPoint => this.IsConnected ? this.client.Client.RemoteEndPoint : null;

    public override RegisteredConsoleType ConsoleType => ConsoleTypeXbox360Xbdm.Instance;

    protected override bool IsConnectedCore => this.client.Connected;

    public override bool IsLittleEndian => false;

    public PhantomRTMConsoleConnection(TcpClient client, StreamReader stream) {
        this.client = client;
        this.stream = stream;

        "setmem addr=0x"u8.CopyTo(new Span<byte>(this.sharedSetMemCommandBuffer, 0, 14));
        " data="u8.CopyTo(new Span<byte>(this.sharedSetMemCommandBuffer, 22, 6));
        "getmem addr=0x"u8.CopyTo(new Span<byte>(this.sharedGetMemCommandBuffer, 0, 14));
        " length=0x"u8.CopyTo(new Span<byte>(this.sharedGetMemCommandBuffer, 22, 10));
    }

    protected override void CloseCore() {
        if (this.IsConnected) {
            this.client.GetStream().Write("bye\r\n"u8);
            this.client.Client.Close();
        }
    }

    public async Task<ConsoleResponse> SendCommand(string command) {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        return await this.SendCommandAndGetResponse(command).ConfigureAwait(false);
    }

    private async ValueTask<string> ReadLineFromStream(CancellationToken token = default) {
        string? result;
        try {
            result = await this.stream.ReadLineAsync(token).ConfigureAwait(false);
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

    /// <summary>
    /// Sends a command and receives a multi-line response
    /// </summary>
    /// <param name="command">The command to send</param>
    /// <returns></returns>
    /// <exception cref="Exception">No such command or response was not a multi-line response</exception>
    public async Task<List<string>> SendCommandAndReceiveLines(string command) {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        ConsoleResponse response = await this.SendCommandAndGetResponse(command).ConfigureAwait(false);
        if (response.ResponseType == ResponseType.UnknownCommand) {
            throw new Exception("Unknown command: " + command);
        }

        if (response.ResponseType != ResponseType.MultiResponse) {
            throw new Exception("Command response is not multi-response: " + command);
        }

        return await this.ReadMultiLineResponseInternal();
    }

    /// <summary>
    /// Reads lines
    /// </summary>
    /// <returns></returns>
    /// <exception cref="IOException"></exception>
    public async Task<List<string>> ReadMultiLineResponse() {
        this.EnsureNotDisposed();
        using BusyToken x = this.CreateBusyToken();

        return await this.ReadMultiLineResponseInternal();
    }

    private async Task<List<string>> ReadMultiLineResponseInternal() {
        List<string> list = new List<string>();
        try {
            string line;
            while ((line = await this.ReadLineFromStream().ConfigureAwait(false)) != ".") {
                list.Add(line);
            }
        }
        catch (IOException) {
            this.client.Client.Close();
            this.isClosed = true;
            throw;
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

    public Task OpenDiskTray() {
        return this.SendCommand("dvdeject");
    }

    public Task DebugFreeze() {
        return this.SendCommand("stop");
    }

    public Task DebugUnFreeze() {
        return this.SendCommand("go");
    }

    public async Task DeleteFile(string path) {
        string[] lines = path.Split("\\".ToCharArray());
        string Directory = "";
        for (int i = 0; i < (lines.Length - 1); i++)
            Directory += (lines[i] + "\\");
        await this.SendCommand("delete title=\"" + path + "\" dir=\"" + Directory + "\"").ConfigureAwait(false);
    }

    public async Task LaunchFile(string path) {
        string[] lines = path.Split("\\".ToCharArray());
        string Directory = "";
        for (int i = 0; i < lines.Length - 1; i++)
            Directory += lines[i] + "\\";
        await this.SendCommand("magicboot title=\"" + path + "\" directory=\"" + Directory + "\"").ConfigureAwait(false);
    }

    public async Task<string> GetConsoleID() {
        return (await this.SendCommand("getconsoleid").ConfigureAwait(false)).Message.Substring(10);
    }

    public async Task<string> GetCPUKey() {
        return (await this.SendCommand("getcpukey").ConfigureAwait(false)).Message;
    }

    public async Task<string> GetDebugName() {
        return (await this.SendCommand("dbgname").ConfigureAwait(false)).Message;
    }

    public async Task<string?> GetXbeInfo(string? executable) {
        List<string> result = await this.SendCommandAndReceiveLines($"xbeinfo {(executable != null ? ("name=" + executable) : "running")}").ConfigureAwait(false);
        foreach (string line in result) {
            if (line.StartsWith("name=")) {
                return line.Substring(6, line.Length - 7);
            }
        }

        return null;
    }

    public async Task<List<MemoryRegion>> GetMemoryRegions(bool willRead, bool willWrite) {
        List<string> list = await this.SendCommandAndReceiveLines("walkmem").ConfigureAwait(false);
        List<MemoryRegion> newList = new List<MemoryRegion>(list.Count);
        foreach (string line in list) {
            // base=0x00000000 size=0x00000000 protect=0x00000000 phys=0x00000000
            uint p = uint.Parse(line.AsSpan(42, 8), NumberStyles.HexNumber);

            // Both flags contain NoCache because we cannot clear memory caches via XBDM,
            // therefore, readers and writers just can't read from/write to the region
            // without risking freezing the console.
            if (willWrite && (p & 0x1222 /* UserReadOnly | NoCache | ReadOnly | ExecuteRead */) != 0)
                continue;
            if (willRead && (p & 0x280 /* NoCache | ExecuteWriteCopy */) != 0)
                continue;

            uint propBase = uint.Parse(line.AsSpan(7, 8), NumberStyles.HexNumber);
            uint propSize = uint.Parse(line.AsSpan(23, 8), NumberStyles.HexNumber);
            uint propPhys = uint.Parse(line.AsSpan(58, 8), NumberStyles.HexNumber);
            newList.Add(new MemoryRegion(propBase, propSize, p, propPhys));
        }

        return newList;
    }

    public async Task<ExecutionState> GetExecutionState() {
        string str = (await this.SendCommand("getexecstate").ConfigureAwait(false)).Message;
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
        uint value = uint.Parse((await this.SendCommand("getpid").ConfigureAwait(false)).Message.Substring(4).Replace("0x", ""), NumberStyles.HexNumber);
        return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    public async Task<IPAddress> GetTitleIPAddress() {
        uint value = uint.Parse((await this.SendCommand("altaddr").ConfigureAwait(false)).Message.Substring(5).Replace("0x", ""), NumberStyles.HexNumber);
        return new IPAddress(BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }

    public async Task SetConsoleColor(ConsoleColor colour) {
        await this.SendCommand("setcolor name=" + colour.ToString().ToLower()).ConfigureAwait(false);
    }

    public async Task SetDebugName(string newName) {
        await this.SendCommand("dbgname name=" + newName).ConfigureAwait(false);
    }

    protected override async Task<uint> ReadBytesCore(uint address, byte[] dstBuffer, int offset, uint count) {
        if (count == 0) {
            return 0;
        }

        this.FillGetMemCommandBuffer(address, count);
        await this.WriteCommandBytes(this.sharedGetMemCommandBuffer).ConfigureAwait(false);
        ConsoleResponse response = await this.ReadResponseCore().ConfigureAwait(false);
        if (response.ResponseType != ResponseType.MultiResponse) {
            throw new IOException($"Xbox responded to getmem with {response.ResponseType} instead of {nameof(ResponseType.MultiResponse)}, which is unexpected");
        }

        uint cbRead = 0;
        byte[]? lineBytes = null;
        string line;
        while ((line = await this.ReadLineFromStream().ConfigureAwait(false)) != ".") {
            uint cbLine = (uint) (line.Length / 2); // typically 128 when reading big chunks
            if (lineBytes == null || lineBytes.Length != cbLine) {
                lineBytes = new byte[cbLine];
            }

            for (int i = 0, j = 0; i < cbLine; i++, j += 2) {
                if (line[j] == '?') {
                    lineBytes[i] = 0; // protected memory maybe?
                }
                else {
                    lineBytes[i] = (byte) ((NumberUtils.HexCharToInt(line[j]) << 4) | NumberUtils.HexCharToInt(line[j + 1]));
                }
            }

            Array.Copy(lineBytes, 0, dstBuffer, offset + cbRead, cbLine);
            cbRead += cbLine;
        }

        return cbRead;
    }

    protected override async Task<uint> WriteBytesCore(uint address, byte[] srcBuffer, int offset, uint count) {
        while (count > 0) {
            uint cbWrite = Math.Min(count, 64 /* Fixed Chunk Size */);
            this.FillSetMemCommandBuffer(address, srcBuffer, offset, count);
            await this.WriteCommandBytes(new ReadOnlyMemory<byte>(this.sharedSetMemCommandBuffer, 0, (int) (30 + (count << 1)))).ConfigureAwait(false);
            ConsoleResponse response = await this.ReadResponseCore().ConfigureAwait(false);
            if (response.ResponseType != ResponseType.SingleResponse && response.ResponseType != ResponseType.MemoryNotMapped) {
                throw new IOException($"Xbox responded to setmem without {nameof(ResponseType.SingleResponse)}, which is unexpected");
            }

            address += cbWrite;
            offset += (int) cbWrite;
            count -= cbWrite;
        }

        return count;
    }

    private async Task<ConsoleResponse> ReadResponseCore() {
        string responseText = await this.ReadLineFromStream().ConfigureAwait(false);
        return ConsoleResponse.FromFirstLine(responseText);
    }

    private Task WriteCommandText(string command) {
        // Sending duplicate CRLF results in effectively two commands being sent.
        if (!command.EndsWith("\r\n", StringComparison.Ordinal)) {
            command += "\r\n";
        }

        return this.WriteCommandBytes(Encoding.ASCII.GetBytes(command));
    }

    private async Task WriteCommandBytes(ReadOnlyMemory<byte> buffer) {
        try {
            await this.client.GetStream().WriteAsync(buffer).ConfigureAwait(false);
        }
        catch (IOException) {
            this.client.Client.Close();
            this.isClosed = true;
            throw;
        }
    }

    private async Task<ConsoleResponse> SendCommandAndGetResponse(string command) {
        await this.WriteCommandText(command).ConfigureAwait(false);
        ConsoleResponse response = await this.ReadResponseCore().ConfigureAwait(false);
        if (response.ResponseType == ResponseType.UnknownCommand) {
            if (this.client.Available > 0) {
                Debugger.Break(); // this was originally to fix an issue where we sent
                // extra \r\n but we fixed that so this checking shouldn't be necessary
                string responseText = await this.ReadLineFromStream().ConfigureAwait(false) ?? "";
                response = ConsoleResponse.FromFirstLine(responseText);
            }
        }

        return response;
    }

    // Using this over-optimised version of creating the command buffer
    // allows us to save roughly 0.2 seconds when writing about 1MB. 
    // That's maybe a 10% improvement over string concat? Not too bad...
    // Who TF's gonna write an entire megabyte though???
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void FillSetMemCommandBuffer(uint address, byte[] srcData, int srcOffset, uint cbData) {
        ref byte dstAscii = ref MemoryMarshal.GetArrayDataReference(this.sharedSetMemCommandBuffer);
        NumberUtils.UInt32ToHexAscii(address, ref dstAscii, 14);

        int i = 28;
        ref byte hexChars = ref MemoryMarshal.GetArrayDataReference(NumberUtils.HEX_CHARS_ASCII);
        for (int j = 0; j < cbData; j++, i += 2) {
            byte b = srcData[srcOffset + j];
            Unsafe.AddByteOffset(ref dstAscii, i + 0) = Unsafe.ReadUnaligned<byte>(ref Unsafe.AddByteOffset(ref hexChars, b >> 4));
            Unsafe.AddByteOffset(ref dstAscii, i + 1) = Unsafe.ReadUnaligned<byte>(ref Unsafe.AddByteOffset(ref hexChars, b & 0xF));
        }

        Unsafe.AddByteOffset(ref dstAscii, i + 0) = (byte) '\r';
        Unsafe.AddByteOffset(ref dstAscii, i + 1) = (byte) '\n';
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void FillGetMemCommandBuffer(uint address, uint count) {
        ref byte dstAscii = ref MemoryMarshal.GetArrayDataReference(this.sharedGetMemCommandBuffer);
        NumberUtils.UInt32ToHexAscii(address, ref dstAscii, 14);
        NumberUtils.UInt32ToHexAscii(count, ref dstAscii, 32);
        Unsafe.AddByteOffset(ref dstAscii, 40) = (byte) '\r';
        Unsafe.AddByteOffset(ref dstAscii, 41) = (byte) '\n';
    }

    public Task AddBreakpoint(uint address) {
        return this.SetBreakpoint(address, false);
    }

    public Task AddDataBreakpoint(uint address, XboxBreakpointType type, uint size) {
        return this.SetDataBreakpoint(address, type, size, false);
    }

    public Task RemoveBreakpoint(uint address) {
        return this.SetBreakpoint(address, true);
    }

    public Task RemoveDataBreakpoint(uint address, XboxBreakpointType type, uint size) {
        return this.SetDataBreakpoint(address, type, size, true);
    }

    public async Task SetBreakpoint(uint address, bool clear) {
        this.EnsureNotDisposed();
        using BusyToken token = this.CreateBusyToken();

        await this.SendCommand($"break addr=0x{address:X8}{(clear ? " clear" : "")}");
    }

    public async Task SetDataBreakpoint(uint address, XboxBreakpointType type, uint size, bool clear) {
        this.EnsureNotDisposed();
        using BusyToken token = this.CreateBusyToken();

        string strType;
        switch (type) {
            case XboxBreakpointType.None:
            case XboxBreakpointType.OnWrite:
                strType = "write";
                break;
            case XboxBreakpointType.OnRead: 
                strType = "read"; 
                break;
            case XboxBreakpointType.OnExecuteHW:
            case XboxBreakpointType.OnExecute:
                strType = "execute";
                break;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        await this.SendCommand($"break {strType}=0x{address:X8} size=0x{size:X8}{(clear ? " clear" : "")}");
    }
}