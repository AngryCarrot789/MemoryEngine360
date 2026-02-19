// 
// Copyright (c) 2026-2026 REghZy
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

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using PFXToolKitUI.Utils;

namespace MemEngine360.PS3.MAPI;

public partial class Ps3ManagerApiV2 {
    public const int PS3M_API_PC_LIB_VERSION = 288;

    private Socket? mainSocket;
    private IPAddress mainSocketIpAddress;
    private IPEndPoint? mainSocketEndPoint;
    private Socket? dataSocket;
    private IPEndPoint? dataSocketEndPoint;


    private readonly byte[] bucketReadBuffer = new byte[512];
    private string socketTextBucket = "";

    public bool IsConnected => this.mainSocket != null;

    public EndPoint? EndPoint => this.mainSocketEndPoint;

    public Ps3ManagerApiV2() {
    }

    public void SetMainSocket(Socket socket, IPEndPoint endPoint) {
        this.mainSocket = socket;
        this.mainSocket.ReceiveTimeout = 5000;
        this.mainSocket.Blocking = false;
        this.mainSocketIpAddress = endPoint.Address;
        this.mainSocketEndPoint = endPoint;
    }

    public async ValueTask<uint> GetServerVersion() {
        this.ValidateConnected();

        MapiResponse response = await this.SendCommandAndGetResponse("SERVER GETVERSION");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful: {
                if (uint.TryParse(response.Message, out uint value))
                    return value;

                throw new IOException("Invalid response from PS3");
            }
            default: throw new IOException("PS3ManagerAPI error");
        }
    }

    public async ValueTask<uint> GetServerMinimumVersion() {
        this.ValidateConnected();

        MapiResponse response = await this.SendCommandAndGetResponse("SERVER GETMINVERSION");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful: {
                if (uint.TryParse(response.Message, out uint value))
                    return value;

                throw new IOException("Invalid response from PS3");
            }
            default: throw new IOException("PS3ManagerAPI error");
        }
    }

    public async ValueTask<uint> GetCoreVersion() {
        this.ValidateConnected();

        MapiResponse response = await this.SendCommandAndGetResponse("CORE GETVERSION");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful: {
                if (uint.TryParse(response.Message, out uint value))
                    return value;

                throw new IOException("Invalid response from PS3");
            }
            default: throw new IOException("PS3ManagerAPI error");
        }
    }

    public async ValueTask<uint> GetCoreMinimumVersion() {
        this.ValidateConnected();

        MapiResponse response = await this.SendCommandAndGetResponse("CORE GETMINVERSION");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful: {
                if (uint.TryParse(response.Message, out uint value))
                    return value;

                throw new IOException("Invalid response from PS3");
            }
            default: throw new IOException("PS3ManagerAPI error");
        }
    }

    public async ValueTask<uint> GetPs3FwVersion() {
        this.ValidateConnected();

        MapiResponse response = await this.SendCommandAndGetResponse("PS3 GETFWVERSION");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful: {
                if (uint.TryParse(response.Message, out uint value))
                    return value;

                throw new IOException("Invalid response from PS3");
            }
            default: throw new IOException("PS3ManagerAPI error");
        }
    }

    public async ValueTask ReadMemory(uint pId, ulong address, byte[] buffer, int offset, int count) {
        if (!this.IsConnected)
            throw new IOException("PS3MAPI not connected!");

        await this.SetBinaryMode(true);

        this.CloseDataSocket();
        await this.OpenDataSocket();

        MapiResponse memGetResponse = await this.SendCommandAndGetResponse($"MEMORY GET {pId} {address:X16} {count}").ConfigureAwait(false);
        if (memGetResponse.Code == ResponseCode.DataConnectionAlreadyOpen || memGetResponse.Code != ResponseCode.MemoryStatusOK) {
            throw new IOException(memGetResponse.Message);
        }

        using CancellationTokenSource cts = new CancellationTokenSource(5000);

        for (int remaining = count; remaining > 0;) {
            try {
                Memory<byte> memory = buffer.AsMemory(offset + (remaining - count), remaining);
                int recv = await this.dataSocket!.ReceiveAsync(memory, SocketFlags.None, cts.Token).ConfigureAwait(false);
                if (recv > 0) {
                    remaining -= recv;
                    cts.CancelAfter(5000);
                }
                else {
                    await Task.Delay(1, cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception e) when (e is TimeoutException || e is OperationCanceledException) {
                this.Disconnect();
                throw new TimeoutException("Timeout reading data from PS3");
            }
            catch (Exception ex) {
                this.Disconnect();
                throw new IOException("Error receiving bytes", ex);
            }
        }

        this.CloseDataSocket();
        MapiResponse closeResponse = await this.ReadResponse();
        switch (closeResponse.Code) {
            case ResponseCode.RequestSuccessful:
            case ResponseCode.MemoryActionCompleted:
                await this.SetBinaryMode(false);
                break;
            default: throw new IOException(closeResponse.Message);
        }
    }

    public async ValueTask WriteMemory(uint pId, ulong address, byte[] buffer, int offset, int count) {
        await this.SetBinaryMode(true);

        this.CloseDataSocket();
        await this.OpenDataSocket();

        MapiResponse memSetResponse = await this.SendCommandAndGetResponse($"MEMORY SET {pId} {address:X16}");
        if (memSetResponse.Code == ResponseCode.DataConnectionAlreadyOpen || memSetResponse.Code != ResponseCode.MemoryStatusOK) {
            throw new IOException(memSetResponse.Message);
        }

        using CancellationTokenSource cts = new CancellationTokenSource(5000);

        for (int remaining = count; remaining > 0;) {
            try {
                Memory<byte> memory = buffer.AsMemory(offset + (remaining - count), remaining);
                int sent = await this.dataSocket!.SendAsync(memory, SocketFlags.None, cts.Token).ConfigureAwait(false);
                if (sent > 0) {
                    remaining -= sent;
                    cts.CancelAfter(5000);
                }
                else {
                    await Task.Delay(1, cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception e) when (e is TimeoutException || e is OperationCanceledException) {
                this.Disconnect();
                throw new TimeoutException("Timeout reading data from PS3");
            }
            catch (Exception ex) {
                this.Disconnect();
                throw new IOException("Error receiving bytes", ex);
            }

            this.CloseDataSocket();
            MapiResponse response1 = await this.ReadResponse();
            switch (response1.Code) {
                case ResponseCode.RequestSuccessful:
                case ResponseCode.MemoryActionCompleted:
                    await this.SetBinaryMode(false);
                    continue;
                default: throw new IOException(response1.Message);
            }
        }
    }

    public async ValueTask<uint[]> GetProcessList() {
        MapiResponse response = await this.SendCommandAndGetResponse("PROCESS GETALLPID");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful: {
                int j = 0;
                string[] lines = response.Message.Split('|');
                Span<uint> pids = stackalloc uint[lines.Length];
                foreach (string line in lines) {
                    if (string.IsNullOrWhiteSpace(line) || line == "0") {
                        continue;
                    }
                    
                    if (uint.TryParse(line.AsSpan().Trim(), out uint pid)) {
                        pids[j++] = pid;
                    }
                }

                return pids.Slice(0, j).ToArray();
            }
            default: throw new IOException(response.Message);
        }
    }

    public async ValueTask<string> GetProcessName(uint pid) {
        MapiResponse response = await this.SendCommandAndGetResponse($"PROCESS GETNAME {pid}");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful:
                return response.Message;
            default: throw new IOException(response.Message);
        }
    }

    public async ValueTask SetBinaryMode(bool mode) {
        MapiResponse response = await this.SendCommandAndGetResponse($"TYPE {(mode ? "I" : "A")}");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful:
                break;
            default: throw new IOException("PS3ManagerAPI error");
        }
    }

    public void Disconnect() {
        this.CloseDataSocket();

        if (this.mainSocket != null && this.mainSocket.Connected) {
            // Send DISCONNECT and Dispose asynchronously in case the send buffer is full,
            // which would cause Send() to block, which is a huge no-no
            Socket socket = this.mainSocket!;
            Task.Run(() => {
                try {
                    socket.Send(this.GetCommandBytes("DISCONNECT").Span, SocketFlags.None);
                }
                catch {
                    // ignored
                }
                finally {
                    socket.Dispose();
                }
            });
        }

        this.mainSocket = null;
        this.mainSocketEndPoint = null;
    }

    public async ValueTask<MapiResponse> SendCommandAndGetResponse(string command) {
        this.ValidateConnected();
        Debug.Assert(this.mainSocket != null);

        await this.mainSocket.SendAsync(this.GetCommandBytes(command), SocketFlags.None);
        return await this.ReadResponse();
    }

    public async ValueTask<MapiResponse> ReadResponse() {
        while (true) {
            string line = await this.ReadNextLineFromSocket();
            if (GetResponseRegex1().IsMatch(line)) {
                return ProcessResponse(line);
            }
            else {
                this.socketTextBucket += $"{GetResponseRegex2().Replace(line, "")}\n";
            }
        }

        static MapiResponse ProcessResponse(string responseInput) {
            if (!int.TryParse(responseInput.AsSpan(0, 3), out int code)) {
                throw new IOException($"Invalid response from PS3ManagerAPI: {responseInput}");
            }

            int i = 4, j = 0;
            char[] chars = new char[responseInput.Length - 4];
            while (i < responseInput.Length) {
                char ch = responseInput[i++];
                if (ch != '\r' && ch != '\n') {
                    chars[j++] = ch;
                }
            }

            string responseText = chars.AsSpan().Trim().ToString();
            return new MapiResponse(responseText, (ResponseCode) code);
        }
    }

    private async ValueTask<string> ReadNextLineFromSocket() {
        this.ValidateConnected();
        Debug.Assert(this.mainSocket != null);

        int length;
        for (length = this.socketTextBucket.IndexOf('\n'); length < 0; length = this.socketTextBucket.IndexOf('\n')) {
            if (await ReceiveTextFromSocket() < 1) {
                await this.WaitForSocketData();
            }
        }

        string lineFromBucket = this.socketTextBucket.Substring(0, length);
        this.socketTextBucket = this.socketTextBucket.Substring(length + 1);
        return lineFromBucket;

        async ValueTask<int> ReceiveTextFromSocket() {
            int total = 0;
            while (this.mainSocket.Available > 0) {
                int count = await this.mainSocket.ReceiveAsync(this.bucketReadBuffer, SocketFlags.None).ConfigureAwait(false);
                this.socketTextBucket += Encoding.ASCII.GetString(this.bucketReadBuffer, 0, count);
                total += count;
            }

            return total;
        }
    }

    private async ValueTask WaitForSocketData() {
        this.ValidateConnected();
        Debug.Assert(this.mainSocket != null);

        if (this.mainSocket.Available > 0) {
            return;
        }

        await Task.Yield();

        long timeStart = Time.GetSystemTicks();
        long timeout = TimeSpan.FromSeconds(5.0).Ticks;

        while (this.mainSocket!.Available < 1) {
            await Task.Delay(50).ConfigureAwait(false);
            if ((Time.GetSystemTicks() - timeStart) > timeout) {
                throw new TimeoutException("Timeout waiting for data from console");
            }
        }
    }

    private void ValidateConnected() {
        if (this.mainSocket == null) {
            throw new InvalidOperationException("Not connected");
        }
    }

    private ReadOnlyMemory<byte> GetCommandBytes(string command) {
        if (!command.EndsWith("\r\n")) {
            command += "\r\n";
        }

        // TODO: optimize
        byte[] bytes = Encoding.ASCII.GetBytes(command);
        return bytes;
    }

    private async ValueTask OpenDataSocket() {
        if (this.dataSocket != null) {
            throw new InvalidOperationException("Data socket already open");
        }

        MapiResponse response = await this.SendCommandAndGetResponse("PASV");
        if (response.Code != ResponseCode.EnteringPassiveMode) {
            throw new IOException("PS3ManagerAPI error. Could not enter passive mode");
        }

        int idxA = response.Message.IndexOf('('), idxB = idxA == -1 ? -1 : response.Message.IndexOf(')', idxA);
        if (idxA == -1 || idxB == -1) {
            this.Disconnect();
            throw new IOException("Malformed PASV response: " + response.Message);
        }

        string[] strArray = response.Message.Substring(idxA + 1, idxB - idxA - 1).Split(',');
        if (strArray.Length < 6) {
            this.Disconnect();
            throw new IOException("Malformed PASV response: " + response.Message);
        }

        if (!int.TryParse(strArray[4], out int a) || !int.TryParse(strArray[5], out int b)) {
            throw new IOException("Malformed PASV response: " + response.Message);
        }

        int port = (a << 8) + b;
        using CancellationTokenSource cts = new CancellationTokenSource(5000);

        try {
            this.CloseDataSocket();
            this.dataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.dataSocketEndPoint = new IPEndPoint(this.mainSocketIpAddress, port);
            await this.dataSocket.ConnectAsync(this.dataSocketEndPoint, cts.Token);
        }
        catch (OperationCanceledException) {
            throw new TimeoutException("Timeout trying to open data socket");
        }
        catch (Exception ex) {
            throw new IOException("Error trying to open data socket", ex);
        }
    }

    private void CloseDataSocket() {
        if (this.dataSocket != null) {
            if (this.dataSocket.Connected)
                this.dataSocket.Close();
            this.dataSocket = null;
        }

        this.dataSocketEndPoint = null;
    }

    [GeneratedRegex("^[0-9]+ ")]
    private static partial Regex GetResponseRegex1();

    [GeneratedRegex("^[0-9]+-")]
    private static partial Regex GetResponseRegex2();
}

public readonly struct MapiResponse(string message, ResponseCode code) {
    public string Message { get; } = message;

    public ResponseCode Code { get; } = code;
}

public enum ResponseCode {
    DataConnectionAlreadyOpen = 125, // 0x0000007D
    MemoryStatusOK = 150, // 0x00000096
    CommandOK = 200, // 0x000000C8
    PS3MAPIConnected = 220, // 0x000000DC
    RequestSuccessful = 226, // 0x000000E2
    EnteringPassiveMode = 227, // 0x000000E3
    PS3MAPIConnectedOK = 230, // 0x000000E6
    MemoryActionCompleted = 250, // 0x000000FA
    MemoryActionPended = 350, // 0x0000015E
}

public enum PowerFlags {
    ShutDown,
    QuickReboot,
    SoftReboot,
    HardReboot,
}

public enum BuzzerMode {
    Single,
    Double,
    Triple,
}

public enum LedColor {
    Red,
    Green,
    Yellow,
}

public enum LedMode {
    Off,
    On,
    BlinkFast,
    BlinkSlow,
}

public enum Syscall8Mode {
    Enabled,
    Only_CobraMambaAndPS3MAPI_Enabled,
    Only_PS3MAPI_Enabled,
    FakeDisabled,
    Disabled,
}