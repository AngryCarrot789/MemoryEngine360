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
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using PFXToolKitUI.Utils;

namespace MemEngine360.PS3.CCAPI;

[SupportedOSPlatform("windows")]
public class ConsoleControlAPI {
    private delegate void ThreadedAction(ConsoleControlAPI api, object? param1, object? param2, CancellationToken cancellation);

    private readonly struct ThreadedActionInfo(ThreadedAction action, object? param1, object? param2, CancellationToken cancellation) {
        public readonly ThreadedAction action = action;
        public readonly object? param1 = param1;
        public readonly object? param2 = param2;
        public readonly CancellationToken cancellation = cancellation;
    }

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
     *   int    CommandID
     *   int    Packed Data Length
     *   byte[] Packet Data
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

    private readonly Lock threadActionInfoLock = new Lock();
    private int threadActionCount; // guarded by lock
    private readonly ThreadedActionInfo[] threadActions; // guarded by lock
    private const int MaxThreadActions = 32;

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

    public EndPoint? EndPoint => this.client?.Client.RemoteEndPoint;

    private ConsoleControlAPI(Process process, TcpClient client) {
        this.process = process;
        this.client = client;
        this.stream = client.GetStream();
        this.threadMre = new ManualResetEvent(false);
        this.keepThreadRunning = true;
        this.threadActions = new ThreadedActionInfo[MaxThreadActions];
        this.threadCCAPI = new Thread(this.ThreadMainConsoleControl) {
            Name = "CCAPI Interop", IsBackground = true
        };

        this.threadCCAPI.Start();
    }

    public static async Task<ConsoleControlAPI> Run(int port = 45678, CancellationToken cancellationToken = default) {
        if (port < 0 || port > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 0 and 65535");

        Process process = new Process() {
            StartInfo = new ProcessStartInfo("ccapi_surrogate.exe") {
                ArgumentList = {
                    port.ToString()
                },
                CreateNoWindow = true, // !Debugger.IsAttached, // create window when debugging
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
            throw new Exception("Failed to start ccapi_surrogate.exe process", e);
        }

        if (!hasStarted.HasValue || !hasStarted.Value) {
            throw new Exception("Failed to start ccapi_surrogate.exe process");
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
            throw new Exception("Could not connect to surrogate process", e);
        }

        ConsoleControlAPI api = new ConsoleControlAPI(process, client);
        try {
            await api.SelfTest(cancellationToken);
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
            api.Dispose();
            throw new Exception("Self-test failed", e);
        }

        if (api.FailureException != null)
            ExceptionDispatchInfo.Throw(api.FailureException);

        return api;
    }

    private Task SelfTest(CancellationToken cancellationToken = default) {
        return this.RunThreadActionLater(static api => {
            const string TestText = "hello!!!";
            byte[] textBuffer = Encoding.UTF8.GetBytes(TestText);
            byte[] fullBuffer = new byte[4 + textBuffer.Length];
            Unsafe.As<byte, int>(ref fullBuffer[0]) = textBuffer.Length;
            textBuffer.AsSpan().CopyTo(fullBuffer.AsSpan(4));
            api.WritePacket(19, fullBuffer);

            int argc = api.ReadFromNetwork<byte>();
            if (argc != 3 /* vars count */) {
                throw new Exception("Self-test failed. Expected 3 args, got " + argc);
            }

            string text = api.ReadTaggedString();
            if (text != TestText) {
                throw new Exception($"Self-test failed. Expected string to be '{TestText}', got '{text}' instead");
            }

            string string2 = api.ReadTaggedString();
            if (string2 != "This is param 1!!!") {
                throw new Exception($"Self-test failed. Expected next string to be 'This is param 1!!!', got '{text}' instead");
            }

            int nextInt = api.ReadFromNetwork<int>();
            if (nextInt != 1234567) {
                throw new Exception($"Self-test failed. Expected next int to be '1234567', got '{nextInt}' instead");
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
                writer.Write(ipAddress.Length);
                foreach (char ch in ipAddress) {
                    writer.Write((byte) ch);
                }

                this.WritePacket(1, memory.ToArray());
            }

            return this.stream!.ReadByte() == 1;
        });
    }

    /// <summary>
    /// Try disconnect from the currently connected PS3
    /// </summary>
    public bool DisconnectFromConsole(bool doNotReadResult) {
        this.WritePacket(2, Span<byte>.Empty);

        if (!doNotReadResult) {
            int argc = this.stream!.ReadByte();
            if (argc != 1) {
                throw new Exception("Invalid response to command5. Expected 1 arg, got " + argc);
            }

            Span<byte> buffer = stackalloc byte[4];
            this.ReadFullyFromNetwork(buffer);

            int ccapiResult = Unsafe.As<byte, int>(ref buffer[0]);
            return ccapiResult == 0;
        }

        return true;
    }

    /// <summary>
    /// Finds the active game PID
    /// </summary>
    /// <returns>The PID, or zero, if no game is running</returns>
    public async Task<(uint, string?)> FindGameProcessId() {
        List<(uint, string?)> result = await this.GetAllProcesses();
        foreach ((uint pid, string? name) proc in result) {
            if (proc.name != null && !proc.name.Contains("dev_flash")) {
                return proc;
            }
        }

        return default;
    }

    public Task<List<(uint, string?)>> GetAllProcesses() {
        return this.RunThreadActionLater(static api => {
            api.WritePacket(8, Span<byte>.Empty);

            List<(uint, string?)> list = new List<(uint, string?)>();
            int length = api.ReadFromNetwork<ushort>();

            Span<byte> buffer8 = stackalloc byte[8];
            for (int i = 0; i < length; i++) {
                api.ReadFullyFromNetwork(buffer8);

                string? processName;
                uint pid = Unsafe.As<byte, uint>(ref buffer8[0]);
                int nameLength = Unsafe.As<byte, int>(ref buffer8[4]);
                if (nameLength > 0) {
                    byte[] asciiBuffer = new byte[nameLength];
                    api.ReadFullyFromNetwork(asciiBuffer, 0, nameLength);
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

    public Task ReadMemory(uint pid, uint address, byte[] dstBuffer, int offset, int count) {
        if (count < 0)
            return Task.FromException(new ArgumentOutOfRangeException(nameof(count), "Could cannot be negative"));
        if (count == 0)
            return Task.CompletedTask;

        return this.RunThreadActionLater(_ => {
            Span<byte> sendBuffer = stackalloc byte[sizeof(uint) + sizeof(ulong) + sizeof(uint)];
            Unsafe.As<byte, uint>(ref sendBuffer[0]) = pid;
            Unsafe.As<byte, ulong>(ref sendBuffer[4]) = address;
            Unsafe.As<byte, uint>(ref sendBuffer[12]) = (uint) count;
            this.WritePacket(7, sendBuffer.ToArray());

            Span<byte> headerBuffer = stackalloc byte[2];
            int totalRead = 0;
            do {
                this.ReadFullyFromNetwork(headerBuffer);
                int header = Unsafe.As<byte, ushort>(ref headerBuffer[0]);
                int chunkSize = header & 0x7FFF;
                if ((header & 0x8000) != 0) { // CCAPI error. Rather than throw error and close connection, just clear the rest of the buffer of junk.
                    dstBuffer.AsSpan(offset + totalRead, count - totalRead).Clear();
                    return;
                }

                if (count < chunkSize)
                    throw new IOException("Received more bytes than expected or invalid data");
                if (chunkSize > 0)
                    this.ReadFullyFromNetwork(dstBuffer, offset + totalRead, chunkSize);

                totalRead += chunkSize;
                count -= chunkSize;
            } while (count > 0);
        });
    }

    public Task WriteMemory(uint pid, uint address, byte[] srcBuffer, int offset, int count) {
        if (count < 0)
            return Task.FromException(new ArgumentOutOfRangeException(nameof(count), "Could cannot be negative"));
        if ((address + (uint) count) < address)
            return Task.FromException(new ArgumentOutOfRangeException(nameof(count), "Address overflow with count"));
        // CBA to check OOB for srcBuffer, caller should do it themselves :--)

        return this.RunThreadActionLater(_ => {
            Span<byte> header = stackalloc byte[sizeof(uint) + sizeof(ulong)];
            for (uint remaining = (uint) count; remaining > 0;) {
                uint cbToWrite = Math.Min(remaining, 65528);
                Unsafe.As<byte, uint>(ref header[0]) = pid;
                Unsafe.As<byte, ulong>(ref header[4]) = address;
                this.WritePacket(6, header, srcBuffer.AsSpan(offset, (int) cbToWrite));

                int ack = this.stream!.ReadByte();
                if (ack != 1) {
                    throw new Exception("Invalid acknowledgement from write memory command");
                }

                offset += (int) cbToWrite;
                address += cbToWrite;
                remaining -= cbToWrite;
            }
        });
    }


    #region Networking utils

    private void WriteToNetwork(ReadOnlySpan<byte> buffer) {
        this.stream!.Write(buffer);
    }

    private void WriteToNetwork<T>(T value) where T : unmanaged {
        Span<byte> span = MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref value), Unsafe.SizeOf<T>());
        this.WriteToNetwork(span);
    }

    private void WriteToNetworkTagged(ReadOnlySpan<byte> buffer) {
        int length = buffer.Length;
        this.stream!.Write(MemoryMarshal.CreateSpan(ref Unsafe.As<int, byte>(ref length), sizeof(int)));
        this.stream!.Write(buffer);
    }

    private void WriteToNetworkTagged(string? text) {
        this.WriteToNetworkTagged(!string.IsNullOrEmpty(text) ? Encoding.UTF8.GetBytes(text) : ReadOnlySpan<byte>.Empty);
    }

    private T ReadFromNetwork<T>() where T : unmanaged {
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<T>()];
        this.ReadFullyFromNetwork(buffer);
        return Unsafe.As<byte, T>(ref MemoryMarshal.GetReference(buffer));
    }

    private void ReadFullyFromNetwork(byte[] buffer, int offset, int count) {
        this.ReadFullyFromNetwork(buffer.AsSpan(offset, count));
    }

    private void ReadFullyFromNetwork(Span<byte> buffer) {
        int total = 0;
        while (total < buffer.Length) {
            int read = this.stream!.Read(buffer.Slice(total));
            if (read < 1) {
                throw new TimeoutException();
            }

            total += read;
        }
    }

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

    private int ReadTaggedArray(byte[] buffer) {
        int length = this.ReadFromNetwork<int>();
        this.ReadFullyFromNetwork(buffer, 0, length);
        return length;
    }

    private string ReadTaggedString() {
        int length = this.ReadFromNetwork<int>();

        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        try {
            this.ReadFullyFromNetwork(buffer, 0, length);
            string text = Encoding.ASCII.GetString(buffer, 0, length);
            return text;
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    #endregion

    #region Threading

    private void ThreadMainConsoleControl() {
        ThreadedActionInfo[] tempArray = new ThreadedActionInfo[MaxThreadActions];
        while (this.keepThreadRunning) {
            this.threadMre.WaitOne();

            // Do not invoke actions in the locked section, in case someone calls Post, which will cause them
            // to be blocked forever. This is especially bad if the UI thread is blocked waiting.

            int count;
            lock (this.threadActionInfoLock) {
                if ((count = this.threadActionCount) < 1) {
                    continue;
                }

                this.threadActionCount = 0;
                Span<ThreadedActionInfo> src = this.threadActions.AsSpan(0, count);
                src.CopyTo(tempArray.AsSpan(0, count));
                src.Clear();
            }

            List<Exception>? exceptions = null;
            for (int i = 0; i < count; i++) {
                ThreadedActionInfo info = tempArray[i];
                Debug.Assert(info.action != null, "Invalid thread action info");

                try {
                    info.action(this, info.param1, info.param2, info.cancellation);
                }
                catch (Exception e) {
                    // Typically IO or Timeout exception
                    (exceptions ??= new List<Exception>()).Add(e);
                }
            }

            if (exceptions != null) {
                this.FailureException = exceptions.Count == 1 ? exceptions[0] : new AggregateException("Multiple errors while invoking actions", exceptions);
                this.NativeFailure?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
    }

    private Task RunThreadActionLater(Action<ConsoleControlAPI> action, CancellationToken cancellation = default) {
        TaskCompletionSource theTcs = new TaskCompletionSource();
        this.PostAction(static (api, p1, p2, token) => {
            Action<ConsoleControlAPI> act = (Action<ConsoleControlAPI>) p1!;
            TaskCompletionSource tcs = (TaskCompletionSource) p2!;
            if (token.IsCancellationRequested) {
                tcs.SetCanceled(token);
                return;
            }

            if (!api.keepThreadRunning) {
                tcs.SetCanceled(CancellationToken.None); // None can be used to tell difference between cancelled and aborted 
                return;
            }

            try {
                api.EnsureNotDisposed();
                act(api);
                tcs.SetResult();
            }
            catch (Exception e) {
                tcs.SetException(e);
                throw; // stop thread, causing API to get disposed
            }
        }, action, theTcs, cancellation);

        return theTcs.Task;
    }

    private Task<T> RunThreadActionLater<T>(Func<ConsoleControlAPI, T> func, CancellationToken cancellation = default) {
        TaskCompletionSource<T> theTcs = new TaskCompletionSource<T>();
        this.PostAction(static (api, p1, p2, token) => {
            Func<ConsoleControlAPI, T> fn = (Func<ConsoleControlAPI, T>) p1!;
            TaskCompletionSource<T> tcs = (TaskCompletionSource<T>) p2!;
            if (token.IsCancellationRequested) {
                tcs.SetCanceled(token);
                return;
            }

            if (!api.keepThreadRunning) {
                tcs.SetCanceled(CancellationToken.None); // None can be used to tell difference between cancelled and aborted 
                return;
            }

            try {
                api.EnsureNotDisposed();
                tcs.SetResult(fn(api));
            }
            catch (Exception e) {
                tcs.SetException(e);
                throw; // stop thread, causing API to get disposed
            }
        }, func, theTcs, cancellation);

        return theTcs.Task;
    }

    private void PostAction(ThreadedAction action, object? param1, object? param2, CancellationToken cancellation) {
        lock (this.threadActionInfoLock) {
            if (this.threadActionCount >= MaxThreadActions) {
                throw new InvalidOperationException("Maximum number of actions already queued");
            }

            this.threadActions[this.threadActionCount++] = new ThreadedActionInfo(action, param1, param2, cancellation);
        }

        this.threadMre.Set();
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