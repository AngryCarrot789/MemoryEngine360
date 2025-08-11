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
public interface IFeatureXboxDebugging : IConsoleFeature, IFeatureXboxThreads, IFeatureIceCubes, IFeatureXboxExecutionState {
    Task AddBreakpoint(uint address);
    
    Task AddDataBreakpoint(uint address, XboxBreakpointType type, uint size);
    
    Task RemoveBreakpoint(uint address);
    
    Task RemoveDataBreakpoint(uint address, XboxBreakpointType type, uint size);

    /// <summary>
    /// Reads the general purpose registers. Returns null if the thread doesn't exist
    /// </summary>
    /// <param name="threadId"></param>
    Task<List<RegisterEntry>?> GetRegisters(uint threadId);

    /// <summary>
    /// Reads the register value
    /// </summary>
    /// <param name="threadId"></param>
    /// <param name="registerName"></param>
    /// <returns>The value, or null, if the thread or register doesn't exist</returns>
    Task<RegisterEntry?> ReadRegisterValue(uint threadId, string registerName);

    Task SuspendThread(uint threadId);
    
    Task ResumeThread(uint threadId);
    
    Task StepThread(uint threadId);

    // TODO: StackWalk64
    
    /// <summary>
    /// Tries to find functions using the given instruction address
    /// </summary>
    Task<FunctionCallEntry?[]> FindFunctions(uint[] iar);

    Task<ConsoleModule?> GetModuleForAddress(uint address, bool bNeedSections);
}