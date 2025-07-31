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
using Vanara.PInvoke;

namespace MemEngine360.Win32;

public class StackWalkerServiceImpl {
    [SuppressMessage(
        "Interoperability", 
        "CA1416:Validate platform compatibility", 
        Justification = $"This class is only accessible when {nameof(PluginMemEngineWin32Lib)} is loaded by MemoryEngine and only when {nameof(OperatingSystem)}.{nameof(OperatingSystem.IsWindows)}() returns true")]
    public void Walk() {
        
        // PowerPC Big-Endian, i assume
        DbgHelp.IMAGE_FILE_MACHINE machine = (DbgHelp.IMAGE_FILE_MACHINE) 498;
        DbgHelp.STACKFRAME64 frame = new DbgHelp.STACKFRAME64();
        
        // Should point to the thread context. However is it
        // expecting PowerPC thread context? I assume so...
        IntPtr hContextRecord = IntPtr.Zero;

        Kernel32.CONTEXT64 e;
        
        bool success = DbgHelp.StackWalk64(
            machine,
            HPROCESS.NULL, 
            HTHREAD.NULL,
            ref frame,
            hContextRecord,
            this.ReadMemoryRoutine,
            this.FunctionTableAccessRoutine,
            this.GetModuleBaseRoutine);
    }

    private bool ReadMemoryRoutine(HPROCESS hprocess, ulong lpBaseAddress, IntPtr lpBuffer, uint nSize, out uint lpNumberOfBytesRead) {
        lpNumberOfBytesRead = 0;
        return false;
    }

    private IntPtr FunctionTableAccessRoutine(HPROCESS ahProcess, ulong addrBase) {
        return IntPtr.Zero;
    }

    private ulong GetModuleBaseRoutine(HPROCESS hProcess, ulong address) {
        return 0;
    }
}