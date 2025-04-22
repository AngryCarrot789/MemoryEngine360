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
    String
}

public static class DataTypeExtensions {
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
    
    public static bool IsFloat(this DataType dataType) {
        switch (dataType) {
            case DataType.Float:
            case DataType.Double:
                return true;
            default: return false;
        }
    }
}