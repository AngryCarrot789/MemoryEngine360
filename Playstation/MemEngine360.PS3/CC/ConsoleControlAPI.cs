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

using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using System.Text;
using PFXToolKitUI.Utils;

namespace MemEngine360.PS3.CC;

[SupportedOSPlatform("windows")]
public class ConsoleControlAPI : IDisposable {
    /*
     * CCAPI is native library compiled in x86, however, MemoryEngine360 is x64.
     *
     * Because of this, we can't load the library directly into our memory and use P/Invoke,
     * because of the architecture difference.
     *
     * Therefore, we have to use a surrogate process, which is 32 bit, that loads the library
     * itself, and implements an RPC protocol for calling them and receiving results.
     *
     * Protocol (send to process):
     *   [uint16]  CommandID
     *   [uint16]  CB_Packet_Data
     *   [byte...] PacketData
     *
     * Protocol (receive from process)
     *   [uint16]  Number of return values
     *   [byte...] Raw data of return values
     *
     * When sending data to the process, the max number of bytes is ushort.MaxValue (65535).
     * Buffers are prefixed with a ushort length
     *
     * When receiving from the process, the max number of bytes is uint.MaxValue(4.2 billion)
     * Buffers are prefixed with a uint length
     *
     */
    private Process? process;
    private TcpClient? client;
    private NetworkStream? stream;
    private Thread? threadCCAPI;

    private Action<ConsoleControlAPI>? threadAction;
    private readonly ManualResetEvent threadMre;
    private bool keepThreadRunning;

    /// <summary>
    /// Gets the exception that causes this CCAPI wrapper to enter a failed state
    /// </summary>
    public Exception? FailureException { get; private set; }

    /// <summary>
    /// Fired when something goes wrong with our thread that causes this CCAPI wrapper to
    /// become invalid and require disposing. <see cref="FailureException"/> will be non-null at this point.
    /// </summary>
    public event EventHandler? NativeFailure;

    private ConsoleControlAPI(Process process, TcpClient client) {
        this.process = process;
        this.client = client;
        this.stream = client.GetStream();
        this.threadMre = new ManualResetEvent(false);
        this.keepThreadRunning = true;
        this.threadCCAPI = new Thread(this.ThreadMainConsoleControl) {
            Name = "CCAPI Interop", IsBackground = true
        };

        this.threadCCAPI.Start();
    }

    #region Threading

    private void ThreadMainConsoleControl() {
        while (this.keepThreadRunning) {
            this.threadMre.WaitOne();
            Action<ConsoleControlAPI>? action = Interlocked.Exchange(ref this.threadAction, null);
            try {
                action?.Invoke(this);
            }
            catch (Exception e) {
                // Typically IO or Timeout exception
                this.FailureException = e;
                this.NativeFailure?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
    }

    private Task RunThreadActionLater(Action<ConsoleControlAPI> func, CancellationToken cancellation = default) {
        return this.RunThreadActionLater<object?>((t) => {
            func(t);
            return null;
        }, cancellation);
    }

    private Task<T> RunThreadActionLater<T>(Func<ConsoleControlAPI, T> func, CancellationToken cancellation = default) {
        TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
        Action<ConsoleControlAPI> delegateAction = (t) => {
            if (cancellation.IsCancellationRequested) {
                tcs.SetCanceled(cancellation);
                return;
            }

            if (!t.keepThreadRunning) {
                tcs.SetCanceled(CancellationToken.None); // None can be used to tell difference between cancelled and aborted 
                return;
            }

            try {
                this.EnsureNotDisposed();
                tcs.SetResult(func(t));
            }
            catch (Exception e) {
                tcs.SetException(e);
                throw;
            }
        };

        if (Interlocked.CompareExchange(ref this.threadAction, delegateAction, null) != null) {
            throw new InvalidOperationException("Another action already queued");
        }

        this.threadMre.Set();
        return tcs.Task;
    }

    #endregion

    public static async Task<ConsoleControlAPI> Run(int port = 45678, CancellationToken cancellationToken = default) {
        if (port < 0 || port > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 0 and 65535");

        Process process = new Process() {
            StartInfo = new ProcessStartInfo("ccapi-surrogate.exe") {
                ArgumentList = {
                    port.ToString()
                },
                CreateNoWindow = true,// !Debugger.IsAttached, // create window when debugging
                // UseShellExecute = true
            }
        };

        bool? hasStarted = null;
        try {
            await Task.Run(void () => hasStarted = process.Start(), cancellationToken);
        }
        catch (OperationCanceledException) {
            Debug.Assert(!hasStarted.HasValue);
            throw;
        }
        catch (Exception e) {
            throw new Exception("Failed to start ccapi-surrogate.exe process", e);
        }

        if (!hasStarted.HasValue || !hasStarted.Value) {
            throw new Exception("Failed to start ccapi-surrogate.exe process");
        }

        TcpClient client = new TcpClient() {
            ReceiveTimeout = 10000, // 99999999, // - for debugging with attached process
            ReceiveBufferSize = 65535
        };

        try {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(10000);
            await client.ConnectAsync("127.0.0.1", port, cts.Token);
        }
        catch (Exception e) {
            try {
                process.Kill();
            }
            catch (Exception ex) {
                e.AddSuppressed(ex);
            }

            client.Dispose();
            throw;
        }

        ConsoleControlAPI api = new ConsoleControlAPI(process, client);
        try {
            await api.SelfTest(cancellationToken);
            if (!await api.Init(cancellationToken)) {
                throw new Exception("Failed to init console control api. Is the DLL available?");
            }
        }
        catch (Exception e) {
            try {
                process.Kill();
            }
            catch (Exception ex) {
                e.AddSuppressed(ex);
            }

            api.keepThreadRunning = false;
            client.Dispose();
            throw;
        }

        if (api.FailureException != null)
            ExceptionDispatchInfo.Throw(api.FailureException);

        return api;
    }

    private Task<bool> Init(CancellationToken cancellationToken) {
        return this.RunThreadActionLater(_ => {
            this.WritePacket(1, Span<byte>.Empty);
            int argc = this.stream!.ReadByte();
            if (argc != 1) {
                throw new Exception("Invalid response to command1. Expected 1 arg, got " + argc);
            }

            return this.stream!.ReadByte() == 1;
        }, cancellationToken);
    }

    private Task SelfTest(CancellationToken cancellationToken = default) {
        return this.RunThreadActionLater(_ => {
            const string TestText = "hello!!!";
            using (MemoryStream dataStream = new MemoryStream()) {
                using BinaryWriter writer = new BinaryWriter(dataStream);
                writer.Write((ushort) TestText.Length);
                foreach (char ch in TestText) {
                    writer.Write((byte) ch);
                }

                this.WritePacket(3, dataStream.ToArray());
            }

            using (BinaryReader reader = new BinaryReader(this.stream!, Encoding.UTF8, leaveOpen: true)) {
                int argc = reader.ReadByte();
                if (argc != 3 /* vars count */) {
                    throw new Exception("Self-test failed. Expected 3 args, got " + argc);
                }

                string text = ReadStringWithTag(reader);
                if (text != TestText) {
                    throw new Exception($"Self-test failed. Expected string to be '{TestText}', got '{text}' instead");
                }

                string string2 = ReadStringWithTag(reader);
                if (string2 != "This is param 1!!!") {
                    throw new Exception($"Self-test failed. Expected next string to be 'This is param 1!!!', got '{text}' instead");
                }

                int nextInt = reader.ReadInt32();
                if (nextInt != 1234567) {
                    throw new Exception($"Self-test failed. Expected next int to be '1234567', got '{nextInt}' instead");
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Try connect to a PS3 with the specific IP
    /// </summary>
    public Task<bool> ConnectToConsole(string ipAddress) {
        return this.RunThreadActionLater(_ => {
            using (MemoryStream memory = new MemoryStream()) {
                using BinaryWriter writer = new BinaryWriter(memory);
                writer.Write((ushort) ipAddress.Length);
                foreach (char ch in ipAddress) {
                    writer.Write((byte) ch);
                }

                this.WritePacket(4, memory.ToArray());
            }

            int argc = this.stream!.ReadByte();
            if (argc != 1) {
                throw new Exception("Invalid response to command4. Expected 1 arg, got " + argc);
            }

            Span<byte> buffer = stackalloc byte[4];
            this.ReadFully(buffer);

            int ccapiResult = Unsafe.As<byte, int>(ref buffer[0]);
            return ccapiResult == 0;
        });
    }

    /// <summary>
    /// Try disconnect from the currently connected PS3
    /// </summary>
    public bool DisconnectFromConsole(bool doNotReadResult) {
        this.WritePacket(5, Span<byte>.Empty);

        if (!doNotReadResult) {
            int argc = this.stream!.ReadByte();
            if (argc != 1) {
                throw new Exception("Invalid response to command5. Expected 1 arg, got " + argc);
            }

            Span<byte> buffer = stackalloc byte[4];
            this.ReadFully(buffer);

            int ccapiResult = Unsafe.As<byte, int>(ref buffer[0]);
            return ccapiResult == 0;
        }

        return true;
    }

    /// <summary>
    /// Sets the attached PID for reading and writing memory
    /// </summary>
    /// <param name="processId">The new PID</param>
    /// <returns>The previous PID</returns>
    public Task<uint> AttachToProcess(uint processId) {
        return this.RunThreadActionLater(_ => {
            Span<byte> buffer4 = stackalloc byte[4];
            Unsafe.As<byte, uint>(ref buffer4[0]) = processId;
            this.WritePacket(22, buffer4);

            int argc = this.stream!.ReadByte();
            if (argc != 1) {
                throw new Exception("Invalid response to command22. Expected 1 arg, got " + argc);
            }

            this.ReadFully(buffer4);
            return Unsafe.As<byte, uint>(ref buffer4[0]);
        });
    }

    /// <summary>
    /// Finds the active game PID
    /// </summary>
    /// <returns>The PID, or zero, if no game is running</returns>
    public Task<(uint, string?)> FindGameProcessId() {
        return this.RunThreadActionLater(_ => {
            this.WritePacket(23, Span<byte>.Empty);

            int argc = this.stream!.ReadByte();
            if (argc != 1 && argc != 2) {
                throw new Exception("Invalid response to command23. Expected 1 or 2 args, got " + argc);
            }

            Span<byte> buffer4 = stackalloc byte[4];
            this.ReadFully(buffer4);
            
            string? processName = null;
            if (argc == 2) {
                Span<byte> lenSpan = stackalloc byte[4];
                this.ReadFully(lenSpan);

                int length = Unsafe.As<byte, int>(ref lenSpan[0]);
                byte[] asciiBuffer = new byte[length];
                this.ReadFully(asciiBuffer, 0, length);
                processName = Encoding.ASCII.GetString(asciiBuffer);
            }

            return (Unsafe.As<byte, uint>(ref buffer4[0]), processName);
        });
    }
    
    public Task<List<(uint, string?)>> GetAllProcesses() {
        return this.RunThreadActionLater(_ => {
            this.WritePacket(24, Span<byte>.Empty);

            int cbRead;
            List<(uint, string?)> list = new List<(uint, string?)>();
            Span<byte> buffer8 = stackalloc byte[8];
            int argc = this.stream!.ReadByte();
            for (int i = 0; i < argc; i++) {
                this.ReadFully(buffer8);
                
                string? processName;
                uint pid = Unsafe.As<byte, uint>(ref buffer8[0]);
                int nameLength = Unsafe.As<byte, int>(ref buffer8[4]);
                if (nameLength > 0) {
                    byte[] asciiBuffer = new byte[nameLength];
                    this.ReadFully(asciiBuffer, 0, nameLength);
                    processName = Encoding.ASCII.GetString(asciiBuffer);
                }
                else {
                    processName = null;
                }

                list.Add((pid, processName));
            }

            return list;
        });
    }

    public Task ReadMemory(uint address, byte[] dstBuffer, int offset, int count) {
        if (count < 0)
            return Task.FromException(new ArgumentOutOfRangeException(nameof(count), "Could cannot be negative"));

        return this.RunThreadActionLater(_ => {
            Span<byte> sendBuffer = stackalloc byte[8];
            Unsafe.As<byte, uint>(ref sendBuffer[0]) = address;
            Unsafe.As<byte, uint>(ref sendBuffer[4]) = (uint) count;
            this.WritePacket(10, sendBuffer.ToArray());

            int argc = this.stream!.ReadByte();
            if (argc != 1) {
                throw new Exception("Invalid response to command10. Expected 1 arg, got " + argc);
            }

            Span<byte> headerBuffer = stackalloc byte[2];
            int totalRead = 0;
            do {
                this.ReadFully(headerBuffer);
                int header = Unsafe.As<byte, ushort>(ref headerBuffer[0]);
                int chunkSize = header & 0x7FFF;
                if ((header & 0x8000) != 0) { // CCAPI error. Rather than throw error and close connection, just clear the rest of the buffer of junk.
                    dstBuffer.AsSpan(offset + totalRead, count - totalRead).Clear();
                    return;
                }
                
                if (count < chunkSize)
                    throw new IOException("Received more bytes than expected or invalid data");
                
                if (chunkSize > 0) {
                    this.ReadFully(dstBuffer, offset + totalRead, chunkSize);
                }

                totalRead += chunkSize;
                count -= chunkSize;
            } while (count > 0);
        });
    }

    private void ReadFully(Span<byte> span) {
        NetworkStream s = this.stream ?? throw new Exception("Stream closed");
        int remaining = span.Length;
        while (remaining > 0) {
            int read = s.Read(span);
            if (read == 0) {
                // TODO: better timeout, also why won't NetworkStream's timeout work >:(
                Thread.Sleep(1);
                if ((read = s.Read(span)) == 0) {
                    throw new TimeoutException("Timed out reading data");
                }
            }
            
            if (read > 0) {
                remaining -= read;
                span = span.Slice(read, span.Length - read);
            }
        }
    }
    
    private void ReadFully(byte[] buffer, int offset, int count) {
        NetworkStream s = this.stream ?? throw new Exception("Stream closed");
        int total = 0;
        while ((count - total) > 0) {
            int read = s.Read(buffer, offset + total, count - total);
            if (read == 0) {
                // TODO: better timeout, also why won't NetworkStream's timeout work >:(
                Thread.Sleep(1);
                if ((read = s.Read(buffer, offset + total, count - total)) == 0) {
                    throw new TimeoutException("Timed out reading data");
                }
            }
            
            total += read;
        }
    }

    public Task WriteMemory(uint address, byte[] srcBuffer, int offset, int count) {
        if (count < 0)
            return Task.FromException(new ArgumentOutOfRangeException(nameof(count), "Could cannot be negative"));
        if ((address + (uint) count) < address)
            return Task.FromException(new ArgumentOutOfRangeException(nameof(count), "Address overflow with count"));
        // CBA to check OOB for srcBuffer, caller should do it themselves :--)

        return this.RunThreadActionLater(_ => {
            Span<byte> buff4 = stackalloc byte[4];
            while (count > 0) {
                int cbToWrite = Math.Min(count, 65532 /* 64K - sizeof(address) */);
                Unsafe.As<byte, uint>(ref buff4[0]) = address;
                this.WritePacket(9, buff4, srcBuffer.AsSpan(offset, cbToWrite));

                int argc = this.stream!.ReadByte();
                if (argc != 1) {
                    throw new Exception("Invalid response to command9. Expected 1 arg, got " + argc);
                }

                this.ReadFully(buff4);
                if (Unsafe.As<byte, uint>(ref buff4[0]) != 0)
                    throw new Exception("CCAPI error");

                offset += cbToWrite;
                address += (uint) cbToWrite;
                count -= cbToWrite;
            }
        });
    }

    #region Networking utils

    private void WritePacket(int cmdId, Span<byte> buffer) {
        if (buffer.Length > 0x1000)
            throw new Exception($"Attempt to write too many bytes. Max = 65536, tried to write {buffer.Length}");

        Span<byte> header = stackalloc byte[2 * sizeof(uint)];
        Unsafe.As<byte, uint>(ref header[sizeof(uint) * 0]) = (uint) cmdId;
        Unsafe.As<byte, uint>(ref header[sizeof(uint) * 1]) = (uint) buffer.Length;
        this.stream!.Write(header);
        if (buffer.Length > 0) {
            this.stream!.Write(buffer);
        }
    }

    private void WritePacket(int cmdId, Span<byte> buffer1, Span<byte> buffer2) {
        ulong length = (ulong) buffer1.Length + (ulong) buffer2.Length;
        if (length > 0x1000)
            throw new Exception($"Attempt to write too many bytes. Max = 65536, tried to write {length}");

        Span<byte> header = stackalloc byte[2 * sizeof(uint)];
        Unsafe.As<byte, uint>(ref header[sizeof(uint) * 0]) = (uint) cmdId;
        Unsafe.As<byte, uint>(ref header[sizeof(uint) * 1]) = (uint) length;
        this.stream!.Write(header);
        if (buffer1.Length > 0)
            this.stream!.Write(buffer1);
        if (buffer2.Length > 0)
            this.stream!.Write(buffer2);
    }

    private static string ReadStringWithTag(BinaryReader reader) {
        int length = reader.ReadInt32();
        byte[] bytes = reader.ReadBytes(length);
        string text = Encoding.ASCII.GetString(bytes);
        return text;
    }

    #endregion

    private void EnsureNotDisposed() {
        ObjectDisposedException.ThrowIf(this.process == null, this);
    }

    public void Dispose() {
        Process? oldProcess = Interlocked.Exchange(ref this.process, null);
        if (oldProcess != null) {
            this.keepThreadRunning = false;
            this.threadMre.Set();

            if (!oldProcess.HasExited) {
                try {
                    oldProcess.Kill();
                }
                catch {
                    Debug.Fail("Error?!");
                    // don't care
                }
            }

            oldProcess.Dispose();
            this.client?.Dispose();
            this.client = null;
            this.stream = null;
            this.threadCCAPI = null;
            this.threadMre.Dispose();
        }
    }
}