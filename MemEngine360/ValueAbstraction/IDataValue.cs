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

using System.Numerics;
using System.Runtime.CompilerServices;
using MemEngine360.Engine.Modes;

namespace MemEngine360.ValueAbstraction;

/// <summary>
/// An immutable object that stores a value of a specific <see cref="DataType"/>.
/// <para>
/// In terms of endianness, it's assumed that a data value is created with a value akin to the system endianness.
/// This of course doesn't affect things like strings, just multibyte primitives such as int and float
/// </para>
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
    /// Gets the amount of bytes this data value takes up. This is used for calls to <see cref="GetBytes(System.Span{byte})"/>
    /// </summary>
    uint ByteCount { get; }

    /// <summary>
    /// Writes this data value into the buffer. Callers must ensure the buffer contains enough bytes.
    /// For all numeric data values, you can safely pass any buffer of at least 8 bytes (obviously...)
    /// </summary>
    /// <param name="buffer">The dst buffer</param>
    /// <param name="littleEndian">
    /// True to specify the data must be written as little endian relative to the system endianness.
    /// May be ignored for data types where endianness isn't applicable (e.g. byte array or byte)
    /// </param>
    void GetBytes(Span<byte> buffer, bool littleEndian);

    /// <summary>
    /// A helper method for getting the bytes of this data value as an array. This creates an array
    /// of <see cref="ByteCount"/> length and passes it to <see cref="GetBytes(System.Span{byte})"/>.
    /// </summary>
    /// <param name="littleEndian">True to specify the data must be written as little endian relative to the system endianness</param>
    /// <returns>An array containing <see cref="ByteCount"/> elements representing the underlying value this object stores</returns>
    byte[] GetBytes(bool littleEndian) {
        byte[] buffer = new byte[this.ByteCount];
        this.GetBytes(buffer.AsSpan(), littleEndian);
        return buffer;
    }

    /// <summary>
    /// Creates a data value from an object value. This is an unsafe method since it blindly casts the value to the expected data type
    /// </summary>
    /// <param name="dataType">The type of value we should expect</param>
    /// <param name="value">The actual value</param>
    /// <param name="stringType">The type of string. Used when <see cref="dataType"/> is <see cref="Engine.Modes.DataType.String"/></param>
    /// <returns>The data value</returns>
    /// <exception cref="ArgumentOutOfRangeException">Invalid data type</exception>
    static IDataValue Create(DataType dataType, object value, StringType stringType = StringType.ASCII) {
        switch (dataType) {
            case DataType.Byte:      return new DataValueByte((byte) value);
            case DataType.Int16:     return new DataValueInt16((short) value);
            case DataType.Int32:     return new DataValueInt32((int) value);
            case DataType.Int64:     return new DataValueInt64((long) value);
            case DataType.Float:     return new DataValueFloat((float) value);
            case DataType.Double:    return new DataValueDouble((double) value);
            case DataType.String:    return new DataValueString((string) value, stringType);
            case DataType.ByteArray: return new DataValueByteArray((byte[]) value);
            default:                 throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
        }
    }

    static IDataValue CreateDefault(DataType dataType, StringType stringType) {
        switch (dataType) {
            case DataType.Byte:      
            case DataType.Int16:     
            case DataType.Int32:     
            case DataType.Int64:     
            case DataType.Float:     
            case DataType.Double:    return CreateDefaultNumeric(dataType);
            case DataType.String:    return new DataValueString("", stringType);
            case DataType.ByteArray: return new DataValueByteArray([]);
            default:                 throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
        }
    }
    
    static BaseNumericDataValue CreateDefaultNumeric(DataType dataType) {
        switch (dataType) {
            case DataType.Byte:      return new DataValueByte(0);
            case DataType.Int16:     return new DataValueInt16(0);
            case DataType.Int32:     return new DataValueInt32(0);
            case DataType.Int64:     return new DataValueInt64(0);
            case DataType.Float:     return new DataValueFloat(0);
            case DataType.Double:    return new DataValueDouble(0);
            default:                 throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
        }
    }

    /// <summary>
    /// Creates a numeric data value from the number. <see cref="T"/> can be byte, short, int, long, float or double.
    /// </summary>
    /// <param name="value">The numeric value</param>
    /// <typeparam name="T">The type of number</typeparam>
    /// <returns>The data value</returns>
    /// <exception cref="ArgumentOutOfRangeException">Unsupported type of <see cref="T"/></exception>
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

    /// <summary>
    /// Creates a float data value from the floating point number. <see cref="T"/> can be float or double.
    /// </summary>
    /// <param name="value">The floating point value</param>
    /// <typeparam name="T">The type of floating point number</typeparam>
    /// <returns>The data value</returns>
    /// <exception cref="ArgumentOutOfRangeException">Unsupported type of <see cref="T"/></exception>
    static BaseFloatDataValue<T> CreateFloat<T>(T value) where T : unmanaged, IFloatingPoint<T> {
        if (typeof(T) == typeof(float))
            return Unsafe.As<BaseFloatDataValue<T>>(new DataValueFloat(Unsafe.As<T, float>(ref value)));
        if (typeof(T) == typeof(double))
            return Unsafe.As<BaseFloatDataValue<T>>(new DataValueDouble(Unsafe.As<T, double>(ref value)));
        throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported value type: " + typeof(T));
    }

    /// <summary>
    /// Creates a string data value. A null string is replaced with an empty string
    /// </summary>
    /// <param name="value">The string value</param>
    /// <param name="type">The string type (ascii, unicode, etc.)</param>
    /// <returns></returns>
    static DataValueString CreateString(string? value, StringType type = StringType.ASCII) => new DataValueString(value ?? "", type);

    /// <summary>
    /// Attempts to convert a data value into a new instance typed by <see cref="newDataType"/>
    /// </summary>
    /// <param name="value">The value to be converted</param>
    /// <param name="newDataType">The data type to convert into</param>
    /// <param name="newStringType">The string type</param>
    /// <returns></returns>
    static IDataValue? TryConvertDataValue(IDataValue? value, DataType newDataType, StringType newStringType) {
        if (value == null) {
            return null;
        }

        // Same types?
        if (value.DataType == newDataType) {
            // String type differs? Convert to new string type
            if (value.DataType == DataType.String && ((DataValueString) value).StringType != newStringType) {
                return new DataValueString(((DataValueString) value).Value, newStringType);
            }

            return value;
        }

        // Old value is numeric?
        if (value.DataType.IsNumeric()) {
            // Try convert into string
            if (newDataType == DataType.String) {
                return new DataValueString(value.BoxedValue.ToString() ?? "", newStringType);
            }

            // Try convert into another numeric type
            return ((BaseNumericDataValue) value).TryConvertTo(newDataType, out BaseNumericDataValue? newValue) ? newValue : null;
        }

        // Old value is string or pattern... or another if more were added since this comment.
        return null;
    }
}