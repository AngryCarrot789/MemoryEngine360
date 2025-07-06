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

using System.Net;
using MemEngine360.Connections;
using MemEngine360.Connections.Traits;
using MemEngine360.Connections.Utils;

namespace MemEngine360.Xbox360XBDM.Consoles.Xbdm;

/// <summary>
/// Represents a connection to an xbox console
/// </summary>
public interface IXbdmConnection : INetworkConsoleConnection, IHaveMemoryRegions, IHaveIceCubes {
    /// <summary>
    /// Gets all the threads running on this console
    /// </summary>
    /// <returns></returns>
    Task<List<XboxThread>> GetThreadDump();

    /// <summary>
    /// Sends the eject command to toggle the disk tray
    /// </summary>
    Task OpenDiskTray();

    /// <summary>
    /// Deletes a file on the console
    /// </summary>
    /// <param name="path">The file path</param>
    Task DeleteFile(string path);

    /// <summary>
    /// Launches an executable file, e.g. an XEX
    /// </summary>
    /// <param name="path"></param>
    Task LaunchFile(string path);

    /// <summary>
    /// Gets the console's ID
    /// </summary>
    /// <returns></returns>
    Task<string> GetConsoleID();

    /// <summary>
    /// Gets the console's CPU key
    /// </summary>
    Task<string> GetCPUKey();

    /// <summary>
    /// Gets the console's debugging name, typically the name displayed in xbox neighbourhood
    /// </summary>
    Task<string> GetDebugName();

    /// <summary>
    /// Gets the path of the executable's name
    /// </summary>
    /// <param name="executable">The executable. Null to use current executable</param>
    /// <returns>The .xex file path</returns>
    Task<string?> GetXbeInfo(string? executable);

    /// <summary>
    /// Gets the current state of the console
    /// </summary>
    Task<XbdmExecutionState> GetExecutionState();

    /// <summary>
    /// Gets the hardware information
    /// </summary>
    Task<HardwareInfo> GetHardwareInfo();

    /// <summary>
    /// Gets something
    /// </summary>
    Task<uint> GetProcessID();

    /// <summary>
    /// Gets the 'alt address' of the xbox, typically the IP address as a uint
    /// </summary>
    Task<IPAddress> GetTitleIPAddress();

    /// <summary>
    /// Sets the console colour property for use in xbox neighbourhood
    /// </summary>
    /// <param name="colour">The new colour</param>
    Task SetConsoleColor(ConsoleColor colour);

    /// <summary>
    /// Sets the console's debug name, typically the name displayed in xbox neighbourhood
    /// </summary>
    /// <param name="newName">The new debug name</param>
    Task SetDebugName(string newName);
}