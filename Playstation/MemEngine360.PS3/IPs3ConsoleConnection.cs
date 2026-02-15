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

namespace MemEngine360.PS3;

public interface IPs3ConsoleConnection : INetworkConsoleConnection {
    /// <summary>
    /// Gets or sets the process we use to read and write memory 
    /// </summary>
    uint AttachedProcess { get; set; }
    
    /// <summary>
    /// Raised when <see cref="AttachedProcess"/> changes
    /// </summary>
    event EventHandler? AttachedProcessChanged;
    
    Task<uint> FindGameProcessId();
    
    Task<List<(uint, string?)>> GetAllProcessesWithName();
    
    Task<uint[]> GetAllProcesses();
}