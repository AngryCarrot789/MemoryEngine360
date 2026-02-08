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
using MemEngine360.Engine.Events;
using MemEngine360.Engine.Events.XbdmEvents;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Utils;
using ConsoleColor = MemEngine360.Connections.Features.ConsoleColor;

namespace MemEngine360.Xbox360XBDM.Consoles.Xbdm;

public partial class XbdmConsoleConnection : BaseConsoleConnection, INetworkConsoleConnection {
    private readonly struct ThreadedBinaryReadInfo(byte[] dstBuffer, int offset, int count, TaskCompletionSource completion, CancellationToken cancellation) {
        public readonly byte[] dstBuffer = dstBuffer;
        public readonly int offset = offset;
        public readonly int count = count;
        public readonly TaskCompletionSource completion = completion;
        public readonly CancellationToken cancellation = cancellation;
    }

    private readonly struct ThreadedStringLineReadInfo(TaskCompletionSource<string> completion, CancellationToken cancellation) {
        public readonly TaskCompletionSource<string> completion = completion;
        public readonly CancellationToken cancellation = cancellation;
    }

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

    private readonly List<EventHandler<ConsoleSystemEventArgs>> systemEventHandlers = new List<EventHandler<ConsoleSystemEventArgs>>();
    private readonly Lock debugHelperLock = new Lock();
    private int systemEventSubscribeCount;
    private ThreadedEventListener? threadedDebugHelper;

    private bool isWaitingForNewLine;
    private int idxBeginLnBuf, idxEndLnBuf;

    private readonly AutoResetEvent readEvent = new AutoResetEvent(false);
    private volatile int readType;
    private ThreadedBinaryReadInfo readInfo_binary;
    private ThreadedStringLineReadInfo readInfo_string;
    private readonly string originalConnectionAddress;

    public EndPoint? EndPoint => !this.IsClosed ? this.client.Client.RemoteEndPoint : null;

    public override RegisteredConnectionType ConnectionType => ConnectionTypeXbox360Xbdm.Instance;

    public override bool IsLittleEndian { get; }

    public override AddressRange AddressableRange => new AddressRange(0, uint.MaxValue);
    
    private readonly XbdmFeaturesImpl xbdmFeatures;
    private Jrpc2FeaturesImpl? jrpcFeatures;

    public XbdmConsoleConnection(bool isLittleEndian, TcpClient client, string originalConnectionAddress) : this(isLittleEndian, client, originalConnectionAddress, false) {
    }

    private XbdmConsoleConnection(bool isLittleEndian, TcpClient client, string originalConnectionAddress, bool isEventConnection) {
        this.IsLittleEndian = isLittleEndian;
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

    public override int GetRecommendedReadChunkSize(int readTotal) {
        return readTotal < 128 ? 128 : Math.Min(readTotal, 0xC000 /* 48K, 3/4th of the receive buffer */);
    }

    /// <summary>
    /// Attempts to figure out additional features, such as JRPC2 being installed 
    /// </summary>
    public async Task DetectDynamicFeatures() {
        if (!IsJRPC2DetectionBroken) {
            const string GetCpuSensorCommand = "consolefeatures ver=2 type=15 params=\"A\\0\\A\\1\\1\\0\\\"";
            XbdmResponse response = await this.SendCommand(GetCpuSensorCommand);
            if (response.ResponseType == XbdmResponseType.SingleResponse) {
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

    public override bool HasFeature(Type typeOfFeature) {
        return typeOfFeature.IsInstanceOfType(this.xbdmFeatures) ||
               typeOfFeature.IsInstanceOfType(this.jrpcFeatures) ||
               base.HasFeature(typeOfFeature);
    }

    protected override void CloseOverride() {
        try {
            this.ctsCheckClosed.Cancel();
            this.ctsCheckClosed.Dispose();
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

    public async Task<XbdmResponse> GetResponseOnly() {
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

    public async Task<XbdmResponse> SendCommand(string command) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        return await this.InternalSendCommand(command).ConfigureAwait(false);
    }

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

        XbdmResponse response = await this.InternalSendCommand(command).ConfigureAwait(false);
        int idx = command.IndexOf(' ');
        VerifyResponse(idx == -1 ? command : command.Substring(0, idx), response.ResponseType, XbdmResponseType.MultiResponse);
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
            XbdmResponse response = await this.InternalSendCommand($"threadinfo thread=0x{threadId:X8}").ConfigureAwait(false);
            if (response.ResponseType == XbdmResponseType.NoSuchThread)
                return default;
            VerifyResponse("threadinfo", response.ResponseType, XbdmResponseType.MultiResponse);
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

        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        foreach (string threadId in list) {
            XboxThread tdInfo = new XboxThread {
                id = (uint) int.Parse(threadId)
            };

            XbdmResponse response = await this.InternalSendCommand($"threadinfo thread=0x{tdInfo.id:X8}").ConfigureAwait(false);
            if (response.ResponseType != XbdmResponseType.MultiResponse) {
                VerifyResponse("threadinfo", response.ResponseType, XbdmResponseType.NoSuchThread);
                continue;
            }

            List<string> info = await this.InternalReadMultiLineResponse();
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
                    tdInfo.readableName = await this.ReadAsciiCore(tdInfo.nameAddress, (int) tdInfo.nameLength);
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
        XbdmResponse response = await this.SendCommand("stop");
        if (response.ResponseType == XbdmResponseType.SingleResponse)
            return FreezeResult.Success;

        VerifyResponse("stop", response.ResponseType, XbdmResponseType.XBDM_ALREADYSTOPPED);
        return FreezeResult.AlreadyFrozen;
    }

    public async Task<UnFreezeResult> DebugUnFreeze() {
        XbdmResponse response = await this.SendCommand("go");
        if (response.ResponseType == XbdmResponseType.SingleResponse)
            return UnFreezeResult.Success;

        VerifyResponse("go", response.ResponseType, XbdmResponseType.NotStopped);
        return UnFreezeResult.AlreadyUnfrozen;
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
        XbdmResponse response = await this.SendCommand("getpid").ConfigureAwait(false);
        VerifyResponse("getpid", response.ResponseType, XbdmResponseType.SingleResponse);
        ParamUtils.GetDwParam(response.Message, "pid", true, out uint pid);
        return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(pid) : pid;
    }

    public async Task<IPAddress> GetTitleIPAddress() {
        XbdmResponse response = await this.SendCommand("altaddr").ConfigureAwait(false);
        VerifyResponse("altaddr", response.ResponseType, XbdmResponseType.SingleResponse);
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

    public override async Task<bool?> IsMemoryInvalidOrProtected(uint address, int count) {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        if (count == 0) {
            return false;
        }

        this.FillGetMemCommandBuffer(address, (uint) count);
        await this.InternalWriteBytes(this.sharedGetMemCommandBuffer).ConfigureAwait(false);
        XbdmResponse response = await this.InternalReadResponse().ConfigureAwait(false);
        VerifyResponse("getmem", response.ResponseType, XbdmResponseType.MultiResponse);

        string line;
        while ((line = await this.InternalReadLine().ConfigureAwait(false)) != ".") {
            if (line.Contains('?')) {
                return true;
            }
        }

        return false;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RUNTIME_FUNCTION {
        [FieldOffset(0)] public uint BeginAddress;

        [FieldOffset(4)] public uint EndAddress;

        // union {
        [FieldOffset(8)] public uint UnwindInfoAddress;

        [FieldOffset(8)] public uint UnwindData;

        // }
        [FieldOffset(12)] private uint Padding;

        static RUNTIME_FUNCTION() {
            Debug.Assert(Unsafe.SizeOf<RUNTIME_FUNCTION>() == 16);
        }

        public static bool Contains(uint address, uint begin, uint end) {
            return address >= begin && address < end;
        }

        public override string ToString() {
            return $"{this.BeginAddress:X8} -> {this.EndAddress:X8} (Length: {this.EndAddress - this.BeginAddress:X})";
        }
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
        XbdmResponse response = await this.InternalReadResponse().ConfigureAwait(false);
        VerifyResponse("getmem", response.ResponseType, XbdmResponseType.MultiResponse);
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

    private async Task NewReadBytes(uint address, byte[] dstBuffer, int offset, int count) {
        if (count <= 0) {
            return;
        }

        XbdmResponse response = await this.InternalSendCommand($"getmemex addr=0x{address:X8} length=0x{count:X8}").ConfigureAwait(false);
        VerifyResponse("getmemex", response.ResponseType, XbdmResponseType.BinaryResponse);

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

    // Rather than potentially lock up the connection, allow cancellation but it might corrupt the connection.
    public async Task<byte[]> ReceiveBinaryData(CancellationToken cancellation) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        const int BufferSize = 1024;
        using MemoryStream memoryStream = new MemoryStream(BufferSize);
        using var _ = ArrayPools.Rent(BufferSize, out byte[] array);

        int statusFlag;
        do {
            await this.ReadFromBufferOrStreamAsync(this.sharedTwoByteArray, 0, 2, cancellation);

            int header = MemoryMarshal.Read<ushort>(new ReadOnlySpan<byte>(this.sharedTwoByteArray, 0, 2));
            int chunkSize = header & 0x7FFF;
            statusFlag = header & 0x8000;
            if (chunkSize <= 0) {
                break;
            }
            
            for (int count = chunkSize; count > 0; count -= BufferSize) {
                int read = Math.Min(count, BufferSize);
                await this.ReadFromBufferOrStreamAsync(array, 0, read, cancellation);
                memoryStream.Write(array, 0, read);
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
            XbdmResponse response = await this.InternalReadResponse().ConfigureAwait(false);
            if (response.ResponseType != XbdmResponseType.SingleResponse && response.ResponseType != XbdmResponseType.MemoryNotMapped) {
                VerifyResponse("setmem", response.ResponseType, XbdmResponseType.SingleResponse);
            }

            cbReadTotal += cbToWrite;
            count -= cbToWrite;
        }
    }

    private async Task<XbdmResponse> InternalReadResponse_Threaded() {
        string responseText = await this.InternalReadLine_Threaded().ConfigureAwait(false);
        return XbdmResponse.FromLine(responseText);
    }

    private async Task<XbdmResponse> InternalReadResponse() {
        string responseText = await this.InternalReadLine().ConfigureAwait(false);
        return XbdmResponse.FromLine(responseText);
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

    private async Task<XbdmResponse> InternalSendCommand(string command) {
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
        NumberUtils.UInt32ToHexAscii(address, ref dstAscii, 14 /* magic */);

        int i = 28 /* magic */;
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
        NumberUtils.UInt32ToHexAscii(address, ref dstAscii, 14 /* magic */);
        NumberUtils.UInt32ToHexAscii(count, ref dstAscii, 32 /* magic */);
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
        this.readInfo_string = new ThreadedStringLineReadInfo(tcs, token);
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
        this.readInfo_binary = new ThreadedBinaryReadInfo(buffer, offset, count, tcs, cancellation);
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
        ThreadedStringLineReadInfo info = this.readInfo_string;
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
        ThreadedBinaryReadInfo info = this.readInfo_binary;
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

    private static void VerifyResponse(string commandName, XbdmResponseType actual, XbdmResponseType expected) {
        if (actual != expected) {
            throw InvalidResponseException.ForCommand(commandName, actual, expected);
        }
    }

    public IDisposable SubscribeToEvents(EventHandler<ConsoleSystemEventArgs> handler) {
        ArgumentNullException.ThrowIfNull(handler);
        if (this.isEventConnection)
            throw new InvalidOperationException("Attempt to subscribe to events on an event connection");

        lock (this.debugHelperLock) {
            if (++this.systemEventSubscribeCount == 1) {
                Debug.Assert(this.threadedDebugHelper == null);
                this.threadedDebugHelper = new ThreadedEventListener(this, this.originalConnectionAddress);
            }
        }

        lock (this.systemEventHandlers) {
            this.systemEventHandlers.Add(handler);
        }

        return new EventSubscriber(this, handler);
    }

    private void UnsubscribeFromEvents(EventHandler<ConsoleSystemEventArgs> handler) {
        lock (this.systemEventHandlers) {
            this.systemEventHandlers.Remove(handler);
        }

        lock (this.debugHelperLock) {
            if (--this.systemEventSubscribeCount == 0) {
                this.threadedDebugHelper!.SignalStop();
                this.threadedDebugHelper = null;
            }
        }
    }

    private class EventSubscriber : IDisposable {
        private volatile XbdmConsoleConnection? connection;
        private readonly EventHandler<ConsoleSystemEventArgs> handler;

        public EventSubscriber(XbdmConsoleConnection connection, EventHandler<ConsoleSystemEventArgs> handler) {
            this.connection = connection;
            this.handler = handler;
        }

        public void Dispose() {
            XbdmConsoleConnection? conn = Interlocked.Exchange(ref this.connection, null);
            conn?.UnsubscribeFromEvents(this.handler);
        }
    }

    private class ThreadedEventListener {
        private readonly string originalConnectionAddress;
        private readonly XbdmConsoleConnection connection;
        private CancellationTokenSource? ctsStop;
        private readonly CancellationToken tokenStopThread;

        public ThreadedEventListener(XbdmConsoleConnection connection, string originalConnectionAddress) {
            this.originalConnectionAddress = originalConnectionAddress;
            this.connection = connection;
            this.ctsStop = new CancellationTokenSource();
            this.tokenStopThread = this.ctsStop.Token;
            new Thread(this.EventListenerThreadMain) {
                IsBackground = true, Name = "Xbdm Event Listener Thread"
            }.Start();
        }

        public void SignalStop() {
            CancellationTokenSource? cts = Interlocked.Exchange(ref this.ctsStop, null);
            cts?.Cancel();
        }

        private void EventListenerThreadMain() {
            TcpClient? tcpClient = null;
            CancellationTokenSource? cts = this.ctsStop;
            if (cts == null) {
                AppLogger.Instance.WriteLine("Event Listener Thread stopped before it started");
                return;
            }

            try {
                AppLogger.Instance.WriteLine("Event Listener Thread Started");
                // This method is extremely janky and needs a rewrite. We shouldn't use a delegate
                // XbdmConsoleConnection, but instead make some static methods that take TcpClient that XbdmConsoleConnection use

                tcpClient = new TcpClient();
                tcpClient.ReceiveTimeout = 0;
                tcpClient.Connect(this.originalConnectionAddress, 730);

                using (StreamReader cmdReader = new StreamReader(tcpClient.GetStream(), Encoding.ASCII, leaveOpen: true)) {
                    string? text = cmdReader.ReadLine()?.ToLower();
                    if (text != "201- connected") {
                        AppLogger.Instance.WriteLine(Thread.CurrentThread.Name + " failed to start: acknowledgement incorrect");
                        return;
                    }
                }

                XbdmConsoleConnection delegateConnection = new XbdmConsoleConnection(this.connection.IsLittleEndian, tcpClient, this.originalConnectionAddress);

                // DEBUGGER CONNECT PORT=0x<PORT_HERE> override user=<COMPUTER_NAME_HERE> name="MemEngine360"
                XbdmResponse response = delegateConnection.SendCommand($"debugger connect override name=\"MemoryEngine360\" user=\"{Environment.MachineName}\"").GetAwaiter().GetResult();
                if (response.ResponseType != XbdmResponseType.SingleResponse) {
                    throw new Exception($"Failed to enable debugger. Response = {response.ToString()}");
                }

                // we must repeat this since if the xbox is spewing events, it seems like it does it in the background. So even if we use
                // delegateConnection.SendMultipleCommands, there's still a chance we get one of the events in the responses.
                // 
                // Also, the xbox sends the current execution state as soon as we connect the debugger,
                // so this list will most likely contain that event
                List<XbdmEventArgs>? initialEvents = new List<XbdmEventArgs>();

                // no idea what reconnectport does, surely it's not the port it tries to reconnect on
                delegateConnection.SendCommandOnly("notify reconnectport=12345 reverse").GetAwaiter().GetResult();
                while (true) {
                    string responseText = delegateConnection.GetResponseAsTextOnly().GetAwaiter().GetResult();
                    if (XbdmResponse.TryParseFromLine(responseText, out response)) {
                        if (response.ResponseType != XbdmResponseType.DedicatedConnection) {
                            throw new Exception($"Failed to setup notifications. Response type is not {nameof(XbdmResponseType.DedicatedConnection)}: {response.RawMessage}");
                        }

                        break;
                    }

                    initialEvents.Add(XbdmEventUtils.ParseSpecial(responseText) ?? new XbdmEventArgs(responseText));
                }

                if (this.tokenStopThread.IsCancellationRequested) {
                    return;
                }

                if (initialEvents.Count > 0) {
                    // Run event handlers in a task to prevent them accessing
                    // the current thread and potentially interrupting it
                    this.InvokeHandlersAndWait(initialEvents.ToArray());
                    initialEvents = null;
                }

                while (!delegateConnection.IsClosed && !this.tokenStopThread.IsCancellationRequested) {
                    if (tcpClient.Available < 1)
                        Thread.Sleep(10);
                    if (this.tokenStopThread.IsCancellationRequested)
                        return;

                    if (tcpClient.Available > 0) {
                        string line;
                        try {
                            line = delegateConnection.ReadLineFromStream(cts.Token).GetAwaiter().GetResult();
                        }
                        catch (OperationCanceledException) {
                            return; // stop requested
                        }
                        catch (Exception) {
                            continue;
                        }

                        Debug.Assert(line != null);
                        XbdmEventArgs e = XbdmEventUtils.ParseSpecial(line) ?? new XbdmEventArgs(line);
                        this.InvokeHandlersAndWait(e);
                    }
                }
            }
            catch (OperationCanceledException) {
                // ignored
            }
            catch (Exception e) {
                AppLogger.Instance.WriteLine("Exception in " + Thread.CurrentThread.Name);
                AppLogger.Instance.WriteLine(e.GetToString());
            }
            finally {
                this.ctsStop = null;
                cts.Dispose();

                AppLogger.Instance.WriteLine("Stopping Event Listener Thread");

                try {
                    tcpClient?.Dispose();
                }
                catch (Exception e) {
                    AppLogger.Instance.WriteLine("Exception closing TCP socket in " + Thread.CurrentThread.Name);
                    AppLogger.Instance.WriteLine(e.GetToString());
                }
            }
        }

        private void InvokeHandlersAndWait(params ReadOnlySpan<XbdmEventArgs> preRunEvents) {
            List<EventHandler<ConsoleSystemEventArgs>> eventHandlerList;
            lock (this.connection.systemEventHandlers) {
                eventHandlerList = this.connection.systemEventHandlers.ToList();
            }

            foreach (XbdmEventArgs tmpEvent in preRunEvents) {
                foreach (EventHandler<ConsoleSystemEventArgs> handler in eventHandlerList) {
                    handler(this.connection, tmpEvent);
                }
            }
        }
    }
}