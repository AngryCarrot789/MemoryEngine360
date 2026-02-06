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

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MemEngine360.Connections;
using MemEngine360.Engine.Modes;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Ranges;

namespace MemEngine360.Engine;

public static class FragmentedReadHelper {
    public static IDataValue ReadDataValueFromBuffer(FragmentedMemoryBuffer buffer, IntegerRange<uint> range, DataType dataType, StringType stringType, int strlen, int arrlen, bool isLittleEndian) {
        using (ArrayPools.RentSpan((int) range.Length, out Span<byte> data)) {
            int readCount = buffer.Read(range.Start, data);
            Debug.Assert(readCount == range.Length, "Not enough bytes in fragment buffer. This should not be possible");

            switch (dataType) {
                case DataType.Byte: {
                    Debug.Assert(sizeof(byte) == range.Length);
                    return new DataValueByte(data[0]);
                }
                case DataType.Int16: {
                    Debug.Assert(sizeof(short) == range.Length);
                    return new DataValueInt16(ReadValueFromSpan<short>(data, isLittleEndian));
                }
                case DataType.Int32: {
                    Debug.Assert(sizeof(int) == range.Length);
                    return new DataValueInt32(ReadValueFromSpan<int>(data, isLittleEndian));
                }
                case DataType.Int64: {
                    Debug.Assert(sizeof(long) == range.Length);
                    return new DataValueInt64(ReadValueFromSpan<long>(data, isLittleEndian));
                }
                case DataType.Float: {
                    Debug.Assert(sizeof(float) == range.Length);
                    return new DataValueFloat(ReadValueFromSpan<float>(data, isLittleEndian));
                }
                case DataType.Double: {
                    Debug.Assert(sizeof(double) == range.Length);
                    return new DataValueDouble(ReadValueFromSpan<double>(data, isLittleEndian));
                }
                case DataType.String: {
                    return new DataValueString(ReadStringFromSpan(data, strlen, stringType.ToEncoding(isLittleEndian)), stringType);
                }
                case DataType.ByteArray: {
                    return new DataValueByteArray(data.Slice(0, arrlen).ToArray());
                }
                default: throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
            }
        }
    }

    public static T ReadValueFromSpan<T>(Span<byte> buffer, bool isBufferLittleEndian) where T : unmanaged {
        if (BitConverter.IsLittleEndian != isBufferLittleEndian) {
            buffer.Slice(0, Unsafe.SizeOf<T>()).Reverse();
        }

        return MemoryMarshal.Read<T>(buffer);
    }

    public static string ReadStringFromSpan(Span<byte> bytes, int charCount, Encoding encoding) {
        Decoder decoder = encoding.GetDecoder();
        char[] charBuffer = new char[charCount];

        try {
            decoder.Convert(bytes, charBuffer.AsSpan(), true, out _, out int charsUsed, out _);
            return new string(charBuffer, 0, charsUsed);
        }
        catch {
            return "";
        }
    }

    /// <summary>
    /// Creates a fragmented memory buffer containing the bytes of the console's memory at each of the fragments in the <see cref="ranges"/> set
    /// </summary>
    /// <param name="connection">The connection to read from</param>
    /// <param name="ranges">The ranges to read</param>
    /// <param name="maximumBufferSize">
    /// The maximum allowed size of the temporary buffer used. May allocate a smaller
    /// buffer based on how <see cref="IConsoleConnection.GetRecommendedReadChunkSize"/> behaves.
    /// This also affects how early the cancellation signal can be checked
    /// </param>
    /// <param name="cancellationToken">Signals to stop the read process.</param>
    /// <returns>The buffer</returns>
    public static async Task<FragmentedMemoryBuffer> CreateMemoryView(IConsoleConnection connection, IntegerSet<uint> ranges, int maximumBufferSize = 65536, CancellationToken cancellationToken = default) {
        FragmentedMemoryBuffer memoryBuffer = new FragmentedMemoryBuffer();
        await ReadMemoryView(memoryBuffer, connection, ranges, maximumBufferSize, cancellationToken).ConfigureAwait(false);
        return memoryBuffer;
    }

    /// <summary>
    /// Main implementation of <see cref="CreateMemoryView"/>
    /// </summary>
    public static async Task ReadMemoryView(FragmentedMemoryBuffer memoryBuffer, IConsoleConnection connection, IntegerSet<uint> ranges, int maximumBufferSize = 65536, CancellationToken cancellationToken = default) {
        int maximumBuffer = connection.GetRecommendedReadChunkSize(maximumBufferSize);
        using (ArrayPools.Rent(maximumBuffer, out byte[] buffer)) {
            foreach (IntegerRange<uint> range in ranges) {
                cancellationToken.ThrowIfCancellationRequested();

                // Just in case the range's length exceeds int.MaxValue, and also to maximize
                // memory efficiency using the largest but still small buffer that the connection
                // can handle, we read in chunks of maximumBuffer
                for (int i = 0; i < range.Length; i += maximumBuffer) {
                    cancellationToken.ThrowIfCancellationRequested();
                    int length = Math.Min(maximumBuffer, (int) (range.Length - (uint) i));
                    await connection.ReadBytes(range.Start, buffer, 0, length);
                    memoryBuffer.Write(range.Start, buffer.AsSpan(0, length));
                }
            }
        }
    }
}