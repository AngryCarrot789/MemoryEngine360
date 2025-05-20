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

using System.Net.Sockets;
using System.Numerics;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Connections;

/// <summary>
/// Represents a connection to a console
/// </summary>
public interface IConsoleConnection {
    /// <summary>
    /// Gets the console type for this connection
    /// </summary>
    RegisteredConsoleType ConsoleType { get; }

    /// <summary>
    /// Returns whether the underlying connection is logically connected (as in <see cref="Close"/> has not been called)
    /// and if the underlying connection is still valid. E.g. for TCP, returns <see cref="TcpClient.Connected"/>
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Returns true when <see cref="Close"/> is invoked or some other internal method caused the connection to close 
    /// </summary>
    bool IsClosed { get; }

    /// <summary>
    /// Returns true when this console's byte order is little-endian. Xbox 360 is big endian, so this returns false 
    /// </summary>
    bool IsLittleEndian { get; }

    /// <summary>
    /// Reads an exact amount of bytes from the console. If the address space
    /// contains protected memory, the buffer will have 0s written into it
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <param name="buffer">The destination buffer</param>
    /// <param name="offset">The offset to start writing into the buffer</param>
    /// <param name="count">The amount of bytes to read from the console</param>
    /// <returns>A task representing the read operation. It contains the amount of bytes actually read</returns>
    Task<uint> ReadBytes(uint address, byte[] buffer, int offset, uint count);

    /// <summary>
    /// Reads an exact amount of bytes from the console, in chunks. By reading in
    /// smaller chunks, we can safely support cancellation. If the address space
    /// contains protected memory, the buffer will have 0s written into it
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <param name="buffer">The destination buffer</param>
    /// <param name="offset">The offset to start writing into the buffer</param>
    /// <param name="count">The total amount of bytes to read from the console</param>
    /// <param name="chunkSize">The amount of bytes to read per chunk</param>
    /// <param name="completion">Optional feedback for how much progress has been done</param>
    /// <param name="cancellationToken">A token which can request cancellation for this operation</param>
    /// <returns>A task representing the read operation</returns>
    Task ReadBytes(uint address, byte[] buffer, int offset, uint count, uint chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default);

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
    Task<byte> ReadByte(uint address);
    
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
    /// <param name="buffer">The buffer to write</param>
    Task WriteBytes(uint address, byte[] buffer);

    /// <summary>
    /// Writes the exact number of bytes to the console, with support for cancelling. If cancellation is requested,
    /// the connection is not left in an invalid state, but not all the bytes in the buffer may have been written
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="buffer">The buffer to write</param>
    /// <param name="offset">The offset, within <see cref="buffer"/>, to start at</param>
    /// <param name="count">The amount of bytes to write</param>
    /// <param name="chunkSize">The amount of bytes to write per chunk</param>
    /// <param name="completion">Optional feedback for the completion progress</param>
    /// <param name="cancellationToken">Used to cancel the write operation</param>
    Task WriteBytes(uint address, byte[] buffer, int offset, uint count, uint chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default);    

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
    /// Writes a value to the console's memory. This method corrects the endianness
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
    /// Closes the console connection
    /// </summary>
    void Close();
}