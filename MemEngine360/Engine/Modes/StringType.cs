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

using System.Text;

namespace MemEngine360.Engine.Modes;

public enum StringType {
    /// <summary>
    /// Fixed single-byte encoding, 1 byte, only first 7-bits are used
    /// </summary>
    ASCII,

    /// <summary>
    /// Variable length encoding, 1 to 4 bytes.
    /// </summary>
    UTF8,

    /// <summary>
    /// Variable length encoding, 2 or 4 bytes. Endianness is relative to connection
    /// </summary>
    UTF16,

    /// <summary>
    /// Fixed length encoding of 4 bytes.
    /// </summary>
    UTF32
}

public static class StringTypeExtensions {
    public static Encoding ToEncoding(this StringType stringType, bool isLittleEndian) {
        switch (stringType) {
            case StringType.ASCII: return Encoding.ASCII;
            case StringType.UTF8:  return Encoding.UTF8;
            case StringType.UTF16: return isLittleEndian ? Encoding.Unicode : Encoding.BigEndianUnicode;
            case StringType.UTF32: return Encoding.UTF32;
            default:               throw new ArgumentOutOfRangeException(nameof(stringType), stringType, null);
        }
    }

    public static uint GetByteCount(this StringType stringType, string value, bool isLittleEndian) {
        return (uint) stringType.ToEncoding(isLittleEndian).GetByteCount(value);
    }
}