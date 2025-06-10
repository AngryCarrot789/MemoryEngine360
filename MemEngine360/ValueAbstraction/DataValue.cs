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
using System.Numerics;
using System.Runtime.CompilerServices;
using MemEngine360.Engine.Modes;

namespace MemEngine360.ValueAbstraction;

public abstract class BaseNumericDataValue : IDataValue, IComparable<BaseNumericDataValue> {
    public DataType DataType { get; }
    public abstract object BoxedValue { get; }
    public abstract uint ByteCount { get; }

    protected BaseNumericDataValue(DataType dataType) {
        this.DataType = dataType;
    }

    public abstract bool TryConvertTo(DataType dataType, out BaseNumericDataValue? value);

    public abstract byte ToByte();
    public abstract short ToShort();
    public abstract int ToInt();
    public abstract long ToLong();
    public abstract float ToFloat();
    public abstract double ToDouble();


    public abstract void GetBytes(Span<byte> buffer);

    public abstract bool Equals(IDataValue? other);

    /// <summary>
    /// Compares the underlying numeric value to another <see cref="BaseNumericDataValue"/>'s value.
    /// </summary>
    /// <param name="other"></param>
    /// <returns>0 when equal, 1 when the other is null, or the comparison result between the numeric values</returns>
    public int CompareTo(BaseNumericDataValue? other) {
        if (ReferenceEquals(this, other))
            return 0;
        if (ReferenceEquals(null, other))
            return 1;
        
        if (this.DataType.IsInteger() && other.DataType.IsInteger())
            return this.ToLong().CompareTo(other.ToLong());
        
        Debug.Assert(this.DataType.IsFloatingPoint() || other.DataType.IsFloatingPoint());
        if (this.DataType == DataType.Float && other.DataType == DataType.Float)
            return this.ToFloat().CompareTo(other.ToFloat());
        
        return this.ToDouble().CompareTo(other.ToDouble());
    }
}

/// <summary>
/// The base class for a numeric <see cref="IDataValue"/>
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class BaseNumericDataValue<T> : BaseNumericDataValue where T : unmanaged, INumber<T>, IEquatable<T>, IComparable<T> {
    private static readonly int TypeSize = Unsafe.SizeOf<T>();
    public T Value { get; }

    public override object BoxedValue => this.Value;

    public override uint ByteCount => (uint) TypeSize;

    protected BaseNumericDataValue(T myValue, DataType dataType) : base(dataType) {
        this.Value = myValue;
    }

    public override bool TryConvertTo(DataType dataType, out BaseNumericDataValue? value) => (value = this.ConvertTo(dataType)) != null;

    public BaseNumericDataValue? ConvertTo(DataType dataType) {
        switch (dataType) {
            case DataType.Byte:   return new DataValueByte(this.ToByte());
            case DataType.Int16:  return new DataValueInt16(this.ToShort());
            case DataType.Int32:  return new DataValueInt32(this.ToInt());
            case DataType.Int64:  return new DataValueInt64(this.ToLong());
            case DataType.Float:  return new DataValueFloat(this.ToFloat());
            case DataType.Double: return new DataValueDouble(this.ToDouble());
            default:              return null;
        }
    }

    public override byte ToByte() {
        T value = this.Value;
        if (typeof(T) == typeof(byte))
            return Unsafe.As<T, byte>(ref value);
        if (typeof(T) == typeof(float))
            return (byte) Math.Clamp(Unsafe.As<T, float>(ref value), byte.MinValue, byte.MaxValue);
        if (typeof(T) == typeof(double))
            return (byte) Math.Clamp(Unsafe.As<T, double>(ref value), byte.MinValue, byte.MaxValue);
        return (byte) Math.Clamp(Convert.ToInt64(value), byte.MinValue, byte.MaxValue);
    }

    public override short ToShort() {
        T value = this.Value;
        if (typeof(T) == typeof(short))
            return Unsafe.As<T, short>(ref value);
        if (typeof(T) == typeof(float))
            return (short) Math.Clamp(Unsafe.As<T, float>(ref value), short.MinValue, short.MaxValue);
        if (typeof(T) == typeof(double))
            return (short) Math.Clamp(Unsafe.As<T, double>(ref value), short.MinValue, short.MaxValue);
        return (short) Math.Clamp(Convert.ToInt64(value), short.MinValue, short.MaxValue);
    }

    public override int ToInt() {
        T value = this.Value;
        if (typeof(T) == typeof(int))
            return Unsafe.As<T, int>(ref value);
        if (typeof(T) == typeof(float))
            return (int) Math.Clamp(Unsafe.As<T, float>(ref value), int.MinValue, int.MaxValue);
        if (typeof(T) == typeof(double))
            return (int) Math.Clamp(Unsafe.As<T, double>(ref value), int.MinValue, int.MaxValue);
        return (int) Math.Clamp(Convert.ToInt64(value), int.MinValue, int.MaxValue);
    }

    public override long ToLong() {
        T value = this.Value;
        if (typeof(T) == typeof(long))
            return Unsafe.As<T, long>(ref value);
        if (typeof(T) == typeof(float))
            return (long) Math.Clamp(Unsafe.As<T, float>(ref value), long.MinValue, long.MaxValue);
        if (typeof(T) == typeof(double))
            return (long) Math.Clamp(Unsafe.As<T, double>(ref value), long.MinValue, long.MaxValue);
        return Math.Clamp(Convert.ToInt64(value), long.MinValue, long.MaxValue);
    }

    public override float ToFloat() {
        T value = this.Value;
        if (typeof(T) == typeof(float))
            return Unsafe.As<T, float>(ref value);
        if (typeof(T) == typeof(double))
            return (float) Math.Clamp(Unsafe.As<T, double>(ref value), float.MinValue, float.MaxValue);
        return Math.Clamp(Convert.ToSingle(value), float.MinValue, float.MaxValue);
    }

    public override double ToDouble() {
        T value = this.Value;
        if (typeof(T) == typeof(float))
            return Unsafe.As<T, float>(ref value);
        if (typeof(T) == typeof(double))
            return Unsafe.As<T, double>(ref value);
        return Math.Clamp(Convert.ToDouble(value), double.MinValue, double.MaxValue);
    }

    public override void GetBytes(Span<byte> buffer) {
        if (buffer.Length < TypeSize) {
            throw new ArgumentException($"Buffer is too small ({buffer.Length} < {TypeSize})");
        }

        Unsafe.As<byte, T>(ref buffer.GetPinnableReference()) = this.Value;
    }

    protected bool Equals(BaseNumericDataValue<T> other) {
        return this.DataType == other.DataType && this.Value == other.Value;
    }

    public override bool Equals(IDataValue? other) {
        if (ReferenceEquals(other, this))
            return true;
        return other != null && other.DataType == this.DataType && other is BaseNumericDataValue<T> numeric && this.Equals(numeric);
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

    public uint ByteCount => this.StringType.GetByteCount(this.Value);

    public DataValueString(string value, StringType stringType) {
        this.Value = value;
        this.StringType = stringType;

        switch (this.StringType) {
            case StringType.ASCII:
            case StringType.UTF8:
            case StringType.UTF16:
            case StringType.UTF32:
                break;
            default: throw new ArgumentOutOfRangeException();
        }
    }

    public void GetBytes(Span<byte> buffer) {
        this.StringType.ToEncoding().GetBytes(this.Value, buffer);
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

public class DataValueByteArray : IDataValue {
    public DataType DataType => DataType.ByteArray;

    public byte[] Value { get; }

    public object BoxedValue => this.Value;

    public uint ByteCount => (uint) this.Value.Length;

    public DataValueByteArray(byte[] value) {
        this.Value = value;
    }

    public void GetBytes(Span<byte> buffer) {
        this.Value.CopyTo(buffer);
    }

    protected bool Equals(DataValueByteArray other) {
        return this.DataType == other.DataType && this.Value.SequenceEqual(other.Value);
    }

    public bool Equals(IDataValue? other) => other is DataValueByteArray str && this.Equals(str);

    public override bool Equals(object? obj) {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        return obj is DataValueByteArray str && this.Equals(str);
    }

    public override int GetHashCode() {
        return HashCode.Combine((int) this.DataType, this.Value);
    }
}