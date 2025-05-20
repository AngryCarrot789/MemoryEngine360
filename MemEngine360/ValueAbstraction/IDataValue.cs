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

using System.Numerics;
using System.Runtime.CompilerServices;
using MemEngine360.Connections;
using MemEngine360.Engine.Modes;

namespace MemEngine360.ValueAbstraction;

/// <summary>
/// An interface that safely represents a value of a specific <see cref="DataType"/>
/// </summary>
public interface IDataValue : IEquatable<IDataValue> {
    /// <summary>
    /// Gets our data type
    /// </summary>
    DataType DataType { get; }

    /// <summary>
    /// Gets the underlying value as an object
    /// </summary>
    object BoxedValue { get; }

    /// <summary>
    /// Writes this data value to the connection at the address
    /// </summary>
    /// <param name="address">The address to write the value at</param>
    /// <param name="connection">The connection to write the value to</param>
    /// <returns></returns>
    Task WriteToConnection(uint address, IConsoleConnection connection);

    /// <summary>
    /// Gets this data value as an array of bytes. E.g. an int32 would return 4 elements.
    /// </summary>
    /// <param name="asLittleEndian">True to specify the value as little endian, false to specify as big endian</param>
    /// <returns></returns>
    byte[] GetBytes(bool asLittleEndian);
    
    static IDataValue Create(DataType dataType, object value) {
        switch (dataType) {
            case DataType.Byte:   return new DataValueByte((byte) value);
            case DataType.Int16:  return new DataValueInt16((short) value);
            case DataType.Int32:  return new DataValueInt32((int) value);
            case DataType.Int64:  return new DataValueInt64((long) value);
            case DataType.Float:  return new DataValueFloat((float) value);
            case DataType.Double: return new DataValueDouble((double) value);
            case DataType.String: return new DataValueString((string) value, StringType.UTF16);
            default:              throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
        }
    }
    
    static BaseNumericDataValue<T> CreateNumeric<T>(T value) where T : unmanaged, INumber<T> {
        if (typeof(T) == typeof(byte))
            return Unsafe.As<BaseNumericDataValue<T>>(new DataValueByte(Unsafe.As<T, byte>(ref value)));
        if (typeof(T) == typeof(short))
            return Unsafe.As<BaseNumericDataValue<T>>(new DataValueInt16(Unsafe.As<T, short>(ref value)));
        if (typeof(T) == typeof(int))
            return Unsafe.As<BaseNumericDataValue<T>>(new DataValueInt32(Unsafe.As<T, int>(ref value)));
        if (typeof(T) == typeof(long))
            return Unsafe.As<BaseNumericDataValue<T>>(new DataValueInt64(Unsafe.As<T, long>(ref value)));
        if (typeof(T) == typeof(float))
            return Unsafe.As<BaseNumericDataValue<T>>(new DataValueFloat(Unsafe.As<T, float>(ref value)));
        if (typeof(T) == typeof(double))
            return Unsafe.As<BaseNumericDataValue<T>>(new DataValueDouble(Unsafe.As<T, double>(ref value)));
        throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported value type: " + typeof(T));
    }
    
    static DataValueString CreateString(string value, StringType type = StringType.ASCII) => new DataValueString(value ?? "", type);
}