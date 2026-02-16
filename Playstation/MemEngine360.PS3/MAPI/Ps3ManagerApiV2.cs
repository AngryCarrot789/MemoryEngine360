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

    private Socket? main_sock;
    private IPAddress ip_address;
    private IPEndPoint? main_ipEndPoint;
    private Socket? data_sock;
    private IPEndPoint? data_ipEndPoint;


    private readonly byte[] bucketReadBuffer = new byte[512];
    private string textBucket = "";

    public bool IsConnected => this.main_sock != null;

    public EndPoint? EndPoint => this.main_ipEndPoint;

    public Ps3ManagerApiV2() {
    }

    public void SetMainSocket(Socket socket, IPEndPoint endPoint) {
        this.main_sock = socket;
        this.main_sock.ReceiveTimeout = 5000;
        this.ip_address = endPoint.Address;
        this.main_ipEndPoint = endPoint;
    }

    public async ValueTask OpenDataSocket() {
        if (this.data_sock != null) {
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
            this.data_sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.data_ipEndPoint = new IPEndPoint(this.ip_address, port);
            await this.data_sock.ConnectAsync(this.data_ipEndPoint, cts.Token);
        }
        catch (OperationCanceledException) {
            throw new TimeoutException("Timeout trying to open data socket");
        }
        catch (Exception ex) {
            throw new IOException("Error trying to open data socket", ex);
        }
    }

    public void CloseDataSocket() {
        if (this.data_sock != null) {
            if (this.data_sock.Connected)
                this.data_sock.Close();
            this.data_sock = null;
        }

        this.data_ipEndPoint = null;
    }

    public async ValueTask<uint> Server_Get_Version() {
        this.ValidateConnected();

        MapiResponse response = await this.SendCommandAndGetResponse("SERVER GETVERSION");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful:
                return Convert.ToUInt32(response.Message);
            default: throw new IOException("PS3ManagerAPI error");
        }
    }

    public async ValueTask<uint> Server_GetMinVersion() {
        this.ValidateConnected();

        MapiResponse response = await this.SendCommandAndGetResponse("SERVER GETMINVERSION");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful:
                return Convert.ToUInt32(response.Message);
            default: throw new IOException("PS3ManagerAPI error");
        }
    }

    public async ValueTask<uint> Core_Get_Version() {
        this.ValidateConnected();

        MapiResponse response = await this.SendCommandAndGetResponse("CORE GETVERSION");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful:
                return Convert.ToUInt32(response.Message);
            default: throw new IOException("PS3ManagerAPI error");
        }
    }

    public async ValueTask<uint> Core_GetMinVersion() {
        this.ValidateConnected();

        MapiResponse response = await this.SendCommandAndGetResponse("CORE GETMINVERSION");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful:
                return Convert.ToUInt32(response.Message);
            default: throw new IOException("PS3ManagerAPI error");
        }
    }

    public async ValueTask<uint> PS3_GetFwVersion() {
        this.ValidateConnected();

        MapiResponse response = await this.SendCommandAndGetResponse("PS3 GETFWVERSION");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful:
                return Convert.ToUInt32(response.Message);
            default: throw new IOException("PS3ManagerAPI error");
        }
    }

    public void SendCommandNonBlocking(string command) {
        this.ValidateConnected();
        Debug.Assert(this.main_sock != null);

        this.main_sock.Send(this.GetCommandBytes(command).Span, SocketFlags.None);
    }

    public async ValueTask<MapiResponse> SendCommandAndGetResponse(string command) {
        this.ValidateConnected();
        Debug.Assert(this.main_sock != null);

        await this.main_sock.SendAsync(this.GetCommandBytes(command), SocketFlags.None);
        return await this.ReadResponse();
    }

    public async ValueTask<MapiResponse> ReadResponse() {
        string lineFromBucket;
        while (true) {
            lineFromBucket = await this.GetLineFromBucket();
            if (!GetResponseRegex1().IsMatch(lineFromBucket)) {
                this.textBucket += $"{GetResponseRegex2().Replace(lineFromBucket, "")}\n";
            }
            else
                break;
        }

        string response = lineFromBucket.Substring(4).Replace("\r", "").Replace("\n", "");
        ResponseCode responseCode = (ResponseCode) int.Parse(lineFromBucket.Substring(0, 3));
        return new MapiResponse(response, responseCode);
    }

    public async ValueTask Memory_Get(uint pId, ulong address, byte[] buffer, int offset, int count) {
        if (!this.IsConnected)
            throw new IOException("PS3MAPI not connected!");

        await this.SetBinaryMode(true);

        this.CloseDataSocket();
        await this.OpenDataSocket();

        MapiResponse memGetResponse = await this.SendCommandAndGetResponse($"MEMORY GET {pId} {address:X16} {count}").ConfigureAwait(false);
        if (memGetResponse.Code == ResponseCode.DataConnectionAlreadyOpen || memGetResponse.Code != ResponseCode.MemoryStatusOK) {
            throw new IOException(memGetResponse.Message);
        }

        for (int remaining = count; remaining > 0;) {
            try {
                Memory<byte> memory = buffer.AsMemory(offset + (remaining - count), remaining);
                int recv = await this.data_sock!.ReceiveAsync(memory, SocketFlags.None);
                if (recv < 0) {
                    await Task.Delay(1).ConfigureAwait(false);
                }

                remaining -= recv;
            }
            catch (TimeoutException) {
                this.CloseDataSocket();
                throw;
            }
            catch (Exception ex) {
                this.CloseDataSocket();
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

    public async ValueTask Memory_Set(uint Pid, ulong Address, byte[] Bytes) {
        await this.SetBinaryMode(true);
        int length = Bytes.Length;
        long num = 0;
        long srcOffset = 0;
        bool flag = false;
        if (this.data_sock == null) {
            await this.OpenDataSocket();
        }

        MapiResponse response = await this.SendCommandAndGetResponse($"MEMORY SET {Pid} {Address:X16}");
        switch (response.Code) {
            case ResponseCode.DataConnectionAlreadyOpen:
            case ResponseCode.MemoryStatusOK:
                while (!flag) {
                    try {
                        byte[] numArray = new byte[length - (int) num];
                        Buffer.BlockCopy(Bytes, (int) srcOffset, numArray, 0, length - (int) num);
                        srcOffset = this.data_sock!.Send(numArray, Bytes.Length - (int) num, SocketFlags.None);
                        flag = false;
                        if (srcOffset > 0L) {
                            num += srcOffset;
                            if ((int) (num * 100L / length) >= 100)
                                flag = true;
                        }
                        else
                            flag = true;

                        if (flag) {
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
                    catch (Exception ex) {
                        this.CloseDataSocket();
                        await this.ReadResponse();
                        await this.SetBinaryMode(false);
                        throw new IOException("Error", ex);
                    }
                }

                break;
            default: throw new IOException(response.Message);
        }
    }

    public async ValueTask<uint[]> GetPidList() {
        MapiResponse response = await this.SendCommandAndGetResponse("PROCESS GETALLPID");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful: {
                int j = 0;
                Span<uint> pids = stackalloc uint[256 /*0x10*/];
                string[] lines = response.Message.Split('|');
                for (int i = 0; i < lines.Length && i < 256; i++) {
                    string pidText = lines[i];
                    if (!string.IsNullOrEmpty(pidText) && pidText != " " && pidText != "0") {
                        pids[j] = Convert.ToUInt32(pidText, 10);
                        ++j;
                    }
                }

                return pids.Slice(0, j).ToArray();
            }
            default: throw new IOException(response.Message);
        }
    }

    public async ValueTask<string> Process_GetName(uint pid) {
        MapiResponse response = await this.SendCommandAndGetResponse("PROCESS GETNAME " + $"{pid}");
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful:
                return response.Message;
            default: throw new IOException(response.Message);
        }
    }

    public async ValueTask SetBinaryMode(bool bMode) {
        MapiResponse response = await this.SendCommandAndGetResponse("TYPE" + (bMode ? " I" : " A"));
        switch (response.Code) {
            case ResponseCode.CommandOK:
            case ResponseCode.RequestSuccessful:
                break;
            default: throw new IOException("PS3ManagerAPI error");
        }
    }

    private async ValueTask<string> GetLineFromBucket() {
        int length;
        for (length = this.textBucket.IndexOf('\n'); length < 0; length = this.textBucket.IndexOf('\n')) {
            if (await this.FillBucket() < 1) {
                await this.WaitForSocketData();
            }
        }

        string lineFromBucket = this.textBucket.Substring(0, length);
        this.textBucket = this.textBucket.Substring(length + 1);
        return lineFromBucket;
    }

    private async ValueTask<int> FillBucket() {
        this.ValidateConnected();
        Debug.Assert(this.main_sock != null);

        int total = 0;
        while (this.main_sock.Available > 0) {
            int count = await this.main_sock.ReceiveAsync(this.bucketReadBuffer, SocketFlags.None).ConfigureAwait(false);
            this.textBucket += Encoding.ASCII.GetString(this.bucketReadBuffer, 0, count);
            total += count;
        }

        return total;
    }

    private async ValueTask WaitForSocketData() {
        this.ValidateConnected();
        Debug.Assert(this.main_sock != null);

        if (this.main_sock.Available > 0) {
            return;
        }

        await Task.Yield();

        long timeStart = Time.GetSystemTicks();
        long timeout = TimeSpan.FromSeconds(5.0).Ticks;

        while (this.main_sock!.Available < 1) {
            await Task.Delay(50).ConfigureAwait(false);
            if ((Time.GetSystemTicks() - timeStart) > timeout) {
                throw new TimeoutException("Timeout waiting for data from console");
            }
        }
    }

    private void ValidateConnected() {
        if (this.main_sock == null) {
            throw new InvalidOperationException("Not connected");
        }
    }

    public void Disconnect() {
        if (this.main_sock != null) {
            if (this.main_sock.Connected) {
                this.SendCommandNonBlocking("DISCONNECT");
            }

            this.main_sock.Dispose();
        }

        this.main_sock = null;
        this.main_ipEndPoint = null;
    }

    private ReadOnlyMemory<byte> GetCommandBytes(string command) {
        if (!command.EndsWith("\r\n")) {
            command += "\r\n";
        }

        // TODO: optimize
        byte[] bytes = Encoding.ASCII.GetBytes(command);
        return bytes;
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