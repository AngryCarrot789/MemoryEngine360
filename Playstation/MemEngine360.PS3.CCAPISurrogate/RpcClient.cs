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

using System.Buffers;
using System.Net.Cache;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MemEngine360.PS3.CCAPISurrogate;

public sealed class RpcClient {
    private static readonly ParameterizedThreadStart s_RunNewRpcClient = RunNew;
    private static int s_ThreadCount;

    private readonly TcpClient client;
    private readonly NetworkStream stream;

    private ApiHelper Api { get; }

    public RpcClient(TcpClient client, ApiHelper api) {
        this.client = client;
        this.stream = this.client.GetStream();
        this.Api = api;
    }

    public void Run() {
        byte[] packetBuffer = new byte[0x8000];
        bool exit = false;

        try {
            while (!exit) {
                uint cmdId = this.ReadFromNetwork<uint>(false);
                uint packetByteCount = this.ReadFromNetwork<uint>(false);
                if (packetByteCount > packetBuffer.Length)
                    throw new Exception("Invalid packet received, length exceeds 32K: " + packetByteCount);
                if (packetByteCount > 0)
                    this.ReadFullyFromNetwork(packetBuffer, 0, (int) packetByteCount, true);

                Console.WriteLine($"Received packet {cmdId} with data length of {packetByteCount}");
                try {
                    this.ProcessPacket(cmdId, packetBuffer, 0, (int) packetByteCount, ref exit);
                }
                catch (Exception e) {
                    this.Api.Dispose();
                    throw new Exception($"Error while processing packet {cmdId}: {e.Message}");
                }
            }
        }
        finally {
            this.client.Dispose();
            this.stream.Dispose();
        }
    }

    /// <summary>
    /// Process an incoming command
    /// </summary>
    /// <param name="cmdId">Command ID</param>
    /// <param name="buffer">Buffer with a size of 32K</param>
    /// <param name="count">The packet header's byte count</param>
    /// <param name="reader"></param>
    /// <exception cref="Exception"></exception>
    private void ProcessPacket(uint cmdId, byte[] buffer, int offset, int count, ref bool exit) {
        switch (cmdId) {
            case 0: throw new Exception("Invalid packet id: 0");
            case 1: { // CONNECT
                bool success = this.Api.Connect(ReadTaggedString(buffer, ref offset, ref count));
                this.WriteToNetwork((byte) (success ? 1 : 0));
                break;
            }
            case 2: { // DISCONNECT
                bool success = this.Api.Disconnect();
                this.WriteToNetwork((byte) (success ? 1 : 0));
                exit = true;
                break;
            }
            case 3: { // UnmanagedCCAPI.CCAPIGetConnectionStatus_t CCAPIGetConnectionStatus;
                bool success = this.Api.GetConnectionStatus(out bool connected);
                this.WriteToNetwork((byte) (success && connected ? 1 : 0));
                break;
            }
            case 4: { // UnmanagedCCAPI.CCAPISetBootConsoleIds_t CCAPISetBootConsoleIds;
                break;
            }
            case 5: { // UnmanagedCCAPI.CCAPISetConsoleIds_t CCAPISetConsoleIds;
                break;
            }
            case 6: { // WRITE MEMORY
                uint pid = ReadValue<uint>(buffer, ref offset, ref count);
                ulong address = ReadValue<ulong>(buffer, ref offset, ref count);

                // TODO: fine-tune chunk size
                int WRITE_CHUNK_SIZE = 0x4000; // 16K

                // Pin the entire buffer so that we don't have to allocate a smaller one and copy each time.
                // CCAPI (probably...?) does not use the C# GC so this should be completely fine
                using ApiHelper.GCHandleAlloc dataAlloc = new ApiHelper.GCHandleAlloc(buffer);

                for (uint remaining = (uint) count; remaining > 0;) {
                    uint writeCount = Math.Min((uint) WRITE_CHUNK_SIZE, remaining);
                    IntPtr bufferOffset = (IntPtr) dataAlloc + offset + (int) ((uint) count - remaining);
                    this.Api.WriteMemory(pid, address, writeCount, bufferOffset);

                    remaining -= writeCount;
                    address += writeCount;
                }
                
                this.WriteToNetwork((byte) 1);

                break;
            }
            case 7: {  // READ MEMORY
                uint pid = ReadValue<uint>(buffer, ref offset, ref count);
                ulong address = ReadValue<ulong>(buffer, ref offset, ref count);
                uint length = ReadValue<uint>(buffer, ref offset, ref count);
                if (length < 1) {
                    this.WriteToNetwork((ushort) 0);
                    break;
                }

                // TODO: fine-tune chunk size
                int READ_CHUNK_SIZE = 0x4000; // 16K
                const int CODE_ERR = 0x8000; // sign-bit is used to notify of errors

                byte[] dstBuffer = ArrayPool<byte>.Shared.Rent(READ_CHUNK_SIZE);
                using ApiHelper.GCHandleAlloc dataAlloc = new ApiHelper.GCHandleAlloc(dstBuffer);

                try {
                    for (uint remaining = length; remaining > 0;) {
                        uint readCount = Math.Min((uint) READ_CHUNK_SIZE, remaining);
                        if (!this.Api.ReadMemory(pid, address, readCount, dataAlloc)) {
                            this.WriteToNetwork<ushort>(CODE_ERR);
                            return;
                        }

                        this.WriteToNetwork((ushort) readCount);
                        this.WriteToNetwork(dstBuffer.AsSpan(0, (int) readCount));
                        remaining -= readCount;
                        address += readCount;
                    }
                }
                finally {
                    ArrayPool<byte>.Shared.Return(dstBuffer);
                }

                break;
            }
            case 8: { // GET PROCESS LIST
                if (!this.Api.GetProcessList(out List<ApiHelper.ProcessInfo> list)) {
                    this.WriteToNetwork((ushort) 0);
                    return;
                }

                int length = Math.Min(list.Count, ushort.MaxValue);
                this.WriteToNetwork((ushort) length);
                for (int i = 0; i < length; i++) {
                    this.WriteToNetwork(list[i].pid);
                    this.WriteToNetworkTagged(list[i].name);
                }

                break;
            }
            case 9: { // GET PROCESS NAME
                uint pid = ReadValue<uint>(buffer, ref offset, ref count);
                this.Api.GetProcessName(pid, out string? name);
                this.WriteToNetworkTagged(name);

                break;
            }
            case 10: { // UnmanagedCCAPI.CCAPIGetTemperature_t CCAPIGetTemperature;
                break;
            }
            case 11: { // UnmanagedCCAPI.CCAPIShutdown_t CCAPIShutdown;
                break;
            }
            case 12: { // UnmanagedCCAPI.CCAPIRingBuzzer_t CCAPIRingBuzzer;
                break;
            }
            case 13: { // UnmanagedCCAPI.CCAPISetConsoleLed_t CCAPISetConsoleLed;
                break;
            }
            case 14: { // UnmanagedCCAPI.CCAPIGetFirmwareInfo_t CCAPIGetFirmwareInfo;
                break;
            }
            case 15: { // UnmanagedCCAPI.CCAPIVshNotify_t CCAPIVshNotify;
                break;
            }
            case 16: { // UnmanagedCCAPI.CCAPIGetNumberOfConsoles_t CCAPIGetNumberOfConsoles;
                break;
            }
            case 17: { // UnmanagedCCAPI.CCAPIGetConsoleInfo_t CCAPIGetConsoleInfo;
                break;
            }
            case 18: { // UnmanagedCCAPI.CCAPIGetDllVersion_t CCAPIGetDllVersion;
                break;
            }
            case 19: { // SELF TEST
                string text = ReadTaggedString(buffer, ref offset, ref count);
                this.WriteToNetwork((byte) 3);
                this.WriteToNetworkTagged(text);
                this.WriteToNetworkTagged("This is param 1!!!");
                this.WriteToNetwork(1234567);
                break;
            }
        }
    }
    
    private static string ReadTaggedString(byte[] buffer, ref int offset, ref int count) {
        return Encoding.ASCII.GetString(ReadTaggedArray(buffer, ref offset, ref count));
    }

    private static ReadOnlySpan<byte> ReadTaggedArray(byte[] buffer, ref int offset, ref int count) {
        if (count < 4)
            throw new IOException("Not enough bytes for buffer length prefix");

        int length = MemoryMarshal.Read<int>(buffer.AsSpan(offset, sizeof(int)));
        if (length < 0)
            throw new IOException("Invalid length prefix: " + length);
        if (length > (count - sizeof(int)))
            throw new IOException("Not enough bytes to read buffer of length " + length);

        Span<byte> span = buffer.AsSpan(offset + sizeof(int), length);
        offset += length + sizeof(int);
        count -= length + sizeof(int);
        return span;
    }

    private static ReadOnlySpan<byte> ReadArray(byte[] buffer, int length, ref int offset, ref int count) {
        if (length < 0)
            throw new IOException("Invalid length prefix: " + length);
        if (length > count)
            throw new IOException("Not enough bytes to read buffer of length " + length);

        Span<byte> span = buffer.AsSpan(offset, length);
        offset += length;
        count -= length;
        return span;
    }

    private static T ReadValue<T>(byte[] buffer, ref int offset, ref int count) where T : unmanaged {
        int size = Unsafe.SizeOf<T>();
        if (size > count)
            throw new IOException("Not enough bytes to read value of size " + size);

        ReadOnlySpan<byte> span = ReadArray(buffer, size, ref offset, ref count);
        return Unsafe.As<byte, T>(ref MemoryMarshal.GetReference(span));
    }

    private void WriteToNetwork(ReadOnlySpan<byte> buffer) {
        this.stream.Write(buffer);
    }

    private void WriteToNetwork<T>(T value) where T : unmanaged {
        Span<byte> span = MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref value), Unsafe.SizeOf<T>());
        this.WriteToNetwork(span);
    }

    private void WriteToNetworkTagged(ReadOnlySpan<byte> buffer) {
        int length = buffer.Length;
        this.stream.Write(MemoryMarshal.CreateSpan(ref Unsafe.As<int, byte>(ref length), sizeof(int)));
        this.stream.Write(buffer);
    }

    private void WriteToNetworkTagged(string? text) {
        this.WriteToNetworkTagged(!string.IsNullOrEmpty(text) ? Encoding.UTF8.GetBytes(text) : ReadOnlySpan<byte>.Empty);
    }

    private T ReadFromNetwork<T>(bool canTimeout) where T : unmanaged {
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<T>()];
        this.ReadFullyFromNetwork(buffer, canTimeout);
        return Unsafe.As<byte, T>(ref MemoryMarshal.GetReference(buffer));
    }

    private void ReadFullyFromNetwork(byte[] buffer, int offset, int count, bool canTimeout) {
        this.ReadFullyFromNetwork(buffer.AsSpan(offset, count), canTimeout);
    }

    private void ReadFullyFromNetwork(Span<byte> buffer, bool canTimeout) {
        this.client.ReceiveTimeout = canTimeout ? 10000 : Timeout.Infinite;
        int total = 0;
        while (total < buffer.Length) {
            int read = this.stream.Read(buffer.Slice(total));
            if (read < 1) {
                throw new TimeoutException();
            }

            total += read;
        }
    }

    public static void RunInThread(TcpClient client) {
        Thread thread = new Thread(s_RunNewRpcClient) {
            Name = $"RPC Client #{Interlocked.Increment(ref s_ThreadCount)}",
            IsBackground = true
        };

        thread.Start(client);
    }

    private static void RunNew(object? obj) {
        TcpClient tcp = (TcpClient) obj!;
        UnmanagedCCAPI lib;

        try {
            lib = UnmanagedCCAPI.LoadLibrary("CCAPI.dll");
        }
        catch (Exception e) {
            Console.Error.WriteLine("Failed to load CCAPI.dll");
            tcp.Dispose();
            return;
        }

        new RpcClient(tcp, new ApiHelper(lib)).Run();
    }
}