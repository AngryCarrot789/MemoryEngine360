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
using System.Buffers.Binary;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using PFXToolKitUI.Utils;

namespace MemEngine360.PS3.CCWin32;

/// <summary>
/// An wrapper into the native CC API
/// </summary>
public sealed class ConsoleControlWin32 {
    public const uint DefaultProcess = 0xFFFFFFFF;
    public const int OK = 0;
    public const int ERROR = -1;
    
    private readonly ConsoleControlNativesWin32 natives;

    private Action<ConsoleControlWin32>? threadAction;
    private readonly ManualResetEvent threadMre;
    private bool keepThreadRunning;

    /// <summary>
    /// Gets or sets the attached process ID
    /// </summary>
    public uint AttachedProcessId { get; set; } = DefaultProcess;

    public ConsoleControlWin32(ConsoleControlNativesWin32 natives) {
        this.natives = natives;
        this.threadMre = new ManualResetEvent(false);
        new Thread(this.ThreadMainConsoleControl) {
            Name = "Thread CCAPI Interop", IsBackground = true
        }.Start();
    }

    // We use a background thread to process requests, because we do not want to block async callers.
    // CCAPI is said to be thread safe, so we could use a few threads at one point.
    private void ThreadMainConsoleControl() {
        while (this.keepThreadRunning) {
            this.threadMre.WaitOne();
            Action<ConsoleControlWin32>? action = Interlocked.Exchange(ref this.threadAction, null);
            action?.Invoke(this);
        }
    }
    
    private int ReadMemoryCore(uint addr, uint size, byte[] data) {
        using GCHandleAlloc dataAlloc = new GCHandleAlloc(data);
        return this.natives.CCAPIGetMemory(this.AttachedProcessId, addr, size, dataAlloc);
    }

    private (int Result, byte Value) ReadU8Core(uint address) {
        using GCHandleAllocArray<byte> alloc = new GCHandleAllocArray<byte>(sizeof(byte));
        int ret = this.natives.CCAPIGetMemory(this.AttachedProcessId, address, sizeof(byte), alloc);
        return (ret, alloc.Array[0]);
    }

    private (int Result, ushort Value) ReadU16Core(uint address) {
        using GCHandleAllocArray<byte> alloc = new GCHandleAllocArray<byte>(sizeof(ushort));
        int ret = this.natives.CCAPIGetMemory(this.AttachedProcessId, address, sizeof(ushort), alloc);
        return (ret, BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(alloc.Array, 0, sizeof(ushort))));
    }

    private (int Result, uint Value) ReadU32Core(uint address) {
        using GCHandleAllocArray<byte> alloc = new GCHandleAllocArray<byte>(sizeof(uint));
        int ret = this.natives.CCAPIGetMemory(this.AttachedProcessId, address, sizeof(uint), alloc);
        return (ret, BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(alloc.Array, 0, sizeof(uint))));
    }

    private (int Result, ulong Value) ReadU64Core(uint address) {
        using GCHandleAllocArray<byte> alloc = new GCHandleAllocArray<byte>(sizeof(ulong));
        int ret = this.natives.CCAPIGetMemory(this.AttachedProcessId, address, sizeof(ulong), alloc);
        return (ret, BinaryPrimitives.ReadUInt64BigEndian(new ReadOnlySpan<byte>(alloc.Array, 0, sizeof(ulong))));
    }

    private int WriteMemoryCore(uint addr, uint size, byte[] data) {
        using GCHandleAlloc dataAlloc = new GCHandleAlloc(data);
        return this.natives.CCAPISetMemory(this.AttachedProcessId, addr, size, dataAlloc);
    }

    private int WriteU8Core(uint address, byte value) {
        using GCHandleAllocArray<byte> alloc = new GCHandleAllocArray<byte>(sizeof(byte));
        alloc.Span[0] = value;
        return this.natives.CCAPISetMemory(this.AttachedProcessId, address, sizeof(byte), alloc);
    }

    private int WriteU16Core(uint address, ushort value) {
        using GCHandleAllocArray<byte> alloc = new GCHandleAllocArray<byte>(sizeof(ushort));
        BinaryPrimitives.WriteUInt16BigEndian(alloc.Span, value);
        return this.natives.CCAPISetMemory(this.AttachedProcessId, address, sizeof(ushort), alloc);
    }

    private int WriteU32Core(uint address, uint value) {
        using GCHandleAllocArray<byte> alloc = new GCHandleAllocArray<byte>(sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(alloc.Span, value);
        return this.natives.CCAPISetMemory(this.AttachedProcessId, address, sizeof(uint), alloc);
    }

    private int WriteU64Core(uint address, ulong value) {
        using GCHandleAllocArray<byte> alloc = new GCHandleAllocArray<byte>(sizeof(ulong));
        BinaryPrimitives.WriteUInt64BigEndian(alloc.Span, value);
        return this.natives.CCAPISetMemory(this.AttachedProcessId, address, sizeof(ulong), alloc);
    }

    public void Dispose() {
        this.keepThreadRunning = false;
    }

    private Task<T> RunThreadActionLater<T>(Func<ConsoleControlWin32, T> func) {
        TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
        Action<ConsoleControlWin32> delegateAction = (t) => {
            if (!t.keepThreadRunning) {
                tcs.SetCanceled();
                return;
            }

            try {
                tcs.SetResult(func(t));
            }
            catch (Exception e) {
                tcs.SetException(e);
            }
        };

        if (Interlocked.CompareExchange(ref this.threadAction, delegateAction, null) != null) {
            throw new InvalidOperationException("Another action already queued");
        }

        this.threadMre.Set();
        return tcs.Task;
    }

    public async Task<bool> IsConnected() {
        int state = 0;
        int res = await this.RunThreadActionLater(t => t.natives.CCAPIGetConnectionStatus(ref state));
        return res == OK && state != 0;
    }

    public Task<int> Connect(string ip) {
        return this.RunThreadActionLater(t => t.natives.CCAPIConnectConsole(ip));
    }

    public Task<int> Disconnect() {
        return this.RunThreadActionLater(static t => t.natives.CCAPIDisconnectConsole());
    }

    public Task<int> GetDllVersion() {
        return this.RunThreadActionLater(static t => t.natives.CCAPIGetDllVersion());
    }

    public Task<List<ConsoleInfo>> GetConsoleList() {
        return this.RunThreadActionLater(t => {
            List<ConsoleInfo> list = new List<ConsoleInfo>();

            using HGlobalAlloc name = new HGlobalAlloc(512 * sizeof(char));
            using HGlobalAlloc ip = new HGlobalAlloc(512 * sizeof(char));
            for (int i = 0; i < t.natives.CCAPIGetNumberOfConsoles(); i++) {
                ConsoleInfo c = new ConsoleInfo();
                t.natives.CCAPIGetConsoleInfo(i, name, ip);
                c.name = Marshal.PtrToStringAnsi(name);
                c.ip = Marshal.PtrToStringAnsi(ip);
                list.Add(c);
            }

            return list;
        });
    }

    public Task<List<ProcessInfo>> GetProcessList() {
        return this.RunThreadActionLater(static t => {
            List<ProcessInfo> list = new List<ProcessInfo>();
            uint pidCount = 32;
            
            using GCHandleAllocArray<uint> pidAlloc = new GCHandleAllocArray<uint>(32); 
            int ret = t.natives.CCAPIGetProcessList(ref pidCount, pidAlloc);
            if (ret != OK) {
                return list;
            }

            Span<uint> pidSpan = pidAlloc.Span;
            using HGlobalAlloc nameAlloc = new HGlobalAlloc(512 * sizeof(char));
            for (uint i = 0; i < pidCount; i++) {
                uint pid = pidSpan[(int) i];
                ret = t.natives.CCAPIGetProcessName(pid, nameAlloc);
                if (ret != OK) {
                    return list;
                }

                ProcessInfo info = new ProcessInfo {
                    pid = pid,
                    name = Marshal.PtrToStringAnsi(nameAlloc)
                };

                list.Add(info);
            }

            return list;
        });
    }

    public async Task<Optional<uint>> TryGetGameProcess() {
        List<ProcessInfo> list = await this.GetProcessList();
        for (int i = 0; i < list.Count; i++) {
            string? processName = list[i].name;
            if (processName != null && !processName.Contains("dev_flash")) {
                return new Optional<uint>(list[i].pid);
            }
        }

        return default;
    }

    public Task<int> ReadMemory(ulong addr, uint size, byte[] data) {
        return this.RunThreadActionLater(self => {
            using GCHandleAlloc dataAlloc = new GCHandleAlloc(data);
            return self.natives.CCAPIGetMemory(this.AttachedProcessId, addr, size, dataAlloc);
        });
    }

    public Task<int> ReadMemory(uint addr, uint size, byte[] data) {
        return this.RunThreadActionLater(self => self.ReadMemoryCore(addr, size, data));
    }

    public Task<int> WriteMemory(uint addr, uint size, byte[] data) {
        return this.RunThreadActionLater(self => self.WriteMemoryCore(addr, size, data));
    }

    public Task<(int, int Cell, int Rsx)> GetTemperature() {
        return this.RunThreadActionLater(self => {
            int cell = 0, rsx = 0;
            int ret = self.natives.CCAPIGetTemperature(ref cell, ref rsx);
            return (ret, cell, rsx);
        });
    }

    public Task<int> Shutdown(ShutdownMode mode) {
        return this.RunThreadActionLater(self => self.natives.CCAPIShutdown((int) mode));
    }

    public Task<int> RingBuzzer(BuzzerType type) {
        return this.RunThreadActionLater(self => self.natives.CCAPIRingBuzzer((int) type));
    }

    public Task<int> SetConsoleLed(ColorLed color, StatusLed st) {
        return this.RunThreadActionLater(self => self.natives.CCAPISetConsoleLed((int) color, (int) st));
    }

    public Task<int> SetConsoleIds(ConsoleIdType t, string id) {
        if (id.Length != 32)
            return Task.FromException<int>(new ArgumentException("Id must have a length of 32"));
        return this.SetConsoleIds(t, StringToArray(id));
    }

    public Task<int> SetConsoleIds(ConsoleIdType t, byte[] id) {
        return this.RunThreadActionLater(self => {
            using GCHandleAlloc dataAlloc = new GCHandleAlloc(id);
            return self.natives.CCAPISetConsoleIds((int) t, dataAlloc);
        });
    }

    public Task<int> SetBootConsoleIds(ConsoleIdType t, [Range(32, 32)] string id) {
        if (id.Length != 32)
            return Task.FromException<int>(new ArgumentException("Id must have a length of 32"));
        return this.SetBootConsoleIds(t, StringToArray(id));
    }

    public Task<int> SetBootConsoleIds(ConsoleIdType t, byte[] id) {
        return this.RunThreadActionLater(self => {
            using GCHandleAlloc dataAlloc = new GCHandleAlloc(id);
            return self.natives.CCAPISetBootConsoleIds((int) t, 1, dataAlloc);
        });
    }

    public Task<int> ResetBootConsoleIds(ConsoleIdType t) {
        return this.RunThreadActionLater(self => self.natives.CCAPISetBootConsoleIds((int) t, 0, IntPtr.Zero));
    }

    public Task<int> VshNotify(NotifyIcon icon, string msg) {
        return this.RunThreadActionLater(self => self.natives.CCAPIVshNotify((int) icon, msg));
    }

    public Task<(int, int Firmware, int CCApiVersion, ConsoleType Type)> GetFirmwareInfo() {
        return this.RunThreadActionLater(self => {
            int firmware = 0, ccapiVersion = 0, cType = 0;
            int ret = this.natives.CCAPIGetFirmwareInfo(ref firmware, ref ccapiVersion, ref cType);
            return (ret, firmware, ccapiVersion, (ConsoleType) cType);
        });
    }

    public Task<(int Result, byte Value)> ReadMemoryU8(uint addr) {
        return this.RunThreadActionLater(t => t.ReadU8Core(addr));
    }

    public Task<(int Result, ushort Value)> ReadMemoryU16(uint addr) {
        return this.RunThreadActionLater(t => t.ReadU16Core(addr));
    }

    public Task<(int Result, uint Value)> ReadMemoryU32(uint addr) {
        return this.RunThreadActionLater(t => t.ReadU32Core(addr));
    }

    public Task<(int Result, ulong Value)> ReadMemoryU64(uint addr) {
        return this.RunThreadActionLater(t => t.ReadU64Core(addr));
    }

    public Task<(int Result, float Value)> ReadMemoryF32(uint addr) {
        return this.RunThreadActionLater(t => {
            (int Result, uint Value) result = t.ReadU32Core(addr);
            return (result.Result, Unsafe.As<uint, float>(ref result.Value));
        });
    }

    public Task<(int Result, double Value)> ReadMemoryF64(uint addr) {
        return this.RunThreadActionLater(t => {
            (int Result, ulong Value) result = t.ReadU64Core(addr);
            return (result.Result, Unsafe.As<ulong, double>(ref result.Value));
        });
    }

    public Task<string> ReadMemoryString(uint addr) {
        return this.RunThreadActionLater(self => {
            StringBuilder sb = new StringBuilder();
            using GCHandleAllocArray<byte> dataAlloc = new GCHandleAllocArray<byte>(0x100);
            while (true) {
                uint size = (uint) dataAlloc.Length;
                int r = self.natives.CCAPIGetMemory(self.AttachedProcessId, addr, size, dataAlloc);
                if (r != OK) {
                    break;
                }

                Span<byte> arraydata = dataAlloc.Span;
                for (int i = 0; i < arraydata.Length; i++) {
                    if (arraydata[i] == 0) {
                        sb.Append(Encoding.ASCII.GetString(dataAlloc.Array, 0, i));
                        goto EndOfString;
                    }
                }

                addr += (uint) dataAlloc.Length;
                sb.Append(Encoding.ASCII.GetString(dataAlloc.Array, 0, dataAlloc.Length));
            }

            EndOfString:
            return sb.ToString();
        });
    }

    public Task<int> WriteMemoryU8(uint addr, byte value) {
        return this.RunThreadActionLater(t => t.WriteU8Core(addr, value));
    }

    public Task<int> WriteMemoryU16(uint addr, ushort value) {
        return this.RunThreadActionLater(t => t.WriteU16Core(addr, value));
    }

    public Task<int> WriteMemoryU32(uint addr, uint value) {
        return this.RunThreadActionLater(t => t.WriteU32Core(addr, value));
    }

    public Task<int> WriteMemoryU64(uint addr, ulong value) {
        return this.RunThreadActionLater(t => t.WriteU64Core(addr, value));
    }

    public Task<int> WriteMemoryF32(uint addr, float value) {
        return this.RunThreadActionLater(t => t.WriteU32Core(addr, Unsafe.As<float, uint>(ref value)));
    }

    public Task<int> ReadMemoryF64(uint addr, double value) {
        return this.RunThreadActionLater(t => t.WriteU64Core(addr, Unsafe.As<double, ulong>(ref value)));
    }

    public Task<int> WriteMemoryString(uint addr, string s) {
        return this.RunThreadActionLater(t => {
            if (!s.EndsWith('\0')) {
                s += '\0';
            }

            byte[] b = Encoding.ASCII.GetBytes(s);
            return this.WriteMemoryCore(addr, (uint) b.Length, b);
        });
    }

    public static string FirmwareToString(int firmware) {
        int l = (firmware >> 12) & 0xFF;
        int h = firmware >> 24;

        return String.Format("{0:X}.{1:X}", h, l);
    }

    public static string ConsoleTypeToString(ConsoleType cType) {
        return cType switch {
            ConsoleType.CEX => "CEX",
            ConsoleType.DEX => "DEX",
            ConsoleType.TOOL => "TOOL",
            _ => "UNK"
        };
    }

    public static byte[] StringToArray(string s) {
        if (s.Length == 0) {
            return Array.Empty<byte>();
        }

        if ((s.Length % 2) != 0) {
            s += "0";
        }

        byte[] b = new byte[s.Length / 2];
        int j = 0;
        for (int i = 0; i < s.Length; i += 2) {
            var sb = s.Substring(i, 2);
            b[j++] = Convert.ToByte(sb, 16);
        }

        return b;
    }

    public struct ProcessInfo {
        public uint pid;
        public string? name;
    };

    public struct ConsoleInfo {
        public string? name, ip;
    };

    public enum ConsoleIdType {
        Idps = 0,
        Psid = 1,
    };

    public enum ShutdownMode {
        Shutdown = 1,
        SoftReboot = 2,
        HardReboot = 3,
    };

    public enum BuzzerType {
        Continuous = 0,
        Single = 1,
        Double = 2,
        Triple = 3,
    };

    public enum ColorLed {
        Green = 1,
        Red = 2,
    };

    public enum StatusLed {
        Off = 0,
        On = 1,
        Blink = 2,
    };

    public enum NotifyIcon {
        Info = 0,
        Caution = 1,
        Friend = 2,
        Slider = 3,
        WrongWay = 4,
        Dialog = 5,
        DalogShadow = 6,
        Text = 7,
        Pointer = 8,
        Grab = 9,
        Hand = 10,
        Pen = 11,
        Finger = 12,
        Arrow = 13,
        ArrowRight = 14,
        Progress = 15,
        Trophy1 = 16,
        Trophy2 = 17,
        Trophy3 = 18,
        Trophy4 = 19
    };

    public enum ConsoleType {
        UNK = 0,
        CEX = 1,
        DEX = 2,
        TOOL = 3,
    };
    
    private ref struct HGlobalAlloc(int cb) {
        public IntPtr Address { get; private set; } = Marshal.AllocHGlobal(cb);

        public static implicit operator IntPtr(HGlobalAlloc value) {
            return value.Address;
        }

        public void Dispose() {
            if (this.Address != IntPtr.Zero) {
                Marshal.FreeHGlobal(this.Address);
                this.Address = IntPtr.Zero;
            }
        }
    }

    private readonly ref struct GCHandleAlloc : IDisposable {
        public GCHandle GCHandle { get; }

        public GCHandleAlloc(object value) {
            this.GCHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
        }

        public GCHandleAlloc(GCHandle gcHandle) {
            this.GCHandle = gcHandle;
        }

        public static implicit operator IntPtr(GCHandleAlloc value) {
            return value.GCHandle.AddrOfPinnedObject();
        }

        public void Dispose() {
            this.GCHandle.Free();
        }
    }

    private readonly ref struct GCHandleAllocArray<T> : IDisposable {
        public GCHandle GCHandle { get; }

        public T[] Array { get; }

        public int Length { get; }

        public Span<T> Span => this.Array.AsSpan(0, this.Length);

        public GCHandleAllocArray(int length) {
            this.Length = length;
            this.Array = ArrayPool<T>.Shared.Rent(length);
            this.GCHandle = GCHandle.Alloc(this.Array, GCHandleType.Pinned);
        }

        public static implicit operator IntPtr(GCHandleAllocArray<T> value) {
            return value.GCHandle.AddrOfPinnedObject();
        }

        public void Dispose() {
            ArrayPool<T>.Shared.Return(this.Array);
            this.GCHandle.Free();
        }
    }
}