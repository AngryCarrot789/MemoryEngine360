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

using MemEngine360.Engine.Debugging;
using MemEngine360.XboxBase;
using MemEngine360.XboxBase.Modules;

namespace MemEngine360.Connections.Features;

/// <summary>
/// A trait for an xbox debuggable console. For now, only xbox is supported. But soon we will move to an abstract debugger to support multiple platforms
/// </summary>
public interface IFeatureXboxDebugging : IConsoleFeature, IFeatureXboxThreads, IFeatureIceCubesEx, IFeatureXboxExecutionState {
    Task AddBreakpoint(uint address);

    Task RemoveBreakpoint(uint address);
    
    Task SetDataBreakpoint(uint address, XboxBreakpointType type, uint size);

    /// <summary>
    /// Reads the registers of the current thread. Returns null if the thread doesn't exist
    /// </summary>
    /// <param name="threadId"></param>
    Task<RegisterContext?> GetThreadRegisters(uint threadId);

    Task SuspendThread(uint threadId);

    Task ResumeThread(uint threadId);

    Task StepThread(uint threadId);

    // TODO: StackWalk64

    /// <summary>
    /// Tries to find functions using the given instruction address
    /// </summary>
    Task<FunctionCallEntry?[]> FindFunctions(uint[] iar);

    Task<ConsoleModule?> GetModuleForAddress(uint address, bool needSections);
}

public readonly struct Vector128(ulong lowPart, ulong highPart) : IEquatable<Vector128> {
    public readonly ulong LowPart = lowPart, HighPart = highPart;

    public bool Equals(Vector128 other) {
        return this.LowPart == other.LowPart && this.HighPart == other.HighPart;
    }

    public override bool Equals(object? obj) {
        return obj is Vector128 other && this.Equals(other);
    }

    public override int GetHashCode() {
        return HashCode.Combine(this.LowPart, this.HighPart);
    }

    public override string ToString() {
        return $"0x{this.LowPart:X16}{this.HighPart:X16}"; // probably right order?
    }
}

/*
     My guess for XBOX 360
     
     
     Format:
         [Index] Register Name (<Register Type> <Register Size or Data Type Size if Register Size is obvious>)
     * Special Purpose Registers:
         [  0]  MSR       (I 32 bit) (Machine State Register)
         [  1]  IAR       (I 32 bit) (Instruction Address Register)
         [  2]  LR        (I 32 bit) (Link Register)
         [  3]  CR        (I 32 bit) (Condition Register)
         [  4]  XER       (I 32 bit) (Exception Register)
         [  0]  CTR       (I 64 bit) (Count Register)
         [ 32]  fpscr     (D 64 bit) (Floating Point Status and Control Register)
         [128]  vscr      (V 32 bit) (Vector Status and Control Register)
     * General Purpose Registers:
         [1-32]  R0 to 31  (I 64 bit)
     * Floating Point Registers:
         [0-31]  FP0 to 31 (D 64 bit)
     * Vector Point Registers: (SIMD) (Apparently only 32 can be accessed at once)
         [0-127] V0 to 127 (V 128 bit)
     */

    /*
     GETCONTEXT THREAD=0xfb000024 CONTROL INT FP VR
     (apparently VR does nothing)
     * Control Registers:
           MSR    FFFFFFFF (32 bit)
           IAR    80075360 (32 bit)
           LR     80072ff0 (32 bit)
           CTR    FFFFFFFFFFFFFFFF (64 bit)
     * INT:
           Gpr0   FFFFFFFFFFFFFFFF (64 bit)
           Gpr1   000000007a07fc10 (64 bit)
           Gpr2   FFFFFFFFFFFFFFFF (64 bit)
           Gpr3   FFFFFFFFFFFFFFFF (64 bit)
           Gpr4   FFFFFFFFFFFFFFFF (64 bit)
           Gpr5   FFFFFFFFFFFFFFFF (64 bit)
           Gpr6   FFFFFFFFFFFFFFFF (64 bit)
           Gpr7   FFFFFFFFFFFFFFFF (64 bit)
           Gpr8   FFFFFFFFFFFFFFFF (64 bit)
           Gpr9   FFFFFFFFFFFFFFFF (64 bit)
           Gpr10  FFFFFFFFFFFFFFFF (64 bit)
           Gpr11  FFFFFFFFFFFFFFFF (64 bit)
           Gpr12  FFFFFFFFFFFFFFFF (64 bit)
           Gpr13  FFFFFFFFFFFFFFFF (64 bit)
           Gpr14  0000000000000003 (64 bit)
           Gpr15  0000000000000000 (64 bit)
           Gpr16  FFFFFFFF80000000 (64 bit)
           Gpr17  FFFFFFFF801b0000 (64 bit)
           Gpr18  0000000000000000 (64 bit)
           Gpr19  0000000000000001 (64 bit)
           Gpr20  0000000000000000 (64 bit)
           Gpr21  0000000000000001 (64 bit)
           Gpr22  0000000000000001 (64 bit)
           Gpr23  000000007a07fe70 (64 bit)
           Gpr24  0000000000000000 (64 bit)
           Gpr25  0000000000000001 (64 bit)
           Gpr26  0000000000000001 (64 bit)
           Gpr27  000000007a07fe54 (64 bit)
           Gpr28  000000007a07fe70 (64 bit)
           Gpr29  0000000000000000 (64 bit)
           Gpr30  000000007a07fe9c (64 bit)
           Gpr31  000000003a0d4020 (64 bit)
           CR     24000222 (32 bit)
           XER    FFFFFFFF (32 bit)
     * 
     */

public class RegisterContext {
    internal readonly Dictionary<string, uint> regU32 = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
    internal readonly Dictionary<string, ulong> regU64 = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
    internal readonly Dictionary<string, Vector128> regV128 = new Dictionary<string, Vector128>(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<KeyValuePair<string, uint>> UInt32Registers => this.regU32;
    public IEnumerable<KeyValuePair<string, ulong>> UInt64Registers => this.regU64;
    public IEnumerable<KeyValuePair<string, Vector128>> Vector128Registers => this.regV128;

    public RegisterContext() {
    }

    public void SetUInt32(string key, uint value) => this.regU32[key] = value;

    public void SetUInt64(string key, ulong value) => this.regU64[key] = value;

    public void SetVector(string key, Vector128 value) => this.regV128[key] = value;

    public bool TryGetUInt32(string key, out uint value) {
        return this.regU32.TryGetValue(key, out value);
    }

    public bool TryGetUInt64(string key, out ulong value) {
        return this.regU64.TryGetValue(key, out value);
    }

    public bool TryGetVector(string key, out Vector128 value) {
        return this.regV128.TryGetValue(key, out value);
    }
}