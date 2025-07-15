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

using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MemEngine360.Connections;
using MemEngine360.Connections.Traits;
using MemEngine360.Connections.Utils;
using MemEngine360.Engine.Debugging;
using MemEngine360.Engine.Events.XbdmEvents;
using MemEngine360.XboxBase;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Destroying;

namespace MemEngine360.Xbox360XBDM.Consoles.Xbdm;

// Rewrite with fixes and performance improvements, based on:
// https://github.com/XeClutch/Cheat-Engine-For-Xbox-360/blob/master/Cheat%20Engine%20for%20Xbox%20360/PhantomRTM.cs

public class XbdmConsoleConnection : BaseConsoleConnection, IXbdmConnection, IHavePowerFunctions, IHaveXboxDebugFeatures, IHaveSystemEvents, IHaveXboxThreadInfo {
    private static int NextReaderID = 1;

    private readonly byte[] sharedTwoByteArray = new byte[2];
    private readonly byte[] sharedSetMemCommandBuffer = new byte[14 + 8 + 6 + 128 + 2];
    private readonly byte[] sharedGetMemCommandBuffer = new byte[14 + 8 + 10 + 8 + 2];
    private readonly byte[] localReadBuffer = new byte[4096];
    private readonly StringBuilder sbLineBuffer = new StringBuilder(400);
    private readonly TcpClient client;
    private readonly CancellationTokenSource ctsCheckClosed;

    private enum EnumEventThreadMode {
        Inactive, // Thread not running
        Starting, // Thread starting
        Running, // (SET BY THREAD) Loop running
        Stopping // Notify thread loop to stop
    }

    private volatile int systemEventSubscribeCount;
    private readonly object systemEventThreadLock = new object();
    private EnumEventThreadMode systemEventMode = EnumEventThreadMode.Inactive;
    private Thread? systemEventThread;
    private readonly List<ConsoleSystemEventHandler> systemEventHandlers = new List<ConsoleSystemEventHandler>();

    private bool isWaitingForNewLine;
    private int idxBeginLnBuf, idxEndLnBuf;

    private readonly AutoResetEvent readEvent = new AutoResetEvent(false);
    private volatile int readType;
    private BinaryReadInfo readInfo_binary;
    private StringLineReadInfo readInfo_string;
    private readonly string originalConnectionAddress;

    private readonly struct BinaryReadInfo(byte[] dstBuffer, int offset, int count, TaskCompletionSource completion, CancellationToken cancellation) {
        public readonly byte[] dstBuffer = dstBuffer;
        public readonly int offset = offset;
        public readonly int count = count;
        public readonly TaskCompletionSource completion = completion;
        public readonly CancellationToken cancellation = cancellation;
    }

    private readonly struct StringLineReadInfo(TaskCompletionSource<string> completion, CancellationToken cancellation) {
        public readonly TaskCompletionSource<string> completion = completion;
        public readonly CancellationToken cancellation = cancellation;
    }

    public EndPoint? EndPoint => this.IsConnected ? this.client.Client.RemoteEndPoint : null;

    public override RegisteredConnectionType ConnectionType => ConnectionTypeXbox360Xbdm.Instance;

    protected override bool IsConnectedCore => this.client.Connected;

    public override bool IsLittleEndian => false;

    public override AddressRange AddressableRange => new AddressRange(0, uint.MaxValue);

    public XbdmConsoleConnection(TcpClient client, string originalConnectionAddress) {
        this.client = client;
        this.originalConnectionAddress = originalConnectionAddress;

        "setmem addr=0x"u8.CopyTo(new Span<byte>(this.sharedSetMemCommandBuffer, 0, 14));
        " data="u8.CopyTo(new Span<byte>(this.sharedSetMemCommandBuffer, 22, 6));
        "getmem addr=0x"u8.CopyTo(new Span<byte>(this.sharedGetMemCommandBuffer, 0, 14));
        " length=0x"u8.CopyTo(new Span<byte>(this.sharedGetMemCommandBuffer, 22, 10));
        "\r\n"u8.CopyTo(new Span<byte>(this.sharedGetMemCommandBuffer, 40, 2));

        new Thread(this.ReaderThreadMain) {
            Name = $"XBDM Reader Thread #{NextReaderID++}",
            IsBackground = true
        }.Start();

        this.ctsCheckClosed = new CancellationTokenSource();
        CancellationToken token = this.ctsCheckClosed.Token;
        
        Task.Run(async () => {
            while (!token.IsCancellationRequested) {
                await Task.Delay(2000, token);
                if (!this.client.Connected) {
                    this.Close();
                    return;
                }
            }
        }, token);
    }

    protected override void CloseOverride() {
        try {
            DisposableUtils.DisposeAfter(this.ctsCheckClosed, x => x.Cancel());
        }
        catch {
            // ignored
        }

        try {
            Socket? socket = this.client.Client;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (socket != null && socket.Connected) {
                try {
                    this.client.Client.Send("bye\r\n"u8);
                }
                catch (Exception) {
                    // ignored
                }
            }

            socket?.Dispose();
            this.client.Dispose();
        }
        catch (Exception e) {
            AppLogger.Instance.WriteLine("Exception while closing " + nameof(XbdmConsoleConnection));
            AppLogger.Instance.WriteLine(e.GetToString());
        }
    }

    public async Task SendCommandOnly(string command) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        await this.InternalWriteCommand(command).ConfigureAwait(false);
    }

    public async Task<ConsoleResponse> GetResponseOnly() {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        return await this.InternalReadResponse_Threaded().ConfigureAwait(false);
    }

    private async Task<string> GetResponseAsTextOnly() {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        string responseText = await this.InternalReadLine_Threaded().ConfigureAwait(false);
        return responseText;
    }

    public async Task<ConsoleResponse> SendCommand(string command) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        return await this.SendCommandAndGetResponse(command).ConfigureAwait(false);
    }

    public async Task<ConsoleResponse[]> SendMultipleCommands(string[] commands) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        Task[] tasks = new Task[commands.Length];
        for (int i = 0; i < tasks.Length; i++) {
            tasks[i] = this.InternalWriteCommand(commands[i]).AsTask();
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        ConsoleResponse[] responses = new ConsoleResponse[commands.Length];
        for (int i = 0; i < responses.Length; i++) {
            responses[i] = await this.InternalReadResponse().ConfigureAwait(false);
        }

        return responses;
    }

    public Task<ConsoleResponse[]> SendMultipleCommands(IEnumerable<string> commands) => this.SendMultipleCommands(commands.ToArray());

    public async Task<string> ReadLineFromStream(CancellationToken token = default) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        return await this.InternalReadLine_Threaded(token);
    }

    /// <summary>
    /// Sends a command and receives a multi-line response
    /// </summary>
    /// <param name="command">The command to send</param>
    /// <returns></returns>
    /// <exception cref="Exception">No such command or response was not a multi-line response</exception>
    public async Task<List<string>> SendCommandAndReceiveLines(string command) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        ConsoleResponse response = await this.SendCommandAndGetResponse(command).ConfigureAwait(false);
        int idx = command.IndexOf(' ');
        VerifyResponse(idx == -1 ? command : command.Substring(0, idx), response.ResponseType, ResponseType.MultiResponse);
        return await this.InternalReadMultiLineResponse();
    }

    public async Task<List<string>> ReadMultiLineResponse() {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        return await this.InternalReadMultiLineResponse();
    }

    private async Task<List<string>> InternalReadMultiLineResponse() {
        List<string> list = new List<string>();

        string line;
        while ((line = await this.InternalReadLine().ConfigureAwait(false)) != ".") {
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

    private static void ParseThreadInfo(string text, ref XboxThread tdInfo) {
        ReadOnlySpan<char> ros = text.AsSpan();
        ParamUtils.GetDwParam(ros, "suspend", true, out tdInfo.suspendCount);
        ParamUtils.GetDwParam(ros, "priority", true, out tdInfo.priority);
        ParamUtils.GetDwParam(ros, "tlsbase", true, out tdInfo.tlsBaseAddress);
        ParamUtils.GetDwParam(ros, "base", true, out tdInfo.baseAddress);
        ParamUtils.GetDwParam(ros, "limit", true, out tdInfo.limit);
        ParamUtils.GetDwParam(ros, "slack", true, out tdInfo.slack);
        ParamUtils.GetDwParam(ros, "nameaddr", true, out tdInfo.nameAddress);
        ParamUtils.GetDwParam(ros, "namelen", true, out tdInfo.nameLength);
        ParamUtils.GetDwParam(ros, "proc", true, out tdInfo.currentProcessor);
        ParamUtils.GetDwParam(ros, "lasterr", true, out tdInfo.lastError);
    }

    public async Task<XboxThread> GetThreadInfo(uint threadId, bool requireName = true) {
        this.EnsureNotClosed();

        XboxThread tdInfo;
        using (this.CreateBusyToken()) {
            ConsoleResponse response = await this.SendCommandAndGetResponse($"threadinfo thread=0x{threadId:X8}").ConfigureAwait(false);
            if (response.ResponseType == ResponseType.NoSuchThread)
                return default;
            VerifyResponse("threadinfo", response.ResponseType, ResponseType.MultiResponse);
            List<string> info = await this.InternalReadMultiLineResponse();
            Debug.Assert(info.Count > 0, "Info should have at least 1 line since it will be a multi-line response");
            if (info.Count != 1) {
                Debugger.Break(); // interesting... more than 1 line of info. Let's explore!
            }

            tdInfo = new XboxThread { id = threadId };
            ParseThreadInfo(info[0], ref tdInfo);
        }

        if (requireName && tdInfo.nameAddress != 0 && tdInfo.nameLength != 0 && tdInfo.nameLength < int.MaxValue) {
            tdInfo.readableName = await this.ReadStringASCII(tdInfo.nameAddress, (int) tdInfo.nameLength);
        }

        return tdInfo;
    }

    public async Task<List<XboxThread>> GetThreadDump(bool requireNames = true) {
        List<string> list = await this.SendCommandAndReceiveLines("threads");
        List<XboxThread> threads = new List<XboxThread>(list.Count);
        foreach (string threadId in list) {
            XboxThread tdInfo = new XboxThread {
                id = (uint) int.Parse(threadId)
            };

            List<string> info = await this.SendCommandAndReceiveLines($"threadinfo thread=0x{tdInfo.id:X8}");
            Debug.Assert(info.Count > 0, "Info should have at least 1 line since it will be a multi-line response");
            if (info.Count != 1) {
                Debugger.Break(); // interesting... more than 1 line of info. Let's explore!
            }

            ParseThreadInfo(info[0], ref tdInfo);
            threads.Add(tdInfo);
        }

        if (requireNames) {
            for (int i = 0; i < threads.Count; i++) {
                XboxThread tdInfo = threads[i];
                if (tdInfo.nameAddress != 0 && tdInfo.nameLength != 0 && tdInfo.nameLength < int.MaxValue) {
                    tdInfo.readableName = await this.ReadStringASCII(tdInfo.nameAddress, (int) tdInfo.nameLength);
                    threads[i] = tdInfo;
                }
            }
        }

        return threads;
    }

    public async Task RebootConsole(bool cold = true) {
        await this.SendCommand("magicboot" + (cold ? " cold" : ""));
        this.Close();
    }

    public async Task ShutdownConsole() {
        await this.InternalWriteCommand("shutdown");
        this.Close();
    }

    public Task OpenDiskTray() => this.SendCommand("dvdeject");

    public Task DebugFreeze() => this.SendCommand("stop");

    public Task DebugUnFreeze() => this.SendCommand("go");

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
        List<string> result = await this.SendCommandAndReceiveLines($"xbeinfo {(executable != null ? ("name=\"" + executable + "\"") : "running")}").ConfigureAwait(false);
        foreach (string line in result) {
            if (ParamUtils.GetStrParam(line, "name", true, out string? name)) {
                return name;
            }
        }

        return null;
    }

    public async Task<List<MemoryRegion>> GetMemoryRegions(bool willRead, bool willWrite) {
        List<string> list = await this.SendCommandAndReceiveLines("walkmem").ConfigureAwait(false);
        List<MemoryRegion> newList = new List<MemoryRegion>(list.Count);
        foreach (string line in list) {
            // Typical format. We parse from unknown format just to be extra safe
            // base=0x00000000 size=0x00000000 protect=0x00000000 phys=0x00000000
            if (ParamUtils.GetDwParam(line, "protect", true, out uint protect)) {
                // Both flags contain NoCache because we cannot clear memory caches via XBDM,
                // at least, I don't know how to, therefore, readers and writers just can't
                // read from/write to the region without risking freezing the console.
                if (willWrite && (protect & 0x1222 /* UserReadOnly | NoCache | ReadOnly | ExecuteRead */) != 0)
                    continue;
                if (willRead && (protect & 0x280 /* NoCache | ExecuteWriteCopy */) != 0)
                    continue;
            }

            ParamUtils.GetDwParam(line, "base", true, out uint propBase);
            ParamUtils.GetDwParam(line, "size", true, out uint propSize);
            ParamUtils.GetDwParam(line, "phys", true, out uint propPhys);
            newList.Add(new MemoryRegion(propBase, propSize, protect, propPhys));
        }

        return newList;
    }

    public async Task<XbdmExecutionState> GetExecutionState() {
        string str = (await this.SendCommand("getexecstate").ConfigureAwait(false)).Message;
        switch (str) {
            case "pending":       return XbdmExecutionState.Pending;
            case "reboot":        return XbdmExecutionState.Reboot;
            case "start":         return XbdmExecutionState.Start;
            case "stop":          return XbdmExecutionState.Stop;
            case "pending_title": return XbdmExecutionState.TitlePending;
            case "reboot_title":  return XbdmExecutionState.TitleReboot;
            default:              return XbdmExecutionState.Unknown;
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
        ConsoleResponse response = await this.SendCommand("getpid").ConfigureAwait(false);
        VerifyResponse("getpid", response.ResponseType, ResponseType.SingleResponse);
        ParamUtils.GetDwParam(response.Message, "pid", true, out uint pid);
        return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(pid) : pid;
    }

    public async Task<IPAddress> GetTitleIPAddress() {
        ConsoleResponse response = await this.SendCommand("altaddr").ConfigureAwait(false);
        VerifyResponse("altaddr", response.ResponseType, ResponseType.SingleResponse);
        ParamUtils.GetDwParam(response.Message, "addr", true, out uint addr);
        IPAddress ip = new IPAddress(BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(addr) : addr);
        return ip;
    }

    public async Task SetConsoleColor(ConsoleColor colour) {
        await this.SendCommand("setcolor name=" + colour.ToString().ToLower()).ConfigureAwait(false);
    }

    public async Task SetDebugName(string newName) {
        await this.SendCommand("dbgname name=" + newName).ConfigureAwait(false);
    }

    public override async Task<bool?> IsMemoryInvalidOrProtected(uint address, uint count) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        if (count == 0) {
            return false;
        }

        this.FillGetMemCommandBuffer(address, count);
        await this.InternalWriteBytes(this.sharedGetMemCommandBuffer).ConfigureAwait(false);
        ConsoleResponse response = await this.InternalReadResponse().ConfigureAwait(false);
        VerifyResponse("getmem", response.ResponseType, ResponseType.MultiResponse);

        string line;
        while ((line = await this.InternalReadLine().ConfigureAwait(false)) != ".") {
            if (line.Contains('?')) {
                return true;
            }
        }

        return false;
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

    public async Task<List<RegisterEntry>?> GetRegisters(uint threadId) {
        ConsoleResponse response = await this.SendCommand($"getcontext thread=0x{threadId:X8} control int fp").ConfigureAwait(false); /* full */
        if (response.ResponseType == ResponseType.NoSuchThread) {
            return null;
        }

        VerifyResponse("getcontext", response.ResponseType, ResponseType.MultiResponse);
        List<RegisterEntry> registers = new List<RegisterEntry>();
        List<string> lines = await this.ReadMultiLineResponse();
        await Task.Run(() => {
            foreach (string line in lines) {
                int split = line.IndexOf('=');
                if (split == -1) continue;

                string name = line.Substring(0, split).ToUpperInvariant();
                string value = line.Substring(split + 1);
                if (value.StartsWith("0x")) {
                    if (uint.TryParse(value.AsSpan(2), NumberStyles.HexNumber, null, out uint value32))
                        registers.Add(new RegisterEntry32(name, value32));
                }
                else if (value.StartsWith("0q")) {
                    if (ulong.TryParse(value.AsSpan(2), NumberStyles.HexNumber, null, out ulong value64))
                        registers.Add(new RegisterEntry64(name, value64));
                }
            }
        });

        return registers;
    }

    public async Task<RegisterEntry?> ReadRegisterValue(uint threadId, string registerName) {
        ConsoleResponse response = await this.SendCommand($"getcontext thread=0x{threadId:X8} control int fp").ConfigureAwait(false); /* full */
        if (response.ResponseType == ResponseType.NoSuchThread) {
            return null;
        }

        VerifyResponse("getcontext", response.ResponseType, ResponseType.MultiResponse);
        List<string> lines = await this.ReadMultiLineResponse();
        foreach (string line in lines) {
            int split = line.IndexOf('=');
            if (split == -1) continue;

            if (!line.AsSpan(0, split).Equals(registerName, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            string value = line.Substring(split + 1);
            if (value.StartsWith("0x")) {
                if (uint.TryParse(value.AsSpan(2), NumberStyles.HexNumber, null, out uint value32))
                    return new RegisterEntry32(line.Substring(0, split), value32);
            }
            else if (value.StartsWith("0q")) {
                if (ulong.TryParse(value.AsSpan(2), NumberStyles.HexNumber, null, out ulong value64))
                    return new RegisterEntry64(line.Substring(0, split), value64);
            }
        }

        return null;
    }

    public Task SuspendThread(uint threadId) => this.SendCommand($"suspend thread=0x{threadId:X8}");

    public Task ResumeThread(uint threadId) => this.SendCommand($"resume thread=0x{threadId:X8}");

    public async Task StepThread(uint threadId) {
        // todo
    }

    public async Task SetBreakpoint(uint address, bool clear) {
        await this.SendCommand($"break addr=0x{address:X8}{(clear ? " clear" : "")}");
    }

    public async Task SetDataBreakpoint(uint address, XboxBreakpointType type, uint size, bool clear) {
        string strType;
        switch (type) {
            case XboxBreakpointType.None:
            case XboxBreakpointType.OnWrite:
                strType = "write";
                break;
            case XboxBreakpointType.OnReadWrite: strType = "read"; break;
            case XboxBreakpointType.OnExecuteHW:
            case XboxBreakpointType.OnExecute:
                strType = "execute";
                break;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        await this.SendCommand($"break {strType}=0x{address:X8} size=0x{size:X8}{(clear ? " clear" : "")}");
    }

    protected override async Task ReadBytesCore(uint address, byte[] dstBuffer, int offset, int count) {
        if (count < 128) {
            // For some reason, the old string-based reader is faster for small chunks of data.
            // However, once reading 1000s of bytes, the binary based one is twice as fast,
            // since the xbox isn't sending two bytes of ASCII for a single byte of data
            await this.OldReadBytes(address, dstBuffer, offset, count);
        }
        else {
            await this.NewReadBytes(address, dstBuffer, offset, count);
        }
    }

    private async Task OldReadBytes(uint address, byte[] dstBuffer, int offset, int count) {
        this.FillGetMemCommandBuffer(address, (uint) count);
        await this.InternalWriteBytes(this.sharedGetMemCommandBuffer).ConfigureAwait(false);
        ConsoleResponse response = await this.InternalReadResponse().ConfigureAwait(false);
        VerifyResponse("getmem", response.ResponseType, ResponseType.MultiResponse);
        int cbRead = 0;
        string line;
        while ((line = await this.InternalReadLine().ConfigureAwait(false)) != ".") {
            cbRead += DecodeLine(line, dstBuffer, offset, cbRead);
        }

        if (cbRead != count) {
            throw new IOException("Incorrect number of bytes read");
        }
    }

    private static int DecodeLine(string line, byte[] dstBuffer, int offset, int cbTotalRead) {
        int cbLine = line.Length / 2; // typically 128 when reading big chunks
        Span<byte> buffer = stackalloc byte[cbLine];
        for (int i = 0, j = 0; i < cbLine; i++, j += 2) {
            if (line[j] == '?') {
                buffer[i] = 0; // protected memory maybe?
            }
            else {
                buffer[i] = (byte) ((NumberUtils.HexCharToInt(line[j]) << 4) | NumberUtils.HexCharToInt(line[j + 1]));
            }
        }

        buffer.CopyTo(dstBuffer.AsSpan(offset + cbTotalRead, cbLine));
        return cbLine;
    }

    // Sometimes it works, sometimes it doesn't. And it also doesn't read from the correct address which is odd
    private async Task NewReadBytes(uint address, byte[] dstBuffer, int offset, int count) {
        if (count <= 0) {
            return;
        }

        ConsoleResponse response = await this.SendCommandAndGetResponse($"getmemex addr=0x{address:X8} length=0x{count:X8}").ConfigureAwait(false);
        VerifyResponse("getmemex", response.ResponseType, ResponseType.BinaryResponse);

        int statusFlag = 0, cbReadTotal = 0;
        do {
            if (statusFlag != 0) { // Most likely reading invalid/protected memory
                dstBuffer.AsSpan(offset + cbReadTotal, count).Clear();
                return;
            }

            await this.ReadFromBufferOrStreamAsync(this.sharedTwoByteArray, 0, 2);

            int header = MemoryMarshal.Read<ushort>(new ReadOnlySpan<byte>(this.sharedTwoByteArray, 0, 2));
            int chunkSize = header & 0x7FFF;
            statusFlag = header & 0x8000;
            if (count < chunkSize) {
                throw new IOException("Received more bytes than expected or invalid data");
            }

            if (chunkSize > 0)
                await this.ReadFromBufferOrStreamAsync(dstBuffer, offset + cbReadTotal, chunkSize);

            cbReadTotal += chunkSize;
            count -= chunkSize;
        } while (count > 0);
    }

    public async Task<byte[]> ReceiveBinaryData() {
        using MemoryStream memoryStream = new MemoryStream(1024);
        byte[] tmpBuffer = new byte[1024];

        int statusFlag;
        do {
            await this.ReadFromBufferOrStreamAsync(this.sharedTwoByteArray, 0, 2);

            int header = MemoryMarshal.Read<ushort>(new ReadOnlySpan<byte>(this.sharedTwoByteArray, 0, 2));
            int chunkSize = header & 0x7FFF;
            statusFlag = header & 0x8000;
            if (chunkSize <= 0) break;
            for (int count = chunkSize; count > 0; count -= tmpBuffer.Length) {
                await this.ReadFromBufferOrStreamAsync(tmpBuffer, 0, Math.Min(count, tmpBuffer.Length));
            }
        } while (statusFlag == 0);

        return memoryStream.ToArray();
    }

    protected override async Task WriteBytesCore(uint address, byte[] srcBuffer, int offset, int count) {
        int cbReadTotal = 0;
        while (count > 0) {
            int cbToWrite = Math.Min(count, 64 /* Fixed Chunk Size */);
            this.FillSetMemCommandBuffer(address + (uint) cbReadTotal, srcBuffer, offset + cbReadTotal, cbToWrite);
            await this.InternalWriteBytes(new ReadOnlyMemory<byte>(this.sharedSetMemCommandBuffer, 0, 30 + (cbToWrite << 1))).ConfigureAwait(false);
            ConsoleResponse response = await this.InternalReadResponse().ConfigureAwait(false);
            if (response.ResponseType != ResponseType.SingleResponse && response.ResponseType != ResponseType.MemoryNotMapped) {
                VerifyResponse("setmem", response.ResponseType, ResponseType.SingleResponse);
            }

            cbReadTotal += cbToWrite;
            count -= cbToWrite;
        }
    }

    private static int GetSafeCountForAddress(uint address, int count) {
        ulong overflow = (ulong) address + (uint) count;
        if (overflow > uint.MaxValue) {
            count = Math.Max(0, (int) (overflow - uint.MaxValue));
        }

        return count;
    }

    private async Task<ConsoleResponse> InternalReadResponse_Threaded() {
        string responseText = await this.InternalReadLine_Threaded().ConfigureAwait(false);
        return ConsoleResponse.FromLine(responseText);
    }

    private async Task<ConsoleResponse> InternalReadResponse() {
        string responseText = await this.InternalReadLine().ConfigureAwait(false);
        return ConsoleResponse.FromLine(responseText);
    }

    private ValueTask InternalWriteCommand(string command) {
        // Sending duplicate CRLF results in effectively two commands being sent.
        if (!command.EndsWith("\r\n", StringComparison.Ordinal))
            command += "\r\n";

        return this.InternalWriteBytes(Encoding.ASCII.GetBytes(command));
    }

    private async ValueTask InternalWriteBytes(ReadOnlyMemory<byte> buffer) {
        try {
            await this.client.GetStream().WriteAsync(buffer).ConfigureAwait(false);
        }
        catch (IOException) {
            this.Close();
            throw;
        }
    }

    private async Task<ConsoleResponse> SendCommandAndGetResponse(string command) {
        await this.InternalWriteCommand(command).ConfigureAwait(false);
        return await this.InternalReadResponse().ConfigureAwait(false);
    }

    // Using this over-optimised version of creating the command buffer
    // allows us to save roughly 0.2 seconds when writing about 1MB. 
    // That's maybe a 10% improvement over string concat? Not too bad...
    // Who TF's gonna write an entire megabyte though???
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void FillSetMemCommandBuffer(uint address, byte[] srcData, int srcOffset, int cbData) {
        ref byte dstAscii = ref MemoryMarshal.GetArrayDataReference(this.sharedSetMemCommandBuffer);
        NumberUtils.UInt32ToHexAscii(address, ref dstAscii, 14);

        int i = 28;
        ref byte hexChars = ref MemoryMarshal.GetArrayDataReference(NumberUtils.HEX_CHARS_ASCII);
        for (int j = 0; j < cbData; j++, i += 2) {
            byte b = srcData[srcOffset + j];
            Unsafe.AddByteOffset(ref dstAscii, i + 0) = Unsafe.ReadUnaligned<byte>(ref Unsafe.AddByteOffset(ref hexChars, (b >> 4) & 0xF));
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
    }

    private int ReadBytesFromBuffer(byte[] dstBuffer, int offset, int count) {
        if (this.idxBeginLnBuf == this.idxEndLnBuf) {
            return 0;
        }

        int available = this.idxEndLnBuf - this.idxBeginLnBuf;
        int read = Math.Min(available, count);
        this.localReadBuffer.AsSpan(this.idxBeginLnBuf, read).CopyTo(dstBuffer.AsSpan(offset, read));
        if ((this.idxBeginLnBuf += read) >= this.idxEndLnBuf) {
            this.idxBeginLnBuf = this.idxEndLnBuf = 0;
        }

        return read;
    }

    // returns: true when bytes processed or read from stream, false when no bytes available at all
    private bool ReadChars(out string? line, bool blocking) {
        int cbUsed;

        // Read buffered data first before reading any more data from TCP
        if (this.idxBeginLnBuf < this.idxEndLnBuf) {
            cbUsed = this.ProcessBufferAsString(this.idxBeginLnBuf, this.idxEndLnBuf, out line);

            // should only need to do == but whatever, just in case
            if ((this.idxBeginLnBuf += cbUsed) >= this.idxEndLnBuf) {
                this.idxEndLnBuf = this.idxBeginLnBuf = 0;
            }

            if (line != null) {
                return true;
            }
        }

        // converting this method into async and using await ReadAsync doesn't really help
        int available = this.client.Available;
        int cbRead = (blocking || available > 0)
            ? this.client.GetStream().Read(this.localReadBuffer, 0, Math.Min(available, this.localReadBuffer.Length))
            : 0;
        if (cbRead < 1) {
            line = null;
            return false;
        }

        cbUsed = this.ProcessBufferAsString(0, cbRead, out line);
        if (cbUsed != cbRead) {
            Debug.Assert(line != null);
            this.idxBeginLnBuf = cbUsed;
            this.idxEndLnBuf = cbRead;
        }

        return true;
    }

    private int ProcessBufferAsString(int offset, int endIndex, out string? line) {
        ref byte buffer = ref MemoryMarshal.GetArrayDataReference(this.localReadBuffer);

        bool bNeedNewLine = this.isWaitingForNewLine;
        for (int i = offset; i < endIndex; i++) {
            byte ch = Unsafe.ReadUnaligned<byte>(ref Unsafe.AddByteOffset(ref buffer, i));
            if (bNeedNewLine) {
                if (ch != '\n') {
                    continue;
                }

                this.isWaitingForNewLine = false;
                line = this.sbLineBuffer.ToString();
                this.sbLineBuffer.Length = 0;
                return i + 1 - offset;
            }
            else {
                if (ch == '\r') {
                    bNeedNewLine = true;
                    continue;
                }

                this.sbLineBuffer.Append((char) ch);
            }
        }

        line = null;
        this.isWaitingForNewLine = bNeedNewLine;
        return endIndex - offset;
    }

    private async ValueTask ActivateReader(int mode) {
        int currMode;
        while ((currMode = Interlocked.CompareExchange(ref this.readType, mode, 0)) != 0) {
            if (this.IsClosed)
                throw new IOException("Connection closed"); // maybe use a field to specify if closed due to timeout?
            await Task.Yield();
        }
    }

    private async Task<string> InternalReadLine_Threaded(CancellationToken token = default) {
        await this.ActivateReader(1);

        TaskCompletionSource<string> tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.readInfo_string = new StringLineReadInfo(tcs, token);
        this.readEvent.Set();
        return await tcs.Task.ConfigureAwait(false);
    }

    private async Task<string> InternalReadLine(CancellationToken token = default) {
        // WARNING: very important not to breakpoint anywhere in the loop when debugging, because if the loop
        // times out mid-operation, the connection becomes corrupted and we have to force close it.

        const long MaxReadIntervalToSleep = TimeSpan.TicksPerMillisecond * 18;
        bool hadAnyAction;
        long currentTime = Time.GetSystemTicks(), lastReadTime = currentTime;
        long endTicks = currentTime + (TimeSpan.TicksPerSecond * 5 /* 5 seconds */);
        do {
            token.ThrowIfCancellationRequested();

            hadAnyAction = this.ReadChars(out string? line, false);
            if (line != null) {
                return line; // assert hadAnyAction == true
            }

            if (!hadAnyAction && this.client.Available < 1) {
                // No data yet, so wait for data to come in. If we've received nothing for a few millis,
                // then just delay for a little bit. On Windows, Task.Delay() will always be about 16ms
                // due to thread context switching
                if ((Time.GetSystemTicks() - lastReadTime) >= MaxReadIntervalToSleep) {
                    await Task.Delay(5, token).ConfigureAwait(false);
                }
                else {
                    await Task.Yield();
                }
            }
        } while ((hadAnyAction ? (lastReadTime = Time.GetSystemTicks()) : Time.GetSystemTicks()) < endTicks);

        this.Close();
        throw new TimeoutException("Timeout while reading line");
    }

    private async Task ReadFromBufferOrStreamAsync(byte[] buffer, int offset, int count) {
        int cbRead = this.ReadLocalBufferOrStream(buffer, offset, count);
        if (cbRead < count)
            await this.InternalReadBytesFromBufferOrStream(buffer, offset + cbRead, count - cbRead);
    }

    private async Task InternalReadBytesFromBufferOrStream(byte[] buffer, int offset, int count, CancellationToken token = default) {
        if (count < 1) {
            return;
        }

        await this.ActivateReader(2);

        TaskCompletionSource tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        this.readInfo_binary = new BinaryReadInfo(buffer, offset, count, tcs, token);
        this.readEvent.Set();
        await tcs.Task.ConfigureAwait(false);
    }

    private void ReaderThreadMain() {
        while (!this.IsClosed) {
            this.readEvent.WaitOne();

            // -1 means locked (busy or connection closed)
            int mode = Interlocked.Exchange(ref this.readType, -1);
            switch (mode) {
                case -1:
                    Debugger.Break();
                    this.Close();
                    return;
                case 0:
                    AppLogger.Instance.WriteLine("Reader thread woke up for nothing");
                    this.readType = 0;
                    break;
                case 1: this.ReadStringData_Threaded(); break;
                case 2: this.ReadBinaryData_Threaded(); break;
            }
        }
    }

    private void ReadStringData_Threaded() {
        StringLineReadInfo info = this.readInfo_string;
        if (info.completion == null) {
            Debugger.Break();
            Debug.Fail(nameof(this.readInfo_string) + " invalid");
        }

        if (info.cancellation.IsCancellationRequested) {
            this.Close();
            info.completion.SetException(new TimeoutException("Timeout while reading line"));
            this.readType = -1;
            return;
        }

        const long MaxReadIntervalToSleep = TimeSpan.TicksPerMillisecond * 18;
        long currentTime = Time.GetSystemTicks(), lastReadTime = currentTime;
        long endTicks = currentTime + (TimeSpan.TicksPerSecond * 5000 /* 5 seconds */);

        while (true) {
            string? line;
            bool hadAnyAction;
            try {
                hadAnyAction = this.ReadChars(out line, true);
            }
            catch (Exception e) {
                this.Close();
                info.completion.SetException(e);
                this.readType = -1;
                break;
            }

            if (line != null) {
                info.completion.SetResult(line);
                this.readType = 0;
                break;
            }

            if (!hadAnyAction && this.client.Available < 1) {
                // No data yet, so wait for data to come in. If we've received nothing for a few millis,
                // then just delay for a little bit. On Windows, Task.Delay() will always be about 16ms
                // due to thread context switching
                if ((Time.GetSystemTicks() - lastReadTime) >= MaxReadIntervalToSleep) {
                    Thread.Sleep(10);
                }
                else {
                    Thread.Yield();
                }
            }

            if (info.cancellation.IsCancellationRequested || (hadAnyAction ? (lastReadTime = Time.GetSystemTicks()) : Time.GetSystemTicks()) >= endTicks) {
                this.Close();
                info.completion.SetException(new TimeoutException("Timeout while reading line"));
                this.readType = -1;
                break;
            }
        }
    }

    private void ReadBinaryData_Threaded() {
        BinaryReadInfo info = this.readInfo_binary;
        if (info.dstBuffer == null) {
            Debugger.Break();
            Debug.Fail(nameof(this.readInfo_binary) + " invalid");
        }

        const long MaxReadIntervalToSleep = TimeSpan.TicksPerMillisecond * 18; // 5ms
        long currentTime = Time.GetSystemTicks(), lastReadTime = currentTime;
        long endTicks = currentTime + (TimeSpan.TicksPerSecond * 5 /* 5 seconds */);
        int count = info.count, offset = info.offset;
        Debug.Assert(count >= 0);

        // Most likely reading data after sending a command, so by cancelling
        // it, there's no option other than to shut down connection
        if (info.cancellation.IsCancellationRequested) {
            this.Close();
            info.completion.SetException(new TimeoutException("Timeout while reading data"));
            this.readType = -1;
            return;
        }

        while (true) {
            int cbRead, cbReadable;
            if ((cbReadable = this.idxEndLnBuf - this.idxBeginLnBuf) != 0 || (cbReadable = this.client.Available) >= 1) {
                int readCount = Math.Min(cbReadable, count);
                cbRead = this.ReadBytesFromBuffer(info.dstBuffer, offset, readCount);
                if (cbRead < readCount) {
                    // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                    int cbReadTcp;
                    try {
                        cbReadTcp = this.client.GetStream().Read(info.dstBuffer, offset + cbRead, readCount - cbRead);
                    }
                    catch (Exception e) {
                        this.Close();
                        info.completion.SetException(e);
                        this.readType = -1;
                        break;
                    }

                    cbRead += cbReadTcp;
                }

                count -= cbRead;
                offset += cbRead;
            }
            else {
                cbRead = 0;
            }

            if (count < 1) {
                info.completion.SetResult();
                this.readType = 0;
                break;
            }

            if (cbRead < 1 && this.client.Available < 1 && (this.idxEndLnBuf - this.idxBeginLnBuf) == 0) {
                // No data yet, so wait for data to come in. If we've received nothing for a few millis,
                // then just delay for a little bit. On Windows, Task.Delay() will always be about 16ms
                // due to thread context switching
                if ((Time.GetSystemTicks() - lastReadTime) >= MaxReadIntervalToSleep) {
                    Thread.Sleep(10);
                }
                else {
                    Thread.Yield();
                }
            }

            if (info.cancellation.IsCancellationRequested || (cbRead > 0 ? (lastReadTime = Time.GetSystemTicks()) : Time.GetSystemTicks()) >= endTicks) {
                this.Close();
                info.completion.SetException(new TimeoutException("Timeout while reading line"));
                this.readType = -1;
                break;
            }
        }
    }

    private int ReadLocalBufferOrStream(byte[] buffer, int offset, int count) {
        int cbTotalRead = this.ReadBytesFromBuffer(buffer, offset, count);
        if (cbTotalRead < count) {
            int cbReadTcp = this.client.GetStream().Read(buffer, offset + cbTotalRead, Math.Min(count - cbTotalRead, this.client.Available));
            cbTotalRead += cbReadTcp;
        }

        return cbTotalRead;
    }

    private static void VerifyResponse(string commandName, ResponseType actual, ResponseType expected) {
        if (actual != expected) {
            throw InvalidResponseException.ForCommand(commandName, actual, expected);
        }
    }

    private void EventListenerThreadMain() {
        TcpClient? tcpClient = null;
        try {
            lock (this.systemEventThreadLock) {
                Debug.WriteLine("Event Listener Thread Started");
                this.systemEventMode = EnumEventThreadMode.Running;
            }

            // This method is extremely janky and needs a rewrite. We shouldn't use a delegate
            // XbdmConsoleConnection, but instead make some static methods that take TcpClient that XbdmConsoleConnection use

            using TcpClient theClient = new TcpClient();
            theClient.ReceiveTimeout = 0;
            theClient.Connect(this.originalConnectionAddress, 730);
            tcpClient = theClient;

            using StreamReader cmdReader = new StreamReader(theClient.GetStream(), Encoding.ASCII);
            string? strresponse = cmdReader.ReadLine()?.ToLower();
            if (strresponse != "201- connected") {
                throw new Exception("Borken");
            }

            XbdmConsoleConnection delegateConnection = new XbdmConsoleConnection(theClient, this.originalConnectionAddress);

            // DEBUGGER CONNECT PORT=0x<PORT_HERE> override user=<COMPUTER_NAME_HERE> name="MemEngine360"
            ConsoleResponse response = delegateConnection.SendCommand($"debugger connect override name=\"MemoryEngine360\" user=\"{Environment.MachineName}\"").GetAwaiter().GetResult();
            if (response.ResponseType != ResponseType.SingleResponse) {
                throw new Exception($"Failed to enable debugger. Response = {response.ToString()}");
            }

            // we must repeat this since if the xbox is spewing events, it seems like it does it in the background. So even if we use
            // delegateConnection.SendMultipleCommands, there's still a chance we get one of the events in the responses.
            List<XbdmEventArgs> tmpPreRunEvents = new List<XbdmEventArgs>();

            // no idea what reconnectport does, surely it's not the port it tries to reconnect on
            delegateConnection.SendCommandOnly("notify reconnectport=12345 reverse").GetAwaiter().GetResult();
            while (true) {
                string responseText = delegateConnection.GetResponseAsTextOnly().GetAwaiter().GetResult();
                if (ConsoleResponse.TryParseFromLine(responseText, out response)) {
                    if (response.ResponseType != ResponseType.DedicatedConnection) {
                        throw new Exception($"Failed to setup notifications. Response type is not {nameof(ResponseType.DedicatedConnection)}: {response.RawMessage}");
                    }

                    break;
                }

                tmpPreRunEvents.Add(XbdmEventUtils.ParseSpecial(responseText) ?? new XbdmEventArgs(responseText));
            }

            lock (this.systemEventThreadLock) {
                if (this.systemEventMode == EnumEventThreadMode.Stopping) {
                    goto CloseConnection;
                }
            }

            List<ConsoleSystemEventHandler> eventHandlerList;
            lock (this.systemEventHandlers) {
                eventHandlerList = this.systemEventHandlers.ToList();
            }

            foreach (XbdmEventArgs tmpEvent in tmpPreRunEvents) {
                foreach (ConsoleSystemEventHandler handler in eventHandlerList) {
                    handler(this, tmpEvent);
                }
            }

            while (delegateConnection.IsConnected) {
                lock (this.systemEventThreadLock) {
                    if (this.systemEventMode == EnumEventThreadMode.Stopping) {
                        goto CloseConnection;
                    }
                }

                if (delegateConnection.client.Available < 1) {
                    Thread.Sleep(10);
                }

                if (delegateConnection.client.Available < 1) {
                    continue;
                }

                string line;
                try {
                    line = delegateConnection.ReadLineFromStream().GetAwaiter().GetResult();
                }
                catch (Exception) {
                    continue;
                }

                Debug.Assert(line != null);
                XbdmEventArgs e = XbdmEventUtils.ParseSpecial(line) ?? new XbdmEventArgs(line);

                lock (this.systemEventHandlers) {
                    eventHandlerList = this.systemEventHandlers.ToList();
                }

                foreach (ConsoleSystemEventHandler handler in eventHandlerList) {
                    handler(this, e);
                }
            }

            CloseConnection:
            Debug.WriteLine("Stopping Event Listener Thread");
            theClient.Close();
        }
        catch (Exception e) {
            AppLogger.Instance.WriteLine("Exception in " + Thread.CurrentThread.Name);
            AppLogger.Instance.WriteLine(e.GetToString());
            tcpClient?.Close();
            tcpClient?.Dispose();
        }
    }

    public IDisposable SubscribeToEvents(ConsoleSystemEventHandler handler) {
        ArgumentNullException.ThrowIfNull(handler);
        if (Interlocked.Increment(ref this.systemEventSubscribeCount) == 1) {
            lock (this.systemEventThreadLock) {
                switch (this.systemEventMode) {
                    case EnumEventThreadMode.Inactive:
                    case EnumEventThreadMode.Stopping: {
                        this.systemEventMode = EnumEventThreadMode.Starting;
                        this.systemEventThread = new Thread(this.EventListenerThreadMain) {
                            IsBackground = true, Name = "Xbdm Event Listener Thread",
                            Priority = ThreadPriority.BelowNormal
                        };

                        this.systemEventThread.Start();
                        break;
                    }
                }
            }
        }

        lock (this.systemEventHandlers) {
            this.systemEventHandlers.Add(handler);
        }

        return new EventSubscriber(this, handler);
    }

    private void UnsubscribeFromEvents(ConsoleSystemEventHandler handler) {
        lock (this.systemEventHandlers) {
            this.systemEventHandlers.Remove(handler);
        }

        if (Interlocked.Decrement(ref this.systemEventSubscribeCount) == 0) {
            this.systemEventMode = EnumEventThreadMode.Stopping;
        }
    }

    private class EventSubscriber : IDisposable {
        private volatile XbdmConsoleConnection? connection;
        private readonly ConsoleSystemEventHandler handler;

        public EventSubscriber(XbdmConsoleConnection connection, ConsoleSystemEventHandler handler) {
            this.connection = connection;
            this.handler = handler;
        }

        public void Dispose() {
            XbdmConsoleConnection? conn = Interlocked.Exchange(ref this.connection, null);
            conn?.UnsubscribeFromEvents(this.handler);
        }
    }
}