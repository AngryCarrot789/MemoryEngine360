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

using System.Runtime.InteropServices;

namespace MemEngine360.PS3.CCWin32;

public class ConsoleControlNativesWin32 : IDisposable {
    private Natives natives;

    public CCAPIConnectConsole_t CCAPIConnectConsole => this.natives.CCAPIConnectConsole ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPIDisconnectConsole_t CCAPIDisconnectConsole => this.natives.CCAPIDisconnectConsole ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPIGetConnectionStatus_t CCAPIGetConnectionStatus => this.natives.CCAPIGetConnectionStatus ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPISetBootConsoleIds_t CCAPISetBootConsoleIds => this.natives.CCAPISetBootConsoleIds ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPISetConsoleIds_t CCAPISetConsoleIds => this.natives.CCAPISetConsoleIds ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPISetMemory_t CCAPISetMemory => this.natives.CCAPISetMemory ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPIGetMemory_t CCAPIGetMemory => this.natives.CCAPIGetMemory ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPIGetProcessList_t CCAPIGetProcessList => this.natives.CCAPIGetProcessList ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPIGetProcessName_t CCAPIGetProcessName => this.natives.CCAPIGetProcessName ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPIGetTemperature_t CCAPIGetTemperature => this.natives.CCAPIGetTemperature ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPIShutdown_t CCAPIShutdown => this.natives.CCAPIShutdown ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPIRingBuzzer_t CCAPIRingBuzzer => this.natives.CCAPIRingBuzzer ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPISetConsoleLed_t CCAPISetConsoleLed => this.natives.CCAPISetConsoleLed ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPIGetFirmwareInfo_t CCAPIGetFirmwareInfo => this.natives.CCAPIGetFirmwareInfo ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPIVshNotify_t CCAPIVshNotify => this.natives.CCAPIVshNotify ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPIGetNumberOfConsoles_t CCAPIGetNumberOfConsoles => this.natives.CCAPIGetNumberOfConsoles ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPIGetConsoleInfo_t CCAPIGetConsoleInfo => this.natives.CCAPIGetConsoleInfo ?? throw new InvalidOperationException("Native library unloaded");
    public CCAPIGetDllVersion_t CCAPIGetDllVersion => this.natives.CCAPIGetDllVersion ?? throw new InvalidOperationException("Native library unloaded");

    public ConsoleControlNativesWin32() {
    }

    public Task LoadAsync(string libraryPath) {
        if (Volatile.Read(ref this.natives.Library) != IntPtr.Zero) {
            throw new InvalidOperationException("Library already loaded");
        }

        TaskCompletionSource tcs = new TaskCompletionSource();
        new Thread(() => {
            try {
                this.ThreadMainLoadConsoleControlAPI(libraryPath);
                tcs.SetResult();
            }
            catch (Exception e) {
                tcs.SetException(e);
            }
        }) {
            Name = "Thread Load CCAPI",
            IsBackground = true
        }.Start();
        return tcs.Task;
    }

    private void ThreadMainLoadConsoleControlAPI(string libraryPath) {
        IntPtr hLibrary;
        try {
            hLibrary = NativeLibrary.Load(libraryPath);
        }
        catch (Exception e) {
            throw new Exception("Failed to load CCAPI library: " + libraryPath, e);
        }

        this.natives = new Natives {
            Library = hLibrary,
            CCAPIConnectConsole = Marshal.GetDelegateForFunctionPointer<CCAPIConnectConsole_t>(NativeLibrary.GetExport(hLibrary, "CCAPIConnectConsole")),
            CCAPIDisconnectConsole = Marshal.GetDelegateForFunctionPointer<CCAPIDisconnectConsole_t>(NativeLibrary.GetExport(hLibrary, "CCAPIDisconnectConsole")),
            CCAPIGetConnectionStatus = Marshal.GetDelegateForFunctionPointer<CCAPIGetConnectionStatus_t>(NativeLibrary.GetExport(hLibrary, "CCAPIGetConnectionStatus")),
            CCAPISetBootConsoleIds = Marshal.GetDelegateForFunctionPointer<CCAPISetBootConsoleIds_t>(NativeLibrary.GetExport(hLibrary, "CCAPISetBootConsoleIds")),
            CCAPISetConsoleIds = Marshal.GetDelegateForFunctionPointer<CCAPISetConsoleIds_t>(NativeLibrary.GetExport(hLibrary, "CCAPISetConsoleIds")),
            CCAPISetMemory = Marshal.GetDelegateForFunctionPointer<CCAPISetMemory_t>(NativeLibrary.GetExport(hLibrary, "CCAPISetMemory")),
            CCAPIGetMemory = Marshal.GetDelegateForFunctionPointer<CCAPIGetMemory_t>(NativeLibrary.GetExport(hLibrary, "CCAPIGetMemory")),
            CCAPIGetProcessList = Marshal.GetDelegateForFunctionPointer<CCAPIGetProcessList_t>(NativeLibrary.GetExport(hLibrary, "CCAPIGetProcessList")),
            CCAPIGetProcessName = Marshal.GetDelegateForFunctionPointer<CCAPIGetProcessName_t>(NativeLibrary.GetExport(hLibrary, "CCAPIGetProcessName")),
            CCAPIGetTemperature = Marshal.GetDelegateForFunctionPointer<CCAPIGetTemperature_t>(NativeLibrary.GetExport(hLibrary, "CCAPIGetTemperature")),
            CCAPIShutdown = Marshal.GetDelegateForFunctionPointer<CCAPIShutdown_t>(NativeLibrary.GetExport(hLibrary, "CCAPIShutdown")),
            CCAPIRingBuzzer = Marshal.GetDelegateForFunctionPointer<CCAPIRingBuzzer_t>(NativeLibrary.GetExport(hLibrary, "CCAPIRingBuzzer")),
            CCAPISetConsoleLed = Marshal.GetDelegateForFunctionPointer<CCAPISetConsoleLed_t>(NativeLibrary.GetExport(hLibrary, "CCAPISetConsoleLed")),
            CCAPIGetFirmwareInfo = Marshal.GetDelegateForFunctionPointer<CCAPIGetFirmwareInfo_t>(NativeLibrary.GetExport(hLibrary, "CCAPIGetFirmwareInfo")),
            CCAPIVshNotify = Marshal.GetDelegateForFunctionPointer<CCAPIVshNotify_t>(NativeLibrary.GetExport(hLibrary, "CCAPIVshNotify")),
            CCAPIGetNumberOfConsoles = Marshal.GetDelegateForFunctionPointer<CCAPIGetNumberOfConsoles_t>(NativeLibrary.GetExport(hLibrary, "CCAPIGetNumberOfConsoles")),
            CCAPIGetConsoleInfo = Marshal.GetDelegateForFunctionPointer<CCAPIGetConsoleInfo_t>(NativeLibrary.GetExport(hLibrary, "CCAPIGetConsoleInfo")),
            CCAPIGetDllVersion = Marshal.GetDelegateForFunctionPointer<CCAPIGetDllVersion_t>(NativeLibrary.GetExport(hLibrary, "CCAPIGetDllVersion"))
        };
    }

    public void Dispose() {
        Natives n = this.natives;
        this.natives = default;
        if (n.Library != IntPtr.Zero) {
            NativeLibrary.Free(n.Library);
        }
    }

    private struct Natives {
        public IntPtr Library;
        public CCAPIConnectConsole_t CCAPIConnectConsole;
        public CCAPIDisconnectConsole_t CCAPIDisconnectConsole;
        public CCAPIGetConnectionStatus_t CCAPIGetConnectionStatus;
        public CCAPISetBootConsoleIds_t CCAPISetBootConsoleIds;
        public CCAPISetConsoleIds_t CCAPISetConsoleIds;
        public CCAPISetMemory_t CCAPISetMemory;
        public CCAPIGetMemory_t CCAPIGetMemory;
        public CCAPIGetProcessList_t CCAPIGetProcessList;
        public CCAPIGetProcessName_t CCAPIGetProcessName;
        public CCAPIGetTemperature_t CCAPIGetTemperature;
        public CCAPIShutdown_t CCAPIShutdown;
        public CCAPIRingBuzzer_t CCAPIRingBuzzer;
        public CCAPISetConsoleLed_t CCAPISetConsoleLed;
        public CCAPIGetFirmwareInfo_t CCAPIGetFirmwareInfo;
        public CCAPIVshNotify_t CCAPIVshNotify;
        public CCAPIGetNumberOfConsoles_t CCAPIGetNumberOfConsoles;
        public CCAPIGetConsoleInfo_t CCAPIGetConsoleInfo;
        public CCAPIGetDllVersion_t CCAPIGetDllVersion;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIConnectConsole_t(string ip);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIDisconnectConsole_t();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIGetConnectionStatus_t(ref int status);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPISetBootConsoleIds_t(int idType, int on, IntPtr pId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPISetConsoleIds_t(int idType, IntPtr pId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPISetMemory_t(uint pid, ulong addr, uint size, IntPtr pData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIGetMemory_t(uint pid, ulong addr, uint size, IntPtr pData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIGetProcessList_t(ref uint npid, IntPtr pids);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIGetProcessName_t(uint pid, IntPtr name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIGetTemperature_t(ref int cell, ref int rsx);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIShutdown_t(int mode);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIRingBuzzer_t(int type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPISetConsoleLed_t(int color, int status);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIGetFirmwareInfo_t(ref int firmware, ref int ccapiVersion, ref int consoleType);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIVshNotify_t(int mode, string msg);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIGetNumberOfConsoles_t();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIGetConsoleInfo_t(int index, IntPtr name, IntPtr ip);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CCAPIGetDllVersion_t();
}