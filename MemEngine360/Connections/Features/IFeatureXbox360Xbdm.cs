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

namespace MemEngine360.Connections.Features;

/// <summary>
/// A feature that captures everything provided by the XBDM connection.
/// Note that this may include things that the XDevkit connection does not, and vice-versa.
/// </summary>
public interface IFeatureXbox360Xbdm :
    IConsoleFeature,
    IFeatureXboxThreads,
    IFeatureMemoryRegions,
    IFeaturePowerFunctions,
    IFeatureDiskEjection,
    IFeatureIceCubesEx,
    IFeatureFileSystemInfo, 
    IFeatureXboxExecutionState {

    /// <summary>
    /// Gets the console's ID
    /// </summary>
    /// <returns></returns>
    Task<string> GetConsoleID();

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
    /// Gets the hardware information
    /// </summary>
    Task<XboxHardwareInfo> GetHardwareInfo();

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

/// <summary>
/// Information about the console
/// </summary>
public struct XboxHardwareInfo {
    public uint Flags;
    public byte NumberOfProcessors, PCIBridgeRevisionID;
    public byte[] ReservedBytes;
    public ushort BldrMagic, BldrFlags;
}

/// <summary>
/// The state of the xbox 360
/// </summary>
public enum XboxExecutionState {
    Stop,
    Start,
    Reboot,
    Pending,
    TitleReboot,
    TitlePending,
    Unknown
}

/// <summary>
/// The colour of the console in xbox 360 neighbourhood
/// </summary>
public enum ConsoleColor {
    Black,
    Blue,
    BlueGray,
    NoSideCar,
    White,
}