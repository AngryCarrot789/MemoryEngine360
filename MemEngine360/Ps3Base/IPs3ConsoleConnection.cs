// 
// Copyright (c) 2026-2026 REghZy
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

using MemEngine360.Connections;

namespace MemEngine360.Ps3Base;

/// <summary>
/// Represents a connection to a PS3
/// </summary>
public interface IPs3ConsoleConnection : INetworkConsoleConnection {
    /// <summary>
    /// Gets or sets the process we use to read and write memory 
    /// </summary>
    Ps3Process AttachedProcess { get; set; }
    
    /// <summary>
    /// Raised when <see cref="AttachedProcess"/> changes
    /// </summary>
    event EventHandler? AttachedProcessChanged;
    
    /// <summary>
    /// Tries to get the name of a process by its pid
    /// </summary>
    /// <returns>The name, or null if the process didn't exist or has no name</returns>
    Task<string?> GetProcessName(uint processId);
    
    /// <summary>
    /// Tries to find the most applicable process id for a game
    /// </summary>
    /// <returns>The game pid, or 0, if a game couldn't be found</returns>
    Task<Ps3Process> FindGameProcessId();
    
    /// <summary>
    /// Returns an array of processes with their process name
    /// </summary>
    Task<Ps3Process[]> GetAllProcessesWithName();
    
    /// <summary>
    /// Returns an array of process ids
    /// </summary>
    Task<uint[]> GetAllProcesses();
}