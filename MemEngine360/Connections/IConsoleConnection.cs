// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using System.Net;
using System.Net.Sockets;
using System.Numerics;
using MemEngine360.Connections.Impl;
using MemEngine360.Connections.Impl.Threads;
using ConsoleColor = MemEngine360.Connections.Impl.ConsoleColor;

namespace MemEngine360.Connections;

public interface IConsoleConnection : IDisposable {
    /// <summary>
    /// Returns whether the underlying connection is valid. E.g. for TCP, returns <see cref="TcpClient.Connected"/>
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// Returns true when a read or write operation is currently in progress
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// Gets all the threads running on this console
    /// </summary>
    /// <returns></returns>
    Task<List<ConsoleThread>> GetThreadDump();
    
    /// <summary>
    /// Sends the eject command to toggle the disk tray
    /// </summary>
    Task OpenDiskTray();

    /// <summary>
    /// Sends the cold reboot command to restart the console
    /// </summary>
    /// <param name="cold">True to fully reboot console, False to only reboot title</param>
    Task RebootConsole(bool cold = true);
    
    /// <summary>
    /// Sends the shutdown command to the console
    /// </summary>
    Task ShutdownConsole();
    
    /// <summary>
    /// Signals the console to completely freeze
    /// </summary>
    Task DebugFreeze();
    
    /// <summary>
    /// Signals the console to unfreeze/resume
    /// </summary>
    /// <returns></returns>
    Task DebugUnFreeze();
    
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
    /// Walks all the memory regions on the console
    /// </summary>
    /// <returns>A task containing a list of all memory regions</returns>
    Task<List<MemoryRegion>> GetMemoryRegions();
    
    /// <summary>
    /// Gets the current state of the console
    /// </summary>
    Task<ExecutionState> GetExecutionState();
    
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

    /// <summary>
    /// Reads an exact amount of bytes from the console. If the address space
    /// contains protected memory, the buffer will have 0s written into it
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <param name="buffer">The destination buffer</param>
    /// <param name="offset">The offset to start writing into the buffer</param>
    /// <param name="count">The amount of bytes to read from the console</param>
    /// <returns>A task representing the read operation</returns>
    Task<int> ReadBytes(uint address, byte[] buffer, int offset, uint count);

    /// <summary>
    /// Convenience method for reading an array of bytes. Calls <see cref="ReadBytes(uint,byte[],int,uint)"/>
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <param name="count">The amount of bytes to read from the console</param>
    /// <returns>A task representing the read operation</returns>
    Task<byte[]> ReadBytes(uint address, uint count);
    
    /// <summary>
    /// Reads a single byte from the console
    /// </summary>
    /// <param name="address">The address to read from</param>
    Task<byte> ReadByte(uint Offset);
    
    /// <summary>
    /// Reads a boolean from the console. Same as reading a single byte and checking it's not equal to 0
    /// </summary>
    /// <param name="address">The address to read from</param>
    Task<bool> ReadBool(uint address);
    
    /// <summary>
    /// Reads a single byte as a character from the console (ASCII char)
    /// </summary>
    /// <param name="address">The address to read from</param>
    Task<char> ReadChar(uint address);
    
    /// <summary>
    /// Reads a value from the console's memory. This method corrects the
    /// endianness (as in, the bytes are flipped when this computer is LE).
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <typeparam name="T">The type of value to read, e.g. <see cref="int"/></typeparam>
    /// <returns>A Task that produces the value</returns>
    Task<T> ReadValue<T>(uint address) where T : unmanaged;
    
    /// <summary>
    /// Reads a struct from the console's memory. This method
    /// corrects the endianness for each field in the struct
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <param name="fields">
    /// The field sizes. Alignment must be done manually, therefore, the layout of the struct
    /// being read must be known, and therefore, the summation of all integers in this array
    /// should equal or almost equal <c>sizeof(T) (might be less due to alignment)</c>
    /// </param>
    /// <typeparam name="T">The type of value to read, e.g. <see cref="Vector3"/></typeparam>
    Task<T> ReadStruct<T>(uint address, params int[] fields) where T : unmanaged;
    
    /// <summary>
    /// Reads the given number of single byte characters from the console (ASCII chars)
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <param name="count">The number of bytes to read</param>
    /// <param name="removeNull">Removes null characters</param>
    Task<string> ReadString(uint address, uint count, bool removeNull = true);

    /// <summary>
    /// Writes the exact number of bytes to the console
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="bytes">The buffer to write</param>
    Task WriteBytes(uint address, byte[] bytes);

    /// <summary>
    /// Writes a single value to the console
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="value">The value</param>
    Task WriteByte(uint address, byte value);
    
    /// <summary>
    /// Writes a boolean value to the console (same as writing a single byte with a value of 1 or 0)
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="value">The boolean value</param>
    Task WriteBool(uint address, bool value);
    
    /// <summary>
    /// Writes a character as a byte value to the console (ASCII char)
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="value">The char value</param>
    Task WriteChar(uint address, char value);
    
    /// <summary>
    /// Writes a value to the console's memory. This method corrects the
    /// endianness (as in, the bytes are flipped when this computer is LE)
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="value">The value to write</param>
    /// <typeparam name="T">The type of value to write, e.g. <see cref="int"/></typeparam>
    Task WriteValue<T>(uint address, T value) where T : unmanaged;

    /// <summary>
    /// Writes a struct to the console's memory. This method
    /// corrects the endianness for each field in the struct
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="value">The value to write</param>
    /// <param name="fields">
    /// The field sizes. Alignment must be done manually, therefore, the layout of the struct
    /// being read must be known, and therefore, the summation of all integers in this array
    /// should equal or almost equal <c>sizeof(T) (might be less due to alignment)</c>
    /// </param>
    /// <typeparam name="T">The type of value to write, e.g. <see cref="Vector3"/></typeparam>
    Task WriteStruct<T>(uint address, T value, params int[] fields) where T : unmanaged;
    
    /// <summary>
    /// Writes the string's characters to the console (ASCII chars)
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="value">The string value to write</param>
    Task WriteString(uint address, string value);

    /// <summary>
    /// Writes the file contents to the console
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="filePath">The source file path on this computer</param>
    Task WriteFile(uint address, string filePath);
    
    Task WriteHook(uint address, uint destination, bool isLinked);
    
    Task WriteNOP(uint address);
}