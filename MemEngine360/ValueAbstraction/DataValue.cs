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
using System.Runtime.InteropServices;
using System.Text;
using MemEngine360.Engine.Modes;

namespace MemEngine360.ValueAbstraction;

/// <summary>
/// The base class for a numeric <see cref="IDataValue"/>
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class BaseNumericDataValue<T> : IDataValue where T : unmanaged, INumber<T>, IEquatable<T>, IComparable<T> {
    private static readonly int TypeSize = Unsafe.SizeOf<T>();
    
    public DataType DataType { get; }

    public T Value { get; }

    public object BoxedValue => this.Value;

    protected BaseNumericDataValue(T myValue, DataType dataType) {
        this.Value = myValue;
        this.DataType = dataType;
    }

    public byte[] GetBytes(bool asLittleEndian) {
        byte[] bytes = new byte[TypeSize];
        Unsafe.As<byte, T>(ref MemoryMarshal.GetArrayDataReference(bytes)) = this.Value;
        if (asLittleEndian != BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return bytes;
    }

    protected bool Equals(BaseNumericDataValue<T> other) {
        return this.DataType == other.DataType && this.Value == other.Value;
    }

    public bool Equals(IDataValue? other) {
        return other != null && other.DataType == this.DataType && other.BoxedValue == this.BoxedValue;
    }

    public override bool Equals(object? obj) {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (!(obj is IDataValue value) || value.DataType != this.DataType)
            return false;
        return value is BaseNumericDataValue<T> ? this.Equals((BaseNumericDataValue<T>) obj) : this.Equals(value);
    }
    
    public override int GetHashCode() {
        return HashCode.Combine((int) this.DataType, this.Value);
    }
}

public abstract class BaseIntegerDataValue<T>(T myValue, DataType dataType) : BaseNumericDataValue<T>(myValue, dataType) where T : unmanaged, IBinaryNumber<T>;

public abstract class BaseFloatDataValue<T>(T myValue, DataType dataType) : BaseNumericDataValue<T>(myValue, dataType) where T : unmanaged, IFloatingPoint<T>;

public class DataValueByte(byte myValue) : BaseIntegerDataValue<byte>(myValue, DataType.Byte);

public class DataValueInt16(short myValue) : BaseIntegerDataValue<short>(myValue, DataType.Int16);

public class DataValueInt32(int myValue) : BaseIntegerDataValue<int>(myValue, DataType.Int32);

public class DataValueInt64(long myValue) : BaseIntegerDataValue<long>(myValue, DataType.Int64);

public class DataValueFloat(float myValue) : BaseFloatDataValue<float>(myValue, DataType.Float);

public class DataValueDouble(double myValue) : BaseFloatDataValue<double>(myValue, DataType.Double);

public class DataValueString : IDataValue {
    public DataType DataType => DataType.String;

    public string Value { get; }

    public StringType StringType { get; }
    
    public object BoxedValue => this.Value;

    public DataValueString(string value, StringType stringType) {
        this.Value = value;
        this.StringType = stringType;
        
        switch (this.StringType) {
            case StringType.ASCII:
            case StringType.UTF8: 
            case StringType.UTF16:
            case StringType.UTF32: break;
            default:               throw new ArgumentOutOfRangeException();
        }
    }

    public byte[] GetBytes(bool asLittleEndian) => this.GetBytes();
    
    public byte[] GetBytes() {
        switch (this.StringType) {
            case StringType.ASCII: return Encoding.ASCII.GetBytes(this.Value);
            case StringType.UTF8:  return Encoding.UTF8.GetBytes(this.Value);
            case StringType.UTF16: return Encoding.Unicode.GetBytes(this.Value);
            case StringType.UTF32: return Encoding.UTF32.GetBytes(this.Value);
            default:
                throw new Exception("Memory corruption");
        }
    }

    protected bool Equals(DataValueString other) {
        return this.DataType == other.DataType && this.Value == other.Value && this.StringType == other.StringType;
    }

    public bool Equals(IDataValue? other) => other is DataValueString str && this.Equals(str);

    public override bool Equals(object? obj) {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        return obj is DataValueString str && this.Equals(str);
    }
    
    public override int GetHashCode() {
        return HashCode.Combine((int) this.DataType, this.Value);
    }
}