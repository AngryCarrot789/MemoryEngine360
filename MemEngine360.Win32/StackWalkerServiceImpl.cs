// 
// Copyright (c) 2025-2025 REghZy
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

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using MemEngine360.Connections;
using MemEngine360.Engine.Debugging;
using MemEngine360.XboxBase;
using MemEngine360.XboxBase.Modules;
using Vanara.PInvoke;
using static Vanara.PInvoke.DbgHelp;

namespace MemEngine360.Win32;

public class StackWalkerServiceImpl : IStackWalker {
    public unsafe void Walk_NotReadyYet(IConsoleConnection connection, ThreadContext context) {
        Task.Run(() => this.Action(connection, context));
    }

    private struct CtxObj {
        public IConsoleConnection Connection;
        public IntPtr RTFUNCInfo;
    }
    
    [SuppressMessage(
        "Interoperability",
        "CA1416:Validate platform compatibility",
        Justification = $"This class is only accessible when {nameof(PluginMemEngineWin32Lib)} is loaded by MemoryEngine and only when {nameof(OperatingSystem)}.{nameof(OperatingSystem.IsWindows)}() returns true")]
    private unsafe void Action(IConsoleConnection connection, ThreadContext context) {
        // PowerPC Big-Endian, i assume
        IMAGE_FILE_MACHINE machine = (IMAGE_FILE_MACHINE) 498;
        STACKFRAME64 frame = new STACKFRAME64();
        frame.AddrPC = new ADDRESS64() { Offset = context.IAR, Mode = ADDRESS_MODE.AddrModeFlat };
        frame.AddrReturn = new ADDRESS64() { Offset = context.LR, Mode = ADDRESS_MODE.AddrModeFlat };
        frame.AddrFrame = new ADDRESS64() { Offset = context.GPR1, Mode = ADDRESS_MODE.AddrModeFlat };
        frame.AddrStack = new ADDRESS64() { Offset = context.GPR1, Mode = ADDRESS_MODE.AddrModeFlat };

        // Should point to the thread context. However is it
        // expecting PowerPC thread context? I assume so...
        // IntPtr hContextRecord = IntPtr.Zero;

        CtxObj ctxObj = new CtxObj {
            Connection = connection,
            RTFUNCInfo = Marshal.AllocCoTaskMem(16)
        };

        bool success = StackWalk64(machine,
            (IntPtr) (&ctxObj),
            HTHREAD.NULL,
            ref frame,
            (IntPtr) (&context),
            this.ReadMemoryRoutine,
            this.FunctionTableAccessRoutine,
            this.GetModuleBaseRoutine);
        Marshal.FreeCoTaskMem(ctxObj.RTFUNCInfo);
    }

    private unsafe bool ReadMemoryRoutine(HPROCESS hProcess, ulong lpBaseAddress, IntPtr lpBuffer, uint nSize, out uint lpNumberOfBytesRead) {
        CtxObj connection = *(CtxObj*) (IntPtr) hProcess;

        byte[] buffer = new byte[nSize];
        connection.Connection.ReadBytes((uint) lpBaseAddress, buffer, 0, buffer.Length).GetAwaiter().GetResult();
        for (int i = 0; i < nSize; i++) {
            Marshal.WriteByte(lpBuffer, i, buffer[i]);
        }

        lpNumberOfBytesRead = nSize;
        return false;
    }

    private unsafe IntPtr FunctionTableAccessRoutine(HPROCESS ahProcess, ulong addrBase) {
        CtxObj connection = *(CtxObj*) (IntPtr) ahProcess;
        FunctionCallEntry? function = ((IHaveXboxDebugFeatures) connection.Connection).FindFunctions([(uint) addrBase]).GetAwaiter().GetResult()[0];
        if (function == null) {
            return IntPtr.Zero;
        }
        
        Marshal.WriteInt32(connection.RTFUNCInfo, 0, (int) function.Address);
        Marshal.WriteInt32(connection.RTFUNCInfo, 4, (int) (function.Address + function.Size));
        Marshal.WriteInt64(connection.RTFUNCInfo, 8, (long) function.UnwindInfo);
        
        return connection.RTFUNCInfo;
    }

    private unsafe ulong GetModuleBaseRoutine(HPROCESS hProcess, ulong address) {
        CtxObj connection = *(CtxObj*) (IntPtr) hProcess;
        ConsoleModule? module = ((IHaveXboxDebugFeatures) connection.Connection).GetModuleForAddress((uint) address, false).GetAwaiter().GetResult();
        return module?.BaseAddress ?? 0;
    }
}