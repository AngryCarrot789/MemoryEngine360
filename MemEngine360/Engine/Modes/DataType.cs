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

namespace MemEngine360.Engine.Modes;

/// <summary>
/// A searchable data type
/// </summary>
public enum DataType {
    /// <summary>
    /// 8 bits
    /// </summary>
    Byte,

    /// <summary>
    /// 16 bits
    /// </summary>
    Int16,

    /// <summary>
    /// 32 bits
    /// </summary>
    Int32,

    /// <summary>
    /// 64 bits
    /// </summary>
    Int64,

    /// <summary>
    /// 32 bit floating point number
    /// </summary>
    Float,

    /// <summary>
    /// 64 bit floating point number
    /// </summary>
    Double,

    /// <summary>
    /// A searchable string
    /// </summary>
    String,

    /// <summary>
    /// A searchable sequence of bytes, while also supporting a wildcard ('?') character
    /// </summary>
    ByteArray
}

public static class DataTypeExtensions {
    /// <summary>
    /// The data type is byte, short, int, long, float or double
    /// </summary>
    public static bool IsNumeric(this DataType dataType) {
        switch (dataType) {
            case DataType.Byte:
            case DataType.Int16:
            case DataType.Int32:
            case DataType.Int64:
            case DataType.Float:
            case DataType.Double:
                return true;
            default: return false;
        }
    }

    /// <summary>
    /// The data type is byte, short, int or long
    /// </summary>
    public static bool IsInteger(this DataType dataType) {
        switch (dataType) {
            case DataType.Byte:
            case DataType.Int16:
            case DataType.Int32:
            case DataType.Int64:
                return true;
            default: return false;
        }
    }

    /// <summary>
    /// The data type is float or double
    /// </summary>
    public static bool IsFloatingPoint(this DataType dataType) {
        switch (dataType) {
            case DataType.Float:
            case DataType.Double:
                return true;
            default: return false;
        }
    }
    
    /// <summary>
    /// Returns the data type size (ONLY FOR NUMERIC PRIMITIVES)
    /// </summary>
    /// <param name="dataType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">Data type is not a numeric primitive or is an invalid data type</exception>
    public static uint GetNumericSize(this DataType dataType) {
        switch (dataType) {
            case DataType.Byte:      return 1u;
            case DataType.Int16:     return 2u;
            case DataType.Int32:     return 4u;
            case DataType.Int64:     return 8u;
            case DataType.Float:     return 4u;
            case DataType.Double:    return 8u;
            case DataType.String:    throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Cannot get the byte count of a string purely from the data type");
            case DataType.ByteArray: throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Cannot get the byte count of a sequence of bytes purely from the data type");
            default:                 throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
        }
    }

    /// <summary>
    /// Gets the recommended byte alignment for the data type. For example, word values (primitives) return
    /// their data type size, and everything else returns 1
    /// </summary>
    public static uint GetAlignmentFromDataType(this DataType type) {
        switch (type) {
            case DataType.Byte:      return 1u;
            case DataType.Int16:     return 2u;
            case DataType.Int32:     return 4u;
            case DataType.Int64:     return 8u;
            case DataType.Float:     return 4u;
            case DataType.Double:    return 8u;
            case DataType.String:    return 1u; // scan for the entire string for each next char
            case DataType.ByteArray: return 1u;
            default:                 throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}