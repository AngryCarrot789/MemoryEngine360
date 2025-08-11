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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using MemEngine360.Engine.Debugging;
using MemEngine360.Engine.Events.XbdmEvents;
using MemEngine360.XboxBase;
using MemEngine360.XboxBase.Modules;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Destroying;
using ConsoleColor = MemEngine360.Connections.Features.ConsoleColor;

namespace MemEngine360.Xbox360XBDM.Consoles.Xbdm;

public class XbdmConsoleConnection : BaseConsoleConnection {
    private static int NextReaderID = 1;
    private static volatile bool IsJRPC2DetectionBroken;

    private readonly byte[] sharedTwoByteArray = new byte[2];
    private readonly byte[] sharedSetMemCommandBuffer = new byte[14 + 8 + 6 + 128 + 2];
    private readonly byte[] sharedGetMemCommandBuffer = new byte[14 + 8 + 10 + 8 + 2];
    private readonly byte[] localReadBuffer = new byte[4096];
    private readonly StringBuilder sbLineBuffer = new StringBuilder(400);
    private readonly TcpClient client;
    private readonly CancellationTokenSource ctsCheckClosed;
    private readonly bool isEventConnection;

    private enum EnumEventThreadMode {
        Inactive, // Thread not running
        Starting, // Thread starting
        Running, // (SET BY THREAD) Loop running
        Stopping // Notify thread loop to stop
    }

    private volatile int systemEventSubscribeCount;
    private readonly Lock systemEventThreadLock = new Lock();
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
    private volatile XbdmEventArgsExecutionState? currentState;

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

    private readonly XbdmFeaturesImpl xbdmFeatures;
    private Jrpc2FeaturesImpl? jrpcFeatures;

    public XbdmConsoleConnection(TcpClient client, string originalConnectionAddress) : this(client, originalConnectionAddress, false) {
    }

    private XbdmConsoleConnection(TcpClient client, string originalConnectionAddress, bool isEventConnection) {
        this.client = client;
        this.originalConnectionAddress = originalConnectionAddress;
        this.isEventConnection = isEventConnection;

        "setmem addr=0x"u8.CopyTo(new Span<byte>(this.sharedSetMemCommandBuffer, 0, 14));
        " data="u8.CopyTo(new Span<byte>(this.sharedSetMemCommandBuffer, 22, 6));
        "getmem addr=0x"u8.CopyTo(new Span<byte>(this.sharedGetMemCommandBuffer, 0, 14));
        " length=0x"u8.CopyTo(new Span<byte>(this.sharedGetMemCommandBuffer, 22, 10));
        "\r\n"u8.CopyTo(new Span<byte>(this.sharedGetMemCommandBuffer, 40, 2));

        this.xbdmFeatures = new XbdmFeaturesImpl(this);

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

    /// <summary>
    /// Attempts to figure out additional features, such as JRPC2 being installed 
    /// </summary>
    public async Task DetectDynamicFeatures() {
        if (!IsJRPC2DetectionBroken) {
            const string GetCpuSensorCommand = "consolefeatures ver=2 type=15 params=\"A\\0\\A\\1\\1\\0\\\"";
            ConsoleResponse response = await this.SendCommand(GetCpuSensorCommand);
            if (response.ResponseType == ResponseType.SingleResponse) {
                if (uint.TryParse(response.Message, NumberStyles.HexNumber, null, out uint temperature)) {
                    // We got a temperature value, so assume JRPC2 is working fine
                    this.jrpcFeatures = new Jrpc2FeaturesImpl(this);
                    Debug.WriteLine(this.originalConnectionAddress + " is using JRPC2");
                }
                else {
                    Debug.WriteLine(this.originalConnectionAddress + " is not using JRPC2");
                }
            }
            else {
                IsJRPC2DetectionBroken = true;
                Debug.WriteLine(this.originalConnectionAddress + " connection broken due to JRPC2 detection");
                this.Close();
                throw new IOException("JRPC2 detection is now disabled as it has corrupted the connection. Please re-connect");
            }
        }
    }

    public override bool TryGetFeature<T>([NotNullWhen(true)] out T? feature) where T : class {
        if (this.xbdmFeatures is T t) {
            feature = t;
            return true;
        }

        if (this.jrpcFeatures is T t2) {
            feature = t2;
            return true;
        }

        return base.TryGetFeature(out feature);
    }

    public override bool HasFeature<T>() {
        return this.xbdmFeatures is T ||
               this.jrpcFeatures is T ||
               base.HasFeature<T>();
    }

    public override bool HasFeature(Type typeOfFeature) {
        return typeOfFeature.IsInstanceOfType(this.xbdmFeatures) ||
               typeOfFeature.IsInstanceOfType(this.jrpcFeatures) ||
               base.HasFeature(typeOfFeature);
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
            AppLogger.Instance.WriteLine(ExceptionUtils.GetToString(e));
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

        return await this.InternalSendCommand(command).ConfigureAwait(false);
    }

    public async Task<ConsoleResponse[]> SendMultipleCommands(string[] commands) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        StringBuilder sb = new StringBuilder();
        foreach (string cmd in commands) {
            sb.Append(cmd);
            if (!cmd.EndsWith("\r\n", StringComparison.Ordinal))
                sb.Append("\r\n");
        }

        await this.InternalWriteBytes(Encoding.ASCII.GetBytes(sb.ToString()));

        ConsoleResponse[] responses = new ConsoleResponse[commands.Length];
        for (int i = 0; i < responses.Length; i++) {
            responses[i] = await this.InternalReadResponse().ConfigureAwait(false);
        }

        return responses;
    }

    public Task<ConsoleResponse[]> SendMultipleCommands(IEnumerable<string> commands) => this.SendMultipleCommands(Enumerable.ToArray(commands));

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

        ConsoleResponse response = await this.InternalSendCommand(command).ConfigureAwait(false);
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
        return Enumerable.ToList(Enumerable.Select(await this.SendCommandAndReceiveLines(Text), x => {
            int split = x.IndexOf(':');
            return new KeyValuePair<string, string>(
                split == -1 ? x : MemoryExtensions.Trim(MemoryExtensions.AsSpan(x, 0, split)).ToString(),
                split == -1 ? "" : MemoryExtensions.Trim(MemoryExtensions.AsSpan(x, split + 1)).ToString());
        }));
    }

    private static void ParseThreadInfo(string text, ref XboxThread tdInfo) {
        ReadOnlySpan<char> ros = MemoryExtensions.AsSpan(text);
        ParamUtils.GetDwParam(ros, "suspend", true, out tdInfo.suspendCount);
        ParamUtils.GetDwParam(ros, "priority", true, out tdInfo.priority);
        ParamUtils.GetDwParam(ros, "tlsbase", true, out tdInfo.tlsBaseAddress);
        ParamUtils.GetDwParam(ros, "base", true, out tdInfo.baseAddress);
        ParamUtils.GetDwParam(ros, "limit", true, out tdInfo.stackLimit);
        ParamUtils.GetDwParam(ros, "slack", true, out tdInfo.stackSlack);
        ParamUtils.GetDwParam(ros, "nameaddr", true, out tdInfo.nameAddress);
        ParamUtils.GetDwParam(ros, "namelen", true, out tdInfo.nameLength);
        ParamUtils.GetDwParam(ros, "proc", true, out tdInfo.currentProcessor);
        ParamUtils.GetDwParam(ros, "lasterr", true, out tdInfo.lastError);
    }

    public async Task<XboxThread> GetThreadInfo(uint threadId, bool requireName = true) {
        this.EnsureNotClosed();

        XboxThread tdInfo;
        using (this.CreateBusyToken()) {
            ConsoleResponse response = await this.InternalSendCommand($"threadinfo thread=0x{threadId:X8}").ConfigureAwait(false);
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

    public async Task<FreezeResult> DebugFreeze() {
        ConsoleResponse response = await this.SendCommand("stop");
        if (response.ResponseType == ResponseType.SingleResponse)
            return FreezeResult.Success;

        VerifyResponse("stop", response.ResponseType, ResponseType.XBDM_ALREADYSTOPPED);
        return FreezeResult.AlreadyFrozen;
    }

    public async Task<UnFreezeResult> DebugUnFreeze() {
        ConsoleResponse response = await this.SendCommand("go");
        if (response.ResponseType == ResponseType.SingleResponse)
            return UnFreezeResult.Success;

        VerifyResponse("go", response.ResponseType, ResponseType.NotStopped);
        return UnFreezeResult.AlreadyUnfrozen;
    }

    public async Task DeleteFile(string path) {
        string[] lines = path.Split('\\');
        StringBuilder dirSb = new StringBuilder();
        for (int i = 0; i < lines.Length - 1; i++)
            dirSb.Append(lines[i]).Append('\\');
        await this.SendCommand("delete title=\"" + path + "\" dir=\"" + dirSb + "\"").ConfigureAwait(false);
    }

    public async Task LaunchFile(string path) {
        string[] lines = path.Split('\\');
        StringBuilder dirSb = new StringBuilder();
        for (int i = 0; i < lines.Length - 1; i++)
            dirSb.Append(lines[i]).Append('\\');
        await this.SendCommand("magicboot title=\"" + path + "\" directory=\"" + dirSb + "\"").ConfigureAwait(false);
    }

    public async Task<string> GetConsoleID() {
        return (await this.SendCommand("getconsoleid").ConfigureAwait(false)).Message.Substring(10);
    }

    public async Task<string> GetDebugName() {
        return (await this.SendCommand("dbgname").ConfigureAwait(false)).Message;
    }

    public async Task<string?> GetXbeInfo(string? executable) {
        List<string> result = await this.SendCommandAndReceiveLines($"xbeinfo {(executable != null ? "name=\"" + executable + "\"" : "running")}").ConfigureAwait(false);
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

    public async Task<XboxExecutionState> GetExecutionState() {
        string str = (await this.SendCommand("getexecstate").ConfigureAwait(false)).Message;
        switch (str) {
            case "pending":       return XboxExecutionState.Pending;
            case "reboot":        return XboxExecutionState.Reboot;
            case "start":         return XboxExecutionState.Start;
            case "stop":          return XboxExecutionState.Stop;
            case "pending_title": return XboxExecutionState.TitlePending;
            case "reboot_title":  return XboxExecutionState.TitleReboot;
            default:              return XboxExecutionState.Unknown;
        }
    }

    public async Task<XboxHardwareInfo> GetHardwareInfo() {
        List<KeyValuePair<string, string>> lines = await this.SendCommandAndReceiveLines2("hwinfo");
        XboxHardwareInfo info;
        info.Flags = uint.Parse(MemoryExtensions.AsSpan(lines[0].Value, 2, 8), NumberStyles.HexNumber);
        info.NumberOfProcessors = byte.Parse(MemoryExtensions.AsSpan(lines[1].Value, 2, 2), NumberStyles.HexNumber);
        info.PCIBridgeRevisionID = byte.Parse(MemoryExtensions.AsSpan(lines[2].Value, 2, 2), NumberStyles.HexNumber);

        string rbStr = lines[3].Value;
        info.ReservedBytes = new byte[6];
        info.ReservedBytes[0] = byte.Parse(MemoryExtensions.AsSpan(rbStr, 3, 2), NumberStyles.HexNumber);
        info.ReservedBytes[1] = byte.Parse(MemoryExtensions.AsSpan(rbStr, 6, 2), NumberStyles.HexNumber);
        info.ReservedBytes[2] = byte.Parse(MemoryExtensions.AsSpan(rbStr, 9, 2), NumberStyles.HexNumber);
        info.ReservedBytes[3] = byte.Parse(MemoryExtensions.AsSpan(rbStr, 12, 2), NumberStyles.HexNumber);
        info.ReservedBytes[4] = byte.Parse(MemoryExtensions.AsSpan(rbStr, 15, 2), NumberStyles.HexNumber);
        info.ReservedBytes[5] = byte.Parse(MemoryExtensions.AsSpan(rbStr, 18, 2), NumberStyles.HexNumber);
        info.BldrMagic = ushort.Parse(MemoryExtensions.AsSpan(lines[4].Value, 2, 4), NumberStyles.HexNumber);
        info.BldrFlags = ushort.Parse(MemoryExtensions.AsSpan(lines[5].Value, 2, 4), NumberStyles.HexNumber);
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
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        ConsoleResponse response = await this.InternalSendCommand($"getcontext thread=0x{threadId:X8} control int fp").ConfigureAwait(false); /* full */
        if (response.ResponseType == ResponseType.NoSuchThread) {
            return null;
        }

        VerifyResponse("getcontext", response.ResponseType, ResponseType.MultiResponse);
        List<RegisterEntry> registers = new List<RegisterEntry>();
        await Task.Run(async () => {
            List<string> lines = await this.InternalReadMultiLineResponse().ConfigureAwait(false);
            foreach (string line in lines) {
                int split = line.IndexOf('=');
                if (split == -1)
                    continue;

                string name = line.Substring(0, split).ToUpperInvariant();
                string value = line.Substring(split + 1);
                if (value.StartsWith("0x")) {
                    if (uint.TryParse(MemoryExtensions.AsSpan(value, 2), NumberStyles.HexNumber, null, out uint value32))
                        registers.Add(new RegisterEntry32(name, value32));
                }
                else if (value.StartsWith("0q")) {
                    if (ulong.TryParse(MemoryExtensions.AsSpan(value, 2), NumberStyles.HexNumber, null, out ulong value64))
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
            if (split == -1)
                continue;

            if (!MemoryExtensions.Equals(MemoryExtensions.AsSpan(line, 0, split), registerName, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            string value = line.Substring(split + 1);
            if (value.StartsWith("0x")) {
                if (uint.TryParse(MemoryExtensions.AsSpan(value, 2), NumberStyles.HexNumber, null, out uint value32))
                    return new RegisterEntry32(line.Substring(0, split), value32);
            }
            else if (value.StartsWith("0q")) {
                if (ulong.TryParse(MemoryExtensions.AsSpan(value, 2), NumberStyles.HexNumber, null, out ulong value64))
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

    public async Task<ConsoleModule?> GetModuleForAddress(uint address, bool bNeedSections) {
        List<string> modules = await this.SendCommandAndReceiveLines("modules");
        foreach (string moduleLine in modules) {
            if (!ParamUtils.GetStrParam(moduleLine, "name", true, out string? name) ||
                !ParamUtils.GetDwParam(moduleLine, "base", true, out uint modBase) ||
                !ParamUtils.GetDwParam(moduleLine, "size", true, out uint modSize)) {
                continue;
            }

            if (address < modBase || address >= modBase + modSize) {
                continue;
            }

            // ParamUtils.GetDwParam(moduleLine, "timestamp", true, out uint modTimestamp);
            // ParamUtils.GetDwParam(moduleLine, "check", true, out uint modChecksum);
            ParamUtils.GetDwParam(moduleLine, "osize", true, out uint modOriginalSize);

            ConsoleModule consoleModule = new ConsoleModule() {
                Name = name,
                FullName = null, // unavailable until I can figure out how to get xbeinfo to work
                BaseAddress = modBase,
                ModuleSize = modSize,
                OriginalModuleSize = modOriginalSize,
                // EntryPoint = module.GetEntryPointAddress()
            };

            // 0x10100 = entry point for most things, apart from xboxkrnl.exe
            // format: 
            //   fieldsize=0x<size in uint32 hex>
            //   <value>
            // may return ResponseType.XexFieldNotFound
            ConsoleResponse entryPointResponse = await this.SendCommand($"xexfield module=\"{name}\" field=0x10100");
            if (entryPointResponse.ResponseType == ResponseType.MultiResponse) {
                List<string> lines = await this.ReadMultiLineResponse();
                if (lines.Count == 2 && uint.TryParse(lines[1], NumberStyles.HexNumber, null, out uint entryPoint)) {
                    consoleModule.EntryPoint = entryPoint;
                }
            }

            if (bNeedSections) {
                ConsoleResponse response = await this.SendCommand($"modsections name=\"{name}\"");
                if (response.ResponseType != ResponseType.FileNotFound) {
                    List<string> sections = await this.ReadMultiLineResponse();
                    foreach (string sectionLine in sections) {
                        ParamUtils.GetStrParam(sectionLine, "name", true, out string? sec_name);
                        ParamUtils.GetDwParam(sectionLine, "base", true, out uint sec_base);
                        ParamUtils.GetDwParam(sectionLine, "size", true, out uint sec_size);
                        ParamUtils.GetDwParam(sectionLine, "index", true, out uint sec_index);
                        ParamUtils.GetDwParam(sectionLine, "flags", true, out uint sec_flags);

                        consoleModule.Sections.Add(new ConsoleModuleSection() {
                            Name = string.IsNullOrWhiteSpace(sec_name) ? null : sec_name,
                            BaseAddress = sec_base,
                            Size = sec_size,
                            Index = sec_index,
                            Flags = (XboxSectionInfoFlags) sec_flags,
                        });
                    }
                }
            }

            return consoleModule;
        }

        return null;
    }

    public async Task<FunctionCallEntry?[]> FindFunctions(uint[] iar) {
        if (iar.Length < 1) {
            return [];
        }

        int resolvedCount = 0;
        FunctionCallEntry?[] entries = new FunctionCallEntry?[iar.Length];

        List<string> modules = await this.SendCommandAndReceiveLines("modules");
        foreach (string moduleLine in modules) {
            if (!ParamUtils.GetStrParam(moduleLine, "name", true, out string? modName)) {
                continue;
            }

            ConsoleResponse response = await this.SendCommand($"modsections name=\"{modName}\"");
            if (response.ResponseType == ResponseType.FileNotFound) {
                continue;
            }

            List<string> sections = await this.ReadMultiLineResponse();
            foreach (string sectionLine in sections) {
                ParamUtils.GetStrParam(sectionLine, "name", true, out string? sec_name);
                if (sec_name != ".pdata") {
                    continue;
                }

                ParamUtils.GetDwParam(sectionLine, "base", true, out uint sec_base);
                ParamUtils.GetDwParam(sectionLine, "size", true, out uint sec_size);
                byte[] buffer = await this.ReadBytes(sec_base, (int) sec_size);
                ReadOnlySpan<byte> rosBuffer = new ReadOnlySpan<byte>(buffer);

                int functionCount = (int) (sec_size / 16);

                uint startAddress = BinaryPrimitives.ReadUInt32BigEndian(rosBuffer);
                for (int j = 0, offset = 8; j < functionCount; j++, offset += 16) {
                    uint endAddress = BinaryPrimitives.ReadUInt32BigEndian(rosBuffer.Slice(offset, 4));
                    RUNTIME_FUNCTION_PPC function = new RUNTIME_FUNCTION_PPC {
                        BeginAddress = startAddress,
                        EndAddress = endAddress
                    };

                    for (int k = 0; k < iar.Length; k++) {
                        if (entries[k] == null && function.Contains(iar[k])) {
                            entries[k] = new FunctionCallEntry(modName, function.BeginAddress, function.EndAddress - function.BeginAddress, BinaryPrimitives.ReadUInt64BigEndian(rosBuffer.Slice(offset, 8)));
                            resolvedCount++;
                        }
                    }

                    if (resolvedCount == iar.Length) {
                        return entries;
                    }

                    startAddress = endAddress;
                }

                break;
            }
        }

        return entries;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RUNTIME_FUNCTION_PPC {
        [FieldOffset(0)] public uint BeginAddress;
        [FieldOffset(4)] public uint Padding1;
        [FieldOffset(8)] public uint EndAddress;
        [FieldOffset(12)] public uint Padding2;

        public readonly bool Contains(uint address) {
            return address >= this.BeginAddress && address < this.EndAddress;
        }

        public override string ToString() {
            return $"{this.BeginAddress:X8} -> {this.EndAddress:X8} (Length: {this.EndAddress - this.BeginAddress:X})";
        }
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

        buffer.CopyTo(MemoryExtensions.AsSpan(dstBuffer, offset + cbTotalRead, cbLine));
        return cbLine;
    }

    // Sometimes it works, sometimes it doesn't. And it also doesn't read from the correct address which is odd
    private async Task NewReadBytes(uint address, byte[] dstBuffer, int offset, int count) {
        if (count <= 0) {
            return;
        }

        ConsoleResponse response = await this.InternalSendCommand($"getmemex addr=0x{address:X8} length=0x{count:X8}").ConfigureAwait(false);
        VerifyResponse("getmemex", response.ResponseType, ResponseType.BinaryResponse);

        int statusFlag = 0, cbReadTotal = 0;
        do {
            if (statusFlag != 0) { // Most likely reading invalid/protected memory
                MemoryExtensions.AsSpan(dstBuffer, offset + cbReadTotal, count).Clear();
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

    // Rather than potentially lock up the connection, allow cancellation but it might corrupt the connection.
    public async Task<byte[]> ReceiveBinaryData(CancellationToken cancellation) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        using MemoryStream memoryStream = new MemoryStream(1024);
        byte[] tmpBuffer = new byte[1024];

        int statusFlag;
        do {
            await this.ReadFromBufferOrStreamAsync(this.sharedTwoByteArray, 0, 2, cancellation);

            int header = MemoryMarshal.Read<ushort>(new ReadOnlySpan<byte>(this.sharedTwoByteArray, 0, 2));
            int chunkSize = header & 0x7FFF;
            statusFlag = header & 0x8000;
            if (chunkSize <= 0)
                break;
            for (int count = chunkSize; count > 0; count -= tmpBuffer.Length) {
                int cbRead = Math.Min(count, tmpBuffer.Length);
                await this.ReadFromBufferOrStreamAsync(tmpBuffer, 0, cbRead, cancellation);
                memoryStream.Write(tmpBuffer, 0, cbRead);
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

    private async Task<ConsoleResponse> InternalSendCommand(string command) {
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
        MemoryExtensions.AsSpan(this.localReadBuffer, this.idxBeginLnBuf, read).CopyTo(MemoryExtensions.AsSpan(dstBuffer, offset, read));
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
        int cbRead = blocking || available > 0
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
        long endTicks = currentTime + TimeSpan.TicksPerSecond * 5 /* 5 seconds */;
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
                if (Time.GetSystemTicks() - lastReadTime >= MaxReadIntervalToSleep) {
                    await Task.Delay(5, token).ConfigureAwait(false);
                }
                else {
                    await Task.Yield();
                }
            }
        } while ((hadAnyAction ? lastReadTime = Time.GetSystemTicks() : Time.GetSystemTicks()) < endTicks);

        this.Close();
        throw new TimeoutException("Timeout while reading line");
    }

    private async Task ReadFromBufferOrStreamAsync(byte[] buffer, int offset, int count, CancellationToken cancellation = default) {
        int cbRead = this.ReadLocalBufferOrStream(buffer, offset, count);
        if (cbRead < count)
            await this.InternalReadBytesFromBufferOrStream(buffer, offset + cbRead, count - cbRead, cancellation);
    }

    private async Task InternalReadBytesFromBufferOrStream(byte[] buffer, int offset, int count, CancellationToken cancellation = default) {
        if (count < 1) {
            return;
        }

        await this.ActivateReader(2);

        TaskCompletionSource tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        this.readInfo_binary = new BinaryReadInfo(buffer, offset, count, tcs, cancellation);
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
        long endTicks = currentTime + TimeSpan.TicksPerSecond * 5000 /* 5 seconds */;

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
                if (Time.GetSystemTicks() - lastReadTime >= MaxReadIntervalToSleep) {
                    Thread.Sleep(10);
                }
                else {
                    Thread.Yield();
                }
            }

            if (info.cancellation.IsCancellationRequested || (hadAnyAction ? lastReadTime = Time.GetSystemTicks() : Time.GetSystemTicks()) >= endTicks) {
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
        long endTicks = currentTime + TimeSpan.TicksPerSecond * 5 /* 5 seconds */;
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

            if (cbRead < 1 && this.client.Available < 1 && this.idxEndLnBuf - this.idxBeginLnBuf == 0) {
                // No data yet, so wait for data to come in. If we've received nothing for a few millis,
                // then just delay for a little bit. On Windows, Task.Delay() will always be about 16ms
                // due to thread context switching
                if (Time.GetSystemTicks() - lastReadTime >= MaxReadIntervalToSleep) {
                    Thread.Sleep(10);
                }
                else {
                    Thread.Yield();
                }
            }

            if (info.cancellation.IsCancellationRequested || (cbRead > 0 ? lastReadTime = Time.GetSystemTicks() : Time.GetSystemTicks()) >= endTicks) {
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
            // 
            // Also, the xbox sends the current execution state as soon as we connect the debugger,
            // so this list will most likely contain that event
            List<XbdmEventArgs> preRunEvents = new List<XbdmEventArgs>();

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

                XbdmEventArgs theEvent = XbdmEventUtils.ParseSpecial(responseText) ?? new XbdmEventArgs(responseText);
                if (theEvent is XbdmEventArgsExecutionState) {
                    this.currentState = (XbdmEventArgsExecutionState) theEvent;
                }

                preRunEvents.Add(theEvent);
            }

            lock (this.systemEventThreadLock) {
                if (this.systemEventMode == EnumEventThreadMode.Stopping) {
                    goto CloseConnection;
                }
            }

            List<ConsoleSystemEventHandler> eventHandlerList;
            if (preRunEvents.Count > 0) {
                lock (this.systemEventHandlers) {
                    eventHandlerList = Enumerable.ToList(this.systemEventHandlers);
                }

                foreach (XbdmEventArgs tmpEvent in preRunEvents) {
                    foreach (ConsoleSystemEventHandler handler in eventHandlerList) {
                        handler(this, tmpEvent);
                    }
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
                if (e is XbdmEventArgsExecutionState) {
                    this.currentState = (XbdmEventArgsExecutionState) e;
                }

                lock (this.systemEventHandlers) {
                    eventHandlerList = Enumerable.ToList(this.systemEventHandlers);
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
            AppLogger.Instance.WriteLine(ExceptionUtils.GetToString(e));
            tcpClient?.Close();
            tcpClient?.Dispose();
        }
    }

    public IDisposable SubscribeToEvents(ConsoleSystemEventHandler handler) {
        ArgumentNullException.ThrowIfNull(handler);
        if (this.isEventConnection)
            throw new InvalidOperationException("Attempt to subscribe to events on an event connection");

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
            if (this.currentState != null) {
                handler(this, this.currentState);
            }
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

    private class Jrpc2FeaturesImpl : IFeatureXboxJRPC2 {
        private readonly XbdmConsoleConnection connection;

        public IConsoleConnection Connection => this.connection;

        public Jrpc2FeaturesImpl(XbdmConsoleConnection connection) {
            this.connection = connection;
        }
        
        // This file, specifically this class, contains code adapted from JRPC by XboxChef, licenced under GPL-3.0.
        // See LICENCE file for the full terms.
        // https://github.com/XboxChef/JRPC/blob/master/JRPC_Client/JRPC.cs

        public Task ShowNotification(XNotifyLogo logo, string? message) {
            string msgHex = message != null ? NumberUtils.ConvertStringToHex(message, Encoding.ASCII) : "";
            string command = $"consolefeatures ver=2 type=12 params=\"A\\0\\A\\2\\2/{message?.Length ?? 0}\\{msgHex}\\{(uint) RPCDataType.Int}\\{(int) logo}\\\"";
            return this.connection.SendCommand(command);
        }

        public async Task<string> GetCPUKey() {
            const string command = "consolefeatures ver=2 type=10 params=\"A\\0\\A\\0\\\"";
            ConsoleResponse response = await this.connection.SendCommand(command);
            if (response.ResponseType != ResponseType.SingleResponse || response.Message.Length != 32) {
                this.connection.Close();
                throw new IOException("JRPC2 did not respond correctly");
            }

            return response.Message;
        }

        public async Task<uint> GetDashboardVersion() {
            const string command = "consolefeatures ver=2 type=13 params=\"A\\0\\A\\4\\\"";
            ConsoleResponse response = await this.connection.SendCommand(command);
            if (response.ResponseType != ResponseType.SingleResponse || !uint.TryParse(response.Message, out uint version)) {
                this.connection.Close();
                throw new IOException("JRPC2 did not respond correctly");
            }

            return version;
        }

        public async Task<uint> GetTemperature(SensorType sensorType) {
            string command = $"consolefeatures ver=2 type=15 params=\"A\\0\\A\\1\\{(uint) RPCDataType.Int}\\{(int) sensorType}\\\"";
            ConsoleResponse response = await this.connection.SendCommand(command);
            if (response.ResponseType == ResponseType.SingleResponse) {
                if (uint.TryParse(response.Message, NumberStyles.HexNumber, null, out uint version)) {
                    return version;
                }
            }

            this.connection.Close();
            throw new IOException("JRPC2 did not respond correctly");
        }

        public async Task<uint> GetCurrentTitleId() {
            const string command = "consolefeatures ver=2 type=16 params=\"A\\0\\A\\0\\\"";
            ConsoleResponse response = await this.connection.SendCommand(command);
            if (response.ResponseType == ResponseType.SingleResponse) {
                if (uint.TryParse(response.Message, NumberStyles.HexNumber, null, out uint version)) {
                    return version;
                }
            }

            this.connection.Close();
            throw new IOException("JRPC2 did not respond correctly");
        }

        public async Task<string> GetMotherboardType() {
            const string command = "consolefeatures ver=2 type=17 params=\"A\\0\\A\\0\\\"";
            ConsoleResponse response = await this.connection.SendCommand(command);
            if (response.ResponseType == ResponseType.SingleResponse) {
                return response.Message;
            }

            this.connection.Close();
            throw new IOException("JRPC2 did not respond correctly");
        }

        public async Task SetLEDs(bool p1, bool p2, bool p3, bool p4) {
            int bits = ((p4 ? 1 : 0) << 3) | ((p3 ? 1 : 0) << 2) | ((p2 ? 1 : 0) << 1) | (p1 ? 1 : 0);
            int value = bits * 16;
            string command = "consolefeatures ver=2 type=14 params=\"A\\0\\A\\4\\1\\0\\1\\0\\1\\0\\1\\" + value + "\\\"";
            ConsoleResponse response = await this.connection.SendCommand(command);
            if (response.ResponseType != ResponseType.SingleResponse || response.Message != "S_OK") {
                this.connection.Close();
                throw new IOException("JRPC2 did not respond correctly");
            }
        }

        private static RPCDataType TypeToRPCType<T>(bool Array) where T : struct {
            Type Type = typeof(T);
            if (Type == typeof(int) || Type == typeof(uint) || Type == typeof(short) || Type == typeof(ushort)) {
                if (Array)
                    return RPCDataType.IntArray;
                return RPCDataType.Int;
            }

            if (Type == typeof(string) || Type == typeof(char[]))
                return RPCDataType.String;
            if (Type == typeof(float) || Type == typeof(double)) {
                if (Array)
                    return RPCDataType.FloatArray;
                return RPCDataType.Float;
            }

            if (Type == typeof(byte) || Type == typeof(char)) {
                if (Array)
                    return RPCDataType.ByteArray;
                return RPCDataType.Byte;
            }

            if (Type == typeof(ulong) || Type == typeof(long)) {
                if (Array)
                    return RPCDataType.Uint64Array;
                return RPCDataType.Uint64;
            }

            return RPCDataType.Uint64;
        }

        public async Task<T> Call<T>(uint Address, params object[] Arguments) where T : struct {
            return (T) await this.CallArgs(true, TypeToRPCType<T>(false), typeof(T), null, 0, Address, 0, false, Arguments);
        }

        public async Task<T> Call<T>(string module, int ordinal, params object[] Arguments) where T : struct {
            return (T) await this.CallArgs(true, TypeToRPCType<T>(false), typeof(T), module, ordinal, 0, 0, false, Arguments);
        }

        public async Task<T> Call<T>(ThreadType thread, uint Address, params object[] Arguments) where T : struct {
            return (T) await this.CallArgs(thread == ThreadType.System, TypeToRPCType<T>(false), typeof(T), null, 0, Address, 0, false, Arguments);
        }

        public async Task<T> Call<T>(ThreadType thread, string module, int ordinal, params object[] Arguments) where T : struct {
            return (T) await this.CallArgs(thread == ThreadType.System, TypeToRPCType<T>(false), typeof(T), module, ordinal, 0, 0, false, Arguments);
        }

        public Task CallVoid(uint address, params object[] Arguments) {
            return this.CallArgs(true, RPCDataType.Void, typeof(void), null, 0, address, 0, false, Arguments);
        }

        public Task CallVoid(string module, int ordinal, params object[] Arguments) {
            return this.CallArgs(true, RPCDataType.Void, typeof(void), module, ordinal, 0, 0, false, Arguments);
        }

        public Task CallVoid(ThreadType Type, uint Address, params object[] Arguments) {
            return this.CallArgs(Type == ThreadType.System, RPCDataType.Void, typeof(void), null, 0, Address, 0, false, Arguments);
        }

        public Task CallVoid(ThreadType Type, string module, int ordinal, params object[] Arguments) {
            return this.CallArgs(Type == ThreadType.System, RPCDataType.Void, typeof(void), module, ordinal, 0, 0, false, Arguments);
        }

        public async Task<T[]> CallArray<T>(uint Address, uint ArraySize, params object[] Arguments) where T : struct {
            if (ArraySize == 0)
                return new T[1];
            return (T[]) await this.CallArgs(true, TypeToRPCType<T>(true), typeof(T), null, 0, Address, ArraySize, false, Arguments);
        }

        public async Task<T[]> CallArray<T>(string module, int ordinal, uint ArraySize, params object[] Arguments) where T : struct {
            if (ArraySize == 0)
                return new T[1];
            return (T[]) await this.CallArgs(true, TypeToRPCType<T>(true), typeof(T), module, ordinal, 0, ArraySize, false, Arguments);
        }

        public async Task<T[]> CallArray<T>(ThreadType Type, uint Address, uint ArraySize, params object[] Arguments) where T : struct {
            if (ArraySize == 0)
                return new T[1];
            return (T[]) await this.CallArgs(Type == ThreadType.System, TypeToRPCType<T>(true), typeof(T), null, 0, Address, ArraySize, false, Arguments);
        }

        public async Task<T[]> CallArray<T>(ThreadType Type, string module, int ordinal, uint ArraySize, params object[] Arguments) where T : struct {
            if (ArraySize == 0)
                return new T[1];
            return (T[]) await this.CallArgs(Type == ThreadType.System, TypeToRPCType<T>(true), typeof(T), module, ordinal, 0, ArraySize, false, Arguments);
        }

        public async Task<string> CallString(uint Address, params object[] Arguments) {
            return (string) await this.CallArgs(true, RPCDataType.String, typeof(string), null, 0, Address, 0, false, Arguments);
        }

        public async Task<string> CallString(string module, int ordinal, params object[] Arguments) {
            return (string) await this.CallArgs(true, RPCDataType.String, typeof(string), module, ordinal, 0, 0, false, Arguments);
        }

        public async Task<string> CallString(ThreadType Type, uint Address, params object[] Arguments) {
            return (string) await this.CallArgs(Type == ThreadType.System, RPCDataType.String, typeof(string), null, 0, Address, 0, false, Arguments);
        }

        public async Task<string> CallString(ThreadType Type, string module, int ordinal, params object[] Arguments) {
            return (string) await this.CallArgs(Type == ThreadType.System, RPCDataType.String, typeof(string), module, ordinal, 0, 0, false, Arguments);
        }

        private static byte[] IntArrayToByte(int[] iArray) {
            byte[] Bytes = new byte[iArray.Length * 4];
            for (int i = 0, q = 0; i < iArray.Length; i++, q += 4) {
                byte[] bytes = BitConverter.GetBytes(iArray[i]);
                for (int w = 0; w < 4; w++)
                    Bytes[q + w] = bytes[w];
            }

            return Bytes;
        }

        public async Task<T> CallVM<T>(uint Address, params object[] Arguments) where T : struct {
            return (T) await this.CallArgs(true, TypeToRPCType<T>(false), typeof(T), null, 0, Address, 0, true, Arguments);
        }

        public async Task<T> CallVM<T>(string module, int ordinal, params object[] Arguments) where T : struct {
            return (T) await this.CallArgs(true, TypeToRPCType<T>(false), typeof(T), module, ordinal, 0, 0, true, Arguments);
        }

        public async Task<T> CallVM<T>(ThreadType Type, uint Address, params object[] Arguments) where T : struct {
            return (T) await this.CallArgs(Type == ThreadType.System, TypeToRPCType<T>(false), typeof(T), null, 0, Address, 0, true, Arguments);
        }

        public async Task<T> CallVM<T>(ThreadType Type, string module, int ordinal, params object[] Arguments) where T : struct {
            return (T) await this.CallArgs(Type == ThreadType.System, TypeToRPCType<T>(false), typeof(T), module, ordinal, 0, 0, true, Arguments);
        }

        public Task CallVMVoid(uint Address, params object[] Arguments) {
            return this.CallArgs(true, RPCDataType.Void, typeof(void), null, 0, Address, 0, true, Arguments);
        }

        public Task CallVMVoid(string module, int ordinal, params object[] Arguments) {
            return this.CallArgs(true, RPCDataType.Void, typeof(void), module, ordinal, 0, 0, true, Arguments);
        }

        public Task CallVMVoid(ThreadType Type, uint Address, params object[] Arguments) {
            return this.CallArgs(Type == ThreadType.System, RPCDataType.Void, typeof(void), null, 0, Address, 0, true, Arguments);
        }

        public Task CallVMVoid(ThreadType Type, string module, int ordinal, params object[] Arguments) {
            return this.CallArgs(Type == ThreadType.System, RPCDataType.Void, typeof(void), module, ordinal, 0, 0, true, Arguments);
        }

        public async Task<T[]> CallVMArray<T>(uint Address, uint ArraySize, params object[] Arguments) where T : struct {
            if (ArraySize == 0)
                return new T[1];
            return (T[]) await this.CallArgs(true, TypeToRPCType<T>(true), typeof(T), null, 0, Address, ArraySize, true, Arguments);
        }

        public async Task<T[]> CallVMArray<T>(string module, int ordinal, uint ArraySize, params object[] Arguments) where T : struct {
            if (ArraySize == 0)
                return new T[1];
            return (T[]) await this.CallArgs(true, TypeToRPCType<T>(true), typeof(T), module, ordinal, 0, ArraySize, true, Arguments);
        }

        public async Task<T[]> CallVMArray<T>(ThreadType Type, uint Address, uint ArraySize, params object[] Arguments) where T : struct {
            if (ArraySize == 0)
                return new T[1];
            return (T[]) await this.CallArgs(Type == ThreadType.System, TypeToRPCType<T>(true), typeof(T), null, 0, Address, ArraySize, true, Arguments);
        }

        public async Task<T[]> CallVMArray<T>(ThreadType Type, string module, int ordinal, uint ArraySize, params object[] Arguments) where T : struct {
            if (ArraySize == 0)
                return new T[1];
            return (T[]) await this.CallArgs(Type == ThreadType.System, TypeToRPCType<T>(true), typeof(T), module, ordinal, 0, ArraySize, true, Arguments);
        }

        public async Task<string> CallVMString(uint Address, params object[] Arguments) {
            return (string) await this.CallArgs(true, RPCDataType.String, typeof(string), null, 0, Address, 0, true, Arguments);
        }

        public async Task<string> CallVMString(string module, int ordinal, params object[] Arguments) {
            return (string) await this.CallArgs(true, RPCDataType.String, typeof(string), module, ordinal, 0, 0, true, Arguments);
        }

        public async Task<string> CallVMString(ThreadType Type, uint Address, params object[] Arguments) {
            return (string) await this.CallArgs(Type == ThreadType.System, RPCDataType.String, typeof(string), null, 0, Address, 0, true, Arguments);
        }

        public async Task<string> CallVMString(ThreadType Type, string module, int ordinal, params object[] Arguments) {
            return (string) await this.CallArgs(Type == ThreadType.System, RPCDataType.String, typeof(string), module, ordinal, 0, 0, true, Arguments);
        }

        private async Task<string?> SendCommand(string Command) {
            ConsoleResponse response = await this.connection.SendCommand(Command);
            if (response.RawMessage.Contains("error="))
                throw new Exception(response.RawMessage.Substring(11));
            if (response.RawMessage.Contains("DEBUG"))
                throw new Exception("JRPC is not installed on the current console");
            if (response.ResponseType == ResponseType.InvalidArgument)
                return null;
            return response.RawMessage;
        }

        private async Task<object> CallArgs(bool onSystemThread, RPCDataType dataType, Type t, string? module, int ordinal, uint addr, uint arraySize, bool vm, params object[] arguments) {
            if (!IsValidReturnType(t))
                throw new Exception("Invalid type " + (object) t.Name + Environment.NewLine + "JRPC only supports: " + "bool, byte, short, int, long, ushort, uint, ulong, float, double");

            uint argc = 0;
            string cmdParams = CreateParams(vm, arguments, ref argc);
            if (argc > 37)
                throw new Exception("Can not use more than 37 paramaters in a call");

            string startSendCMD = $"consolefeatures ver=2 " +
                                  $"type={(uint) dataType}" +
                                  $"{(onSystemThread ? " system" : "")}" +
                                  $"{(module != null ? (" module=\"" + module + "\" ord=" + ordinal) : "")}" +
                                  $"{(vm ? " VM" : "")} " +
                                  $"as={arraySize} " +
                                  $"params=\"A\\{addr:X}\\A\\{argc}\\{cmdParams}";

            const string findText = "buf_addr=";
            string? response = await this.SendCommand(startSendCMD);
            if (response == null)
                throw new Exception("Invalid argument");
            
            while (response.Contains(findText)) {
                await Task.Delay(100);
                uint address = uint.Parse(response.AsSpan(response.IndexOf(findText) + findText.Length), NumberStyles.HexNumber);
                response = await this.SendCommand("consolefeatures " + findText + "0x" + address.ToString("X"));
                if (response == null)
                    throw new Exception("Invalid argument");
            }

            switch (dataType) {
                case RPCDataType.Int:
                    uint uVal = uint.Parse(response.AsSpan(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
                    if (t == typeof(uint))
                        return uVal;
                    if (t == typeof(int))
                        return (int) uVal;
                    if (t == typeof(short))
                        return short.Parse(response.AsSpan(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
                    if (t == typeof(ushort))
                        return ushort.Parse(response.AsSpan(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
                    break;
                case RPCDataType.String:
                    string sString = response.Substring(response.IndexOf(' ') + 1);
                    if (t == typeof(string))
                        return sString;
                    if (t == typeof(char[]))
                        return sString.ToCharArray();
                    break;
                case RPCDataType.Float:
                    if (t == typeof(double))
                        return double.Parse(response.AsSpan(response.IndexOf(' ') + 1));
                    if (t == typeof(float))
                        return float.Parse(response.AsSpan(response.IndexOf(' ') + 1));
                    break;
                case RPCDataType.Byte:
                    byte bByte = byte.Parse(response.AsSpan(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
                    if (t == typeof(byte))
                        return bByte;
                    if (t == typeof(char))
                        return (char) bByte;
                    break;
                case RPCDataType.Uint64:
                    if (t == typeof(long))
                        return long.Parse(response.AsSpan(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
                    if (t == typeof(ulong))
                        return ulong.Parse(response.AsSpan(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
                    break;
                case RPCDataType.IntArray: {
                    string String = response.Substring(response.IndexOf(' ') + 1);
                    int Tmp = 0;
                    string Temp = "";
                    uint[] Uarray = new uint[8];
                    foreach (char Char1 in String) {
                        if (Char1 != ',' && Char1 != ';')
                            Temp += Char1.ToString();
                        else {
                            Uarray[Tmp] = uint.Parse(Temp, NumberStyles.HexNumber);
                            Tmp += 1;
                            Temp = "";
                        }

                        if (Char1 == ';')
                            break;
                    }

                    return Uarray;
                }
                case RPCDataType.FloatArray: {
                    string String = response.Substring(response.IndexOf(' ') + 1);
                    int Tmp = 0;
                    string Temp = "";
                    float[] Farray = new float[arraySize];
                    foreach (char Char1 in String) {
                        if (Char1 != ',' && Char1 != ';')
                            Temp += Char1.ToString();
                        else {
                            Farray[Tmp] = float.Parse(Temp);
                            Tmp += 1;
                            Temp = "";
                        }

                        if (Char1 == ';')
                            break;
                    }

                    return Farray;
                }
                case RPCDataType.ByteArray: {
                    string String = response.Substring(response.IndexOf(' ') + 1);
                    int Tmp = 0;
                    string Temp = "";
                    byte[] Barray = new byte[arraySize];
                    foreach (char Char1 in String) {
                        if (Char1 != ',' && Char1 != ';')
                            Temp += Char1.ToString();
                        else {
                            Barray[Tmp] = byte.Parse(Temp);
                            Tmp += 1;
                            Temp = "";
                        }

                        if (Char1 == ';')
                            break;
                    }

                    return Barray;
                }
                case RPCDataType.Uint64Array: {
                    string str = response.Substring(response.IndexOf(' ') + 1);
                    int Tmp = 0;
                    string Temp = "";
                    ulong[] ulongArray = new ulong[arraySize];
                    foreach (char ch in str) {
                        if (ch != ',' && ch != ';')
                            Temp += ch.ToString();
                        else {
                            ulongArray[Tmp] = ulong.Parse(Temp);
                            Tmp += 1;
                            Temp = "";
                        }

                        if (ch == ';')
                            break;
                    }

                    if (t == typeof(ulong)) {
                        return ulongArray;
                    }
                    else if (t == typeof(long)) {
                        long[] longArray = new long[arraySize];
                        for (int i = 0; i < arraySize; i++)
                            longArray[i] = (long) ulongArray[i];
                        return longArray;
                    }

                    break;
                }
                case RPCDataType.Void: return 0;
            }

            return ulong.Parse(response.Substring(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
        }

        // private static string CreateParams(bool vm, object[] arguments, ref uint argc) {
        //     StringBuilder sbParams = new StringBuilder(128);
        //     foreach (object obj in arguments) {
        //         switch (obj) {
        //             case uint u:
        //                 sbParams.Append((uint) RPCDataType.Int).Append("\\" + (int) u).Append("\\");
        //                 argc += 1;
        //                 break;
        //             case int:
        //             case bool:
        //             case byte: {
        //                 if (obj is bool) {
        //                     sbParams.Append((uint) RPCDataType.Int).Append('\\').Append((bool) obj ? '1' : '0').Append('\\');
        //                 }
        //                 else {
        //                     sbParams.Append((uint) RPCDataType.Int).Append('\\' + (obj is byte ? Convert.ToByte(obj).ToString() : Convert.ToInt32(obj).ToString())).Append('\\');
        //                 }
        //
        //                 argc += 1;
        //                 break;
        //             }
        //             case int[]:
        //             case uint[]: {
        //                 if (!vm) {
        //                     byte[] bytes = IntArrayToByte((int[]) obj);
        //                     sbParams.Append((uint) RPCDataType.ByteArray).Append("/" + bytes.Length).Append('\\');
        //                     foreach (byte b in bytes)
        //                         sbParams.Append(b.ToString("X2"));
        //
        //                     sbParams.Append('\\');
        //                     argc += 1;
        //                 }
        //                 else {
        //                     bool isInt = obj is int[];
        //                     int len;
        //                     if (isInt) {
        //                         int[] iarray = (int[]) obj;
        //                         len = iarray.Length;
        //                     }
        //                     else {
        //                         uint[] iarray = (uint[]) obj;
        //                         len = iarray.Length;
        //                     }
        //
        //                     int[] Iarray = new int[len];
        //                     for (int i = 0; i < len; i++) {
        //                         if (isInt) {
        //                             int[] tiarray = (int[]) obj;
        //                             Iarray[i] = tiarray[i];
        //                         }
        //                         else {
        //                             uint[] tiarray = (uint[]) obj;
        //                             Iarray[i] = (int) tiarray[i];
        //                         }
        //
        //                         sbParams.Append((uint) RPCDataType.Int).Append('\\' + Iarray[i]).Append('\\');
        //                         argc += 1;
        //                     }
        //                 }
        //
        //                 break;
        //             }
        //             case string s: {
        //                 string Str = s;
        //                 sbParams.Append((uint) RPCDataType.ByteArray).Append("/" + Str.Length).Append('\\' + NumberUtils.ConvertStringToHex(s, Encoding.ASCII)).Append('\\');
        //                 argc += 1;
        //                 break;
        //             }
        //             case double d1: {
        //                 double d = d1;
        //                 sbParams.Append((uint) RPCDataType.Float).Append('\\' + d.ToString()).Append('\\');
        //                 argc += 1;
        //                 break;
        //             }
        //             case float f: {
        //                 float Fl = f;
        //                 sbParams.Append((uint) RPCDataType.Float).Append('\\' + Fl.ToString()).Append('\\');
        //                 argc += 1;
        //                 break;
        //             }
        //             case float[] floats: {
        //                 float[] floatArray = floats;
        //                 if (!vm) {
        //                     sbParams.Append((uint) RPCDataType.ByteArray).Append("/" + (floatArray.Length * 4).ToString()).Append('\\');
        //                     foreach (float f in floatArray) {
        //                         byte[] bytes = BitConverter.GetBytes(f);
        //                         Array.Reverse(bytes);
        //                         for (int q = 0; q < 4; q++)
        //                             sbParams.Append(bytes[q].ToString("X2"));
        //                     }
        //
        //                     sbParams.Append('\\');
        //                     argc += 1;
        //                 }
        //                 else {
        //                     foreach (float f in floatArray) {
        //                         sbParams.Append((uint) RPCDataType.Float).Append('\\' + f.ToString()).Append('\\');
        //                         argc += 1;
        //                     }
        //                 }
        //
        //                 break;
        //             }
        //             case byte[] bytes: {
        //                 byte[] ByteArray = bytes;
        //                 sbParams.Append(ByteArray.ToString()).Append("/" + ByteArray.Length).Append('\\');
        //                 foreach (byte b in ByteArray)
        //                     sbParams.Append(b.ToString("X2"));
        //
        //                 sbParams.Append('\\');
        //                 argc += 1;
        //                 break;
        //             }
        //             default: {
        //                 sbParams.Append((uint) RPCDataType.Uint64).Append('\\').Append(ConvertToUInt64(obj)).Append('\\');
        //                 argc += 1;
        //                 break;
        //             }
        //         }
        //     }
        //
        //     sbParams.Append('\"');
        //     return sbParams.ToString();
        // }
        //

        private static string CreateParams(bool vm, object[] arguments, ref uint argc) {
            string SendCMD = "";
            foreach (object obj in arguments) {
                bool Done = false;
                if (obj is uint) {
                    SendCMD += ((uint) RPCDataType.Int) + "\\" + UIntToInt((uint) obj) + "\\";
                    argc += 1;
                    Done = true;
                }

                if (obj is int || obj is bool || obj is byte) {
                    if (obj is bool) {
                        SendCMD += ((uint) RPCDataType.Int) + "\\" + Convert.ToInt32((bool) obj) + "\\";
                    }
                    else {
                        SendCMD += ((uint) RPCDataType.Int) + "\\" + ((obj is byte) ? Convert.ToByte(obj).ToString() : Convert.ToInt32(obj).ToString()) + "\\";
                    }

                    argc += 1;
                    Done = true;
                }
                else if (obj is int[] || obj is uint[]) {
                    if (!vm) {
                        byte[] Array = IntArrayToByte((int[]) obj);
                        SendCMD += ((uint) RPCDataType.ByteArray).ToString() + "/" + Array.Length + "\\";
                        for (int i = 0; i < Array.Length; i++)
                            SendCMD += Array[i].ToString("X2");
                        SendCMD += "\\";
                        argc += 1;
                    }
                    else {
                        bool isInt = obj is int[];
                        int len;
                        if (isInt) {
                            int[] iarray = (int[]) obj;
                            len = iarray.Length;
                        }
                        else {
                            uint[] iarray = (uint[]) obj;
                            len = iarray.Length;
                        }

                        int[] Iarray = new int[len];
                        for (int i = 0; i < len; i++) {
                            if (isInt) {
                                int[] tiarray = (int[]) obj;
                                Iarray[i] = tiarray[i];
                            }
                            else {
                                uint[] tiarray = (uint[]) obj;
                                Iarray[i] = UIntToInt(tiarray[i]);
                            }

                            SendCMD += ((uint) RPCDataType.Int) + "\\" + Iarray[i] + "\\";
                            argc += 1;
                        }
                    }

                    Done = true;
                }
                else if (obj is string) {
                    string Str = (string) obj;
                    SendCMD += ((uint) RPCDataType.ByteArray).ToString() + "/" + Str.Length + "\\" + ToHexString((string)obj) + "\\";
                    argc += 1;
                    Done = true;
                }
                else if (obj is double) {
                    double d = (double) obj;
                    SendCMD += ((uint) RPCDataType.Float).ToString() + "\\" + d.ToString() + "\\";
                    argc += 1;
                    Done = true;
                }
                else if (obj is float) {
                    float Fl = (float) obj;
                    SendCMD += ((uint) RPCDataType.Float).ToString() + "\\" + Fl.ToString() + "\\";
                    argc += 1;
                    Done = true;
                }
                else if (obj is float[]) {
                    float[] floatArray = (float[]) obj;
                    if (!vm) {
                        SendCMD += ((uint) RPCDataType.ByteArray).ToString() + "/" + (floatArray.Length * 4).ToString() + "\\";
                        for (int i = 0; i < floatArray.Length; i++) {
                            byte[] bytes = BitConverter.GetBytes(floatArray[i]);
                            Array.Reverse(bytes);
                            for (int q = 0; q < 4; q++)
                                SendCMD += bytes[q].ToString("X2");
                        }

                        SendCMD += "\\";
                        argc += 1;
                    }
                    else {
                        for (int i = 0; i < floatArray.Length; i++) {
                            SendCMD += ((uint) RPCDataType.Float).ToString() + "\\" + floatArray[i].ToString() + "\\";
                            argc += 1;
                        }
                    }

                    Done = true;
                }
                else if (obj is byte[]) {
                    byte[] ByteArray = (byte[]) obj;
                    SendCMD += ((uint) RPCDataType.ByteArray).ToString() + "/" + ByteArray.Length + "\\";
                    for (int i = 0; i < ByteArray.Length; i++)
                        SendCMD += ByteArray[i].ToString("X2");
                    SendCMD += "\\";
                    argc += 1;
                    Done = true;
                }

                if (!Done) {
                    SendCMD += ((uint) RPCDataType.Uint64).ToString() + "\\" + ConvertToUInt64(obj).ToString() + "\\";
                    argc += 1;
                }
            }

            SendCMD += "\"";
            return SendCMD;
        }
        
        public static string ToHexString(string String)
        {
            string Str = "";
            foreach (byte b in String)
                Str += b.ToString("X2");
            return Str;
        }
        
        private static int UIntToInt(uint Value)
        {
            byte[] array = BitConverter.GetBytes(Value);
            return BitConverter.ToInt32(array, 0);
        }

        private static readonly HashSet<Type> ValidReturnTypes = new HashSet<Type>() {
            typeof(void),
            typeof(bool),
            typeof(byte),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(ushort),
            typeof(uint),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(string),
            typeof(bool[]),
            typeof(byte[]),
            typeof(short[]),
            typeof(int[]),
            typeof(long[]),
            typeof(ushort[]),
            typeof(uint[]),
            typeof(ulong[]),
            typeof(float[]),
            typeof(double[]),
            typeof(string[])
        };

        public static bool IsValidReturnType(Type t) {
            return ValidReturnTypes.Contains(t);
        }

        // private static ulong ConvertToUInt64(object o) {
        //     return o switch {
        //         bool bo => !bo ? 0UL : 1UL,
        //         byte b => b,
        //         short s => (ulong) s,
        //         int i => (ulong) i,
        //         long l => (ulong) l,
        //         ushort us => us,
        //         uint u => u,
        //         ulong ul => ul,
        //         float f => (ulong) BitConverter.DoubleToInt64Bits(f),
        //         double d => (ulong) BitConverter.DoubleToInt64Bits(d),
        //         _ => 0UL
        //     };
        // }

        private static ulong ConvertToUInt64(object o) {
            if (o is bool)
                return (ulong) (!(bool) o ? 0UL : 1UL);
            else {
                if (o is byte)
                    return (ulong) (byte) o;
                if (o is short)
                    return (ulong) (short) o;
                if (o is int)
                    return (ulong) (int) o;
                if (o is long)
                    return (ulong) (long) o;
                if (o is ushort)
                    return (ulong) (ushort) o;
                if (o is uint)
                    return (ulong) (uint) o;
                if (o is ulong)
                    return (ulong) o;
                if (o is float)
                    return (ulong) BitConverter.DoubleToInt64Bits((double) (float) o);
                if (o is double)
                    return (ulong) BitConverter.DoubleToInt64Bits((double) o);
                else
                    return 0UL;
            }
        }
    }

    private class XbdmFeaturesImpl : IConsoleFeature, IFeatureXbox360Xbdm, IFeatureXboxDebugging, IFeatureSystemEvents {
        private readonly XbdmConsoleConnection connection;

        public IConsoleConnection Connection => this.connection;

        public XbdmFeaturesImpl(XbdmConsoleConnection connection) {
            this.connection = connection;
        }

        public Task<XboxThread> GetThreadInfo(uint threadId, bool requireName = true) {
            return this.connection.GetThreadInfo(threadId, requireName);
        }

        public async Task<List<XboxThread>> GetThreadDump(bool requireNames = true) {
            return await this.connection.GetThreadDump(requireNames);
        }

        public Task RebootConsole(bool cold = true) => this.connection.RebootConsole(cold);

        public Task ShutdownConsole() => this.connection.ShutdownConsole();

        public Task EjectDisk() => this.connection.SendCommand("dvdeject");

        public Task<FreezeResult> DebugFreeze() => this.connection.DebugFreeze();

        public Task<UnFreezeResult> DebugUnFreeze() => this.connection.DebugUnFreeze();

        public async Task<bool?> IsFrozen() {
            XboxExecutionState state = await this.GetExecutionState();
            return state == XboxExecutionState.Stop;
        }

        public Task DeleteFile(string path) => this.connection.DeleteFile(path);

        public Task LaunchFile(string path) => this.connection.LaunchFile(path);

        public Task<string> GetConsoleID() => this.connection.GetConsoleID();

        public Task<string> GetDebugName() => this.connection.GetDebugName();

        public Task<string?> GetXbeInfo(string? executable) => this.connection.GetXbeInfo(executable);

        public Task<List<MemoryRegion>> GetMemoryRegions(bool willRead, bool willWrite) => this.connection.GetMemoryRegions(willRead, willWrite);

        public Task<XboxExecutionState> GetExecutionState() => this.connection.GetExecutionState();

        public Task<XboxHardwareInfo> GetHardwareInfo() => this.connection.GetHardwareInfo();

        public Task<uint> GetProcessID() => this.connection.GetProcessID();

        public Task<IPAddress> GetTitleIPAddress() => this.connection.GetTitleIPAddress();

        public Task SetConsoleColor(ConsoleColor colour) => this.connection.SetConsoleColor(colour);

        public Task SetDebugName(string newName) => this.connection.SetDebugName(newName);

        public Task AddBreakpoint(uint address) => this.connection.AddBreakpoint(address);

        public Task AddDataBreakpoint(uint address, XboxBreakpointType type, uint size) => this.connection.AddDataBreakpoint(address, type, size);

        public Task RemoveBreakpoint(uint address) => this.connection.RemoveBreakpoint(address);

        public Task RemoveDataBreakpoint(uint address, XboxBreakpointType type, uint size) => this.connection.RemoveDataBreakpoint(address, type, size);

        public Task<List<RegisterEntry>?> GetRegisters(uint threadId) => this.connection.GetRegisters(threadId);

        public Task<RegisterEntry?> ReadRegisterValue(uint threadId, string registerName) => this.connection.ReadRegisterValue(threadId, registerName);

        public Task SuspendThread(uint threadId) => this.connection.SuspendThread(threadId);

        public Task ResumeThread(uint threadId) => this.connection.ResumeThread(threadId);

        public Task StepThread(uint threadId) => this.connection.StepThread(threadId);

        public Task<ConsoleModule?> GetModuleForAddress(uint address, bool bNeedSections) => this.connection.GetModuleForAddress(address, bNeedSections);

        public Task<FunctionCallEntry?[]> FindFunctions(uint[] iar) => this.connection.FindFunctions(iar);

        public IDisposable SubscribeToEvents(ConsoleSystemEventHandler handler) => this.connection.SubscribeToEvents(handler);
    }
}