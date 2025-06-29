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

using System.Net.Sockets;
using System.Numerics;
using System.Text;
using MemEngine360.Engine.Addressing;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Connections;

/// <summary>
/// Represents a connection to a console. Reading/writing operations may
/// throw <see cref="IOException"/> or <see cref="TimeoutException"/> at any time.
/// However, timeouts are the most likely (e.g. maybe the user unplugged the power cord midway through a read operation)
/// </summary>
public interface IConsoleConnection {
    /// <summary>
    /// Gets the console type for this connection
    /// </summary>
    RegisteredConnectionType ConnectionType { get; }

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
    /// Reads an exact amount of bytes from the console. If the address space contains protected memory, the buffer will
    /// have 0s written into it. If for some reason the amount of bytes count not be read, an <see cref="IOException"/> is thrown
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <param name="buffer">The destination buffer</param>
    /// <param name="offset">The offset to start writing into the buffer</param>
    /// <param name="count">The amount of bytes to read from the console</param>
    /// <returns>A task representing the read operation. It contains the amount of bytes actually read</returns>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not read all bytes or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while reading bytes</exception>
    Task ReadBytes(uint address, byte[] buffer, int offset, int count);

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
    /// <exception cref="IOException">An IO exception occurred, e.g. could not read all bytes or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while reading bytes</exception>
    Task ReadBytes(uint address, byte[] buffer, int offset, int count, uint chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience method for reading an array of bytes. Calls <see cref="ReadBytes(uint,byte[],int,uint)"/>
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <param name="count">The amount of bytes to read from the console</param>
    /// <returns>A task representing the read operation</returns>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not read all bytes or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while reading bytes</exception>
    Task<byte[]> ReadBytes(uint address, int count);
    
    /// <summary>
    /// Reads a single byte from the console
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not read byte or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while reading byte</exception>
    Task<byte> ReadByte(uint address);
    
    /// <summary>
    /// Reads a boolean from the console. Same as reading a single byte and checking it's not equal to 0
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not read byte or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while reading byte</exception>
    Task<bool> ReadBool(uint address);
    
    /// <summary>
    /// Reads a single byte as a character from the console (ASCII char)
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not read char or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while reading char</exception>
    Task<char> ReadChar(uint address);
    
    /// <summary>
    /// Reads a value from the console's memory. This method corrects the
    /// endianness (as in, the bytes are flipped when this computer is LE).
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <typeparam name="T">The type of value to read, e.g. <see cref="int"/></typeparam>
    /// <returns>A Task that produces the value</returns>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not read all bytes or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while reading bytes</exception>
    Task<T> ReadValue<T>(uint address) where T : unmanaged;
    
    /// <summary>
    /// Reads a struct from the console's memory. This method
    /// corrects the endianness for each field in the struct
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <param name="fields">
    /// The field sizes. Alignment must be done manually, therefore, the layout of the struct
    /// being read must be known, and therefore, the summation of all integers in this array
    /// should equal or almost equal <c>sizeof(T)</c> (might be less due to alignment)
    /// </param>
    /// <typeparam name="T">The type of value to read, e.g. <see cref="Vector3"/></typeparam>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not read all bytes or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while reading bytes</exception>
    Task<T> ReadStruct<T>(uint address, params int[] fields) where T : unmanaged;

    /// <summary>
    /// Reads the given number of single byte characters from the console (ASCII chars)
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <param name="count">The number of chars to read</param>
    /// <param name="removeNull">Removes null characters</param>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not read all bytes or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while reading bytes</exception>
    Task<string> ReadStringASCII(uint address, int count, bool removeNull = true);

    /// <summary>
    /// Reads the given number of single byte characters from the console (ASCII chars)
    /// </summary>
    /// <param name="address">The address to read from</param>
    /// <param name="count">The number of chars to read</param>
    /// <param name="encoding">The encoding to use to read chars</param>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not read all bytes or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while reading bytes</exception>
    Task<string> ReadString(uint address, int count, Encoding encoding);

    /// <summary>
    /// Writes the exact number of bytes to the console. If for some reason the amount of bytes count not be written, an <see cref="IOException"/> is thrown
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="buffer">The buffer to write</param>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not write all bytes or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while writing bytes</exception>
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
    /// <exception cref="IOException">An IO exception occurred, e.g. could not write all bytes or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while writing bytes</exception>
    Task WriteBytes(uint address, byte[] buffer, int offset, int count, uint chunkSize, CompletionState? completion = null, CancellationToken cancellationToken = default);    

    /// <summary>
    /// Writes a single value to the console
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="value">The value</param>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not write byte or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while writing byte</exception>
    Task WriteByte(uint address, byte value);
    
    /// <summary>
    /// Writes a boolean value to the console (same as writing a single byte with a value of 1 or 0)
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="value">The boolean value</param>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not write byte or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while writing byte</exception>
    Task WriteBool(uint address, bool value);
    
    /// <summary>
    /// Writes a character as a byte value to the console (ASCII char)
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="value">The char value</param>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not write char or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while writing char</exception>
    Task WriteChar(uint address, char value);
    
    /// <summary>
    /// Writes a value to the console's memory. This method corrects the endianness
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="value">The value to write</param>
    /// <typeparam name="T">The type of value to write, e.g. <see cref="int"/></typeparam>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not write all bytes or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while writing bytes</exception>
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
    /// should equal or almost equal <c>sizeof(T)</c> (might be less due to alignment)
    /// </param>
    /// <typeparam name="T">The type of value to write, e.g. <see cref="Vector3"/></typeparam>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not write all bytes or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while writing bytes</exception>
    Task WriteStruct<T>(uint address, T value, params int[] fields) where T : unmanaged;
    
    /// <summary>
    /// Writes the string's characters to the console (ASCII chars)
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="value">The string value to write</param>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not write all bytes or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while writing bytes</exception>
    Task WriteString(uint address, string value);
    
    /// <summary>
    /// Writes the string's characters to the console (ASCII chars)
    /// </summary>
    /// <param name="address">The address to write to</param>
    /// <param name="value">The string value to write</param>
    /// <param name="encoding">The encoding to use to write the string</param>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not write all bytes or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while writing bytes</exception>
    Task WriteString(uint address, string value, Encoding encoding);

    // /// <summary>
    // /// Finds a memory pattern in the console
    // /// </summary>
    // /// <param name="address">The start address to begin scanning</param>
    // /// <param name="count">The amount of bytes to process</param>
    // /// <param name="pattern">The pattern</param>
    // /// <param name="completion">Completion progress</param>
    // /// <param name="cancellationToken">Used to notify cancellation of the operation</param>
    // /// <returns>A task containing the found memory address </returns>
    // /// <exception cref="IOException">An IO exception occurred, e.g. could not read from console or network error occurred</exception>
    // /// <exception cref="TimeoutException">Timed out while reading from console</exception>
    // Task<uint?> FindPattern(uint address, uint count, MemoryPattern pattern, CompletionState? completion = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to resolve a dynamic address
    /// </summary>
    /// <param name="address">The address to resolve</param>
    /// <returns>The address of the value the address points to, or null, if a null pointer or invalid value was read from the console</returns>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not read from console or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while reading from console</exception>
    Task<uint?> ResolvePointer(DynamicAddress address);
    
    /// <summary>
    /// Figures out if the memory region is either unallocated or intersects a protected region of memory at all,
    /// even if it's a single byte. Returns false when <see cref="count"/> is 0
    /// <para>
    /// When this returns null, the only thing you can really do is assume unprotected using this code:
    /// <code>
    /// <![CDATA[
    /// bool isProtected = (await connection.IsMemoryProtected(addr, cbData)) ?? false
    /// ]]>
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="address">The address to start the search</param>
    /// <param name="count">The number of bytes to search</param>
    /// <returns>
    /// True when protected, false when not protected, null when cannot be determined (e.g. this method is
    /// unsupported), in which case you can just assume the memory is not protected
    /// </returns>
    /// <exception cref="IOException">An IO exception occurred, e.g. could not read from console or network error occurred</exception>
    /// <exception cref="TimeoutException">Timed out while reading from console</exception>
    Task<bool?> IsMemoryInvalidOrProtected(uint address, uint count);
    
    /// <summary>
    /// Closes the console connection
    /// </summary>
    Task Close();
}