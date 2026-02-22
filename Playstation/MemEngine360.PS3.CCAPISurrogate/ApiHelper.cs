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
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace MemEngine360.PS3.CCAPISurrogate;

public sealed class ApiHelper {
    private readonly UnmanagedCCAPI api;

    public ApiHelper(UnmanagedCCAPI api) {
        this.api = api;
    }

    public void Dispose() => this.api.Dispose();
    
    public bool Connect(string ip) {
        return this.api.CCAPIConnectConsole(ip) == 0;
    }

    public bool Disconnect() {
        return this.api.CCAPIDisconnectConsole() == 0;
    }

    public bool GetConnectionStatus(out bool isConnected) {
        int status = 0;
        if (this.api.CCAPIGetConnectionStatus(ref status) == 0) {
            isConnected = status != 0;
        }
        else {
            isConnected = false;
        }
        
        return status != 0;
    }

    public bool SetBootConsoleIds(ConsoleIdType t, [Range(32, 32)] string id) {
        if (id.Length != 32)
            throw new ArgumentException("Id must have a length of 32");
        return this.SetBootConsoleIds(t, StringToArray(id));
    }

    public bool SetConsoleIds(ConsoleIdType t, string id) {
        if (id.Length != 32)
            throw new ArgumentException("Id must have a length of 32");
        return this.SetConsoleIds(t, StringToArray(id));
    }

    public bool SetConsoleIds(ConsoleIdType t, byte[] id) {
        using GCHandleAlloc dataAlloc = new GCHandleAlloc(id);
        return this.api.CCAPISetConsoleIds((int) t, dataAlloc) == 0;
    }

    public bool SetBootConsoleIds(ConsoleIdType t, byte[] id) {
        using GCHandleAlloc dataAlloc = new GCHandleAlloc(id);
        return this.api.CCAPISetBootConsoleIds((int) t, 1, dataAlloc) == 0;
    }

    public bool ResetBootConsoleIds(ConsoleIdType t) {
        return this.api.CCAPISetBootConsoleIds((int) t, 0, IntPtr.Zero) == 0;
    }

    public bool WriteMemory(uint pid, ulong address, uint size, byte[] data) {
        using GCHandleAlloc dataAlloc = new GCHandleAlloc(data);
        return this.WriteMemory(pid, address, size, dataAlloc);
    }
    
    public bool WriteMemory(uint pid, ulong address, uint size, IntPtr dataHandle) {
        return this.api.CCAPISetMemory(pid, address, size, dataHandle) == 0;
    }

    public bool WriteMemoryString(uint pid, ulong addr, string s) {
        if (!s.EndsWith('\0')) {
            s += '\0';
        }

        byte[] b = Encoding.ASCII.GetBytes(s);
        return this.WriteMemory(pid, addr, (uint) b.Length, b);
    }

    public bool ReadMemory(uint pid, ulong address, uint size, byte[] data) {
        using GCHandleAlloc dataAlloc = new GCHandleAlloc(data);
        return this.ReadMemory(pid, address, size, dataAlloc);
    }
    
    public bool ReadMemory(uint pid, ulong address, uint size, IntPtr dataHandle) {
        return this.api.CCAPIGetMemory(pid, address, size, dataHandle) == 0;
    }

    public bool ReadString(uint pid, ulong addr, [NotNullWhen(true)] out string? value) {
        StringBuilder sb = new StringBuilder();

        const int BUFFER_SIZE = 0x100;
        using GCHandleAllocArray<byte> dataAlloc = new GCHandleAllocArray<byte>(BUFFER_SIZE);
        while (true) {
            if (this.api.CCAPIGetMemory(pid, addr, BUFFER_SIZE, dataAlloc) != 0) {
                value = null;
                return false;
            }
            
            for (int i = 0; i < BUFFER_SIZE; i++) {
                if (dataAlloc.Array[i] == 0) {
                    sb.Append(Encoding.ASCII.GetString(dataAlloc.Array, 0, i));
                    goto EndOfString;
                }
            }

            addr += BUFFER_SIZE;
            sb.Append(Encoding.ASCII.GetString(dataAlloc.Array, 0, BUFFER_SIZE));
        }

        EndOfString:
        value = sb.ToString();
        return true;
    }

    public bool GetProcessList(out List<ProcessInfo> list) {
        list = new List<ProcessInfo>(4 /* most likely won't be more than 2 */);

        uint pidCount = 32;
        using GCHandleAllocArray<uint> pidAlloc = new GCHandleAllocArray<uint>(32);
        if (this.api.CCAPIGetProcessList(ref pidCount, pidAlloc) != 0) {
            return false;
        }
        
        using HGlobalAlloc nameAlloc = new HGlobalAlloc(512 * sizeof(char));
        for (uint i = 0; i < pidCount; i++) {
            uint pid = pidAlloc.Array[(int) i];
            if (this.api.CCAPIGetProcessName(pid, nameAlloc) != 0) {
                return false;
            }
            
            list.Add(new ProcessInfo {
                pid = pid,
                name = Marshal.PtrToStringAnsi(nameAlloc)
            });
        }

        return true;
    }

    public bool GetProcessName(uint pid, [NotNullWhen(true)] out string? name) {
        using HGlobalAlloc nameAlloc = new HGlobalAlloc(512 * sizeof(char));
        if (this.api.CCAPIGetProcessName(pid, nameAlloc) != 0) {
            name = null;
            return false;
        }
        
        name = Marshal.PtrToStringAnsi(nameAlloc)!;
        return true;
    }

    /// <summary>
    /// Returns false when a CCAPI error occurred. Otherwise returns true, but the processInfo parameter may still be null 
    /// </summary>
    public bool GetGameProcess(out ProcessInfo? processInfo) {
        if (!this.GetProcessList(out List<ProcessInfo> infoList)) {
            processInfo = null;
            return false;
        }
        
        foreach (ProcessInfo info in infoList) {
            if (info.name != null && !info.name.Contains("dev_flash")) {
                processInfo = info;
                return true;
            }
        }

        processInfo = null;
        return true;
    }

    public bool GetTemperature(out int cpu, out int rsx) {
        cpu = 0;
        rsx = 0;
        return this.api.CCAPIGetTemperature(ref cpu, ref rsx) == 0;
    }

    public bool Shutdown(ShutdownMode mode) {
        return this.api.CCAPIShutdown((int) mode) == 0;
    }

    public bool RingBuzzer(BuzzerType type) {
        return this.api.CCAPIRingBuzzer((int) type) == 0;
    }

    public bool SetConsoleLed(ColorLed color, StatusLed st) {
        return this.api.CCAPISetConsoleLed((int) color, (int) st) == 0;
    }

    public bool GetFirmwareInfo(out int firmware, out int ccApiVersion, out ConsoleType consoleType) {
        firmware = 0;
        ccApiVersion = 0;
        int cType = 0;
        if (this.api.CCAPIGetFirmwareInfo(ref firmware, ref ccApiVersion, ref cType) == 0) {
            consoleType = (ConsoleType) cType;
            return true;
        }

        consoleType = ConsoleType.UNK;
        return false;
    }

    public bool VshNotify(NotifyIcon icon, string msg) {
        return this.api.CCAPIVshNotify((int) icon, msg) == 0;
    }

    public List<ConsoleInfo> GetConsoleList() {
        List<ConsoleInfo> list = new List<ConsoleInfo>(4);
        using HGlobalAlloc name = new HGlobalAlloc(512 * sizeof(char));
        using HGlobalAlloc ip = new HGlobalAlloc(512 * sizeof(char));
        for (int i = 0; i < this.api.CCAPIGetNumberOfConsoles(); i++) {
            if (this.api.CCAPIGetConsoleInfo(i, name, ip) == 0) {
                list.Add(new ConsoleInfo {
                    name = Marshal.PtrToStringAnsi(name),
                    ip = Marshal.PtrToStringAnsi(ip)
                });
            }
        }

        return list;
    }

    public int GetDllVersion() {
        return this.api.CCAPIGetDllVersion();
    }

    public static string FirmwareToString(int firmware) {
        int l = (firmware >> 12) & 0xFF;
        int h = firmware >> 24;
        return $"{h:X}.{l:X}";
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
            string sb = s.Substring(i, 2);
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

    public readonly ref struct GCHandleAlloc : IDisposable {
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

    public readonly ref struct GCHandleAllocArray<T> : IDisposable {
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
            this.GCHandle.Free();
            ArrayPool<T>.Shared.Return(this.Array);
        }
    }
}