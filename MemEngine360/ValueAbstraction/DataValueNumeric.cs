using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MemEngine360.Engine.Modes;

namespace MemEngine360.ValueAbstraction;

public abstract class DataValueNumeric : IDataValue, IComparable<DataValueNumeric> {
    public DataType DataType { get; }
    
    public abstract object BoxedValue { get; }
    
    public abstract int ByteCount { get; }

    internal DataValueNumeric(DataType dataType) {
        if (!dataType.IsNumeric())
            throw new ArgumentException("Data type is not numeric, it is " + dataType, nameof(dataType));
        this.DataType = dataType;
    }

    public abstract bool TryConvertTo(DataType dataType, out DataValueNumeric? value);

    public abstract byte ToByte();
    public abstract short ToShort();
    public abstract int ToInt();
    public abstract long ToLong();
    public abstract float ToFloat();
    public abstract double ToDouble();

    public abstract int GetBytes(Span<byte> buffer, bool littleEndian);

    public abstract bool Equals(IDataValue? other);

    /// <summary>
    /// Compares the underlying numeric value to another <see cref="DataValueNumeric"/>'s value.
    /// </summary>
    /// <param name="other"></param>
    /// <returns>0 when equal, 1 when the other is null, or the comparison result between the numeric values</returns>
    public int CompareTo(DataValueNumeric? other) {
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
public abstract class DataValueNumeric<T> : DataValueNumeric where T : unmanaged, INumber<T>, IEquatable<T>, IComparable<T> {
    public T Value { get; }

    public override object BoxedValue => this.Value;

    public override int ByteCount => Unsafe.SizeOf<T>();

    internal DataValueNumeric(T myValue, DataType dataType) : base(dataType) {
        this.Value = myValue;
    }

    public override bool TryConvertTo(DataType dataType, out DataValueNumeric? value) => (value = this.ConvertTo(dataType)) != null;

    public DataValueNumeric? ConvertTo(DataType dataType) {
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

    public override int GetBytes(Span<byte> buffer, bool littleEndian) {
        int typeSize = Unsafe.SizeOf<T>();
        if (typeSize > buffer.Length) {
            throw new ArgumentException($"Buffer is too small ({buffer.Length} < sizeof(T) ({typeSize}))");
        }

        Unsafe.As<byte, T>(ref MemoryMarshal.GetReference(buffer)) = this.Value;
        if (BitConverter.IsLittleEndian != littleEndian) {
            buffer.Slice(0, typeSize).Reverse();
        }

        return typeSize;
    }

    protected bool Equals(DataValueNumeric<T> other) {
        return this.DataType == other.DataType && this.Value == other.Value;
    }

    public override bool Equals(IDataValue? other) {
        if (ReferenceEquals(other, this))
            return true;
        return other != null
               && other.DataType == this.DataType
               && other is DataValueNumeric<T> numeric
               && this.Equals(numeric);
    }

    public override bool Equals(object? obj) {
        if (ReferenceEquals(this, obj))
            return true;

        return obj is DataValueNumeric<T> value && this.Equals(value);
    }

    public override int GetHashCode() {
        return HashCode.Combine((int) this.DataType, this.Value);
    }
}

public abstract class DataValueInteger<T> : DataValueNumeric<T> where T : unmanaged, IBinaryInteger<T> {
    internal DataValueInteger(T myValue, DataType dataType) : base(myValue, dataType) {
    }
}

public abstract class DataValueFloatingPoint<T> : DataValueNumeric<T> where T : unmanaged, IFloatingPoint<T> {
    internal DataValueFloatingPoint(T myValue, DataType dataType) : base(myValue, dataType) {
    }
}

public sealed class DataValueByte(byte myValue) : DataValueInteger<byte>(myValue, DataType.Byte);

public sealed class DataValueInt16(short myValue) : DataValueInteger<short>(myValue, DataType.Int16);

public sealed class DataValueInt32(int myValue) : DataValueInteger<int>(myValue, DataType.Int32);

public sealed class DataValueInt64(long myValue) : DataValueInteger<long>(myValue, DataType.Int64);

public sealed class DataValueFloat(float myValue) : DataValueFloatingPoint<float>(myValue, DataType.Float);

public sealed class DataValueDouble(double myValue) : DataValueFloatingPoint<double>(myValue, DataType.Double);