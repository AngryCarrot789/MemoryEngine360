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

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using PFXToolKitUI.Interactivity.Formatting;

namespace MemEngine360.Engine.Scanners;

public static class ValueScannerUtils {
    public static readonly AutoMemoryValueFormatter ByteFormatter = new AutoMemoryValueFormatter() {
        SourceFormat = MemoryFormatType.Byte,
        NonEditingRoundedPlaces = 1,
        AllowedFormats = [MemoryFormatType.Byte, MemoryFormatType.KiloByte1000, MemoryFormatType.MegaByte1000, MemoryFormatType.GigaByte1000, MemoryFormatType.TeraByte1000]
    };

    public static double TruncateDouble(double value, int decimals) {
        double factor = Math.Pow(10, decimals);
        return Math.Truncate(value * factor) / factor;
    }

    public static T CreateNumberFromBytes<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        if (typeof(T) == typeof(sbyte))
            return CreateGeneric_sbyte<T>(bytes);
        if (typeof(T) == typeof(byte))
            return CreateGeneric_byte<T>(bytes);
        if (typeof(T) == typeof(short))
            return CreateGeneric_short<T>(bytes);
        if (typeof(T) == typeof(ushort))
            return CreateGeneric_ushort<T>(bytes);
        if (typeof(T) == typeof(int))
            return CreateGeneric_int<T>(bytes);
        if (typeof(T) == typeof(uint))
            return CreateGeneric_uint<T>(bytes);
        if (typeof(T) == typeof(long))
            return CreateGeneric_long<T>(bytes);
        if (typeof(T) == typeof(ulong))
            return CreateGeneric_ulong<T>(bytes);
        if (typeof(T) == typeof(float))
            return CreateGeneric_float<T>(bytes);
        if (typeof(T) == typeof(double))
            return CreateGeneric_double<T>(bytes);
        throw new NotSupportedException();
    }
    
    public static T CreateFloat<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        if (typeof(T) == typeof(float))
            return CreateGeneric_float<T>(bytes);
        if (typeof(T) == typeof(double))
            return CreateGeneric_double<T>(bytes);
        throw new NotSupportedException();
    }

    public static TOut CreateNumberFromRawLong<TOut>(ulong src) where TOut : INumber<TOut> {
        return Unsafe.As<ulong, TOut>(ref src);
    }

    private static T CreateGeneric_sbyte<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        sbyte value = (sbyte) bytes[0];
        return Unsafe.As<sbyte, T>(ref value);
    }

    private static T CreateGeneric_byte<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        byte value = bytes[0];
        return Unsafe.As<byte, T>(ref value);
    }

    private static T CreateGeneric_short<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        short value = BinaryPrimitives.ReadInt16BigEndian(bytes);
        return Unsafe.As<short, T>(ref value);
    }

    private static T CreateGeneric_ushort<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        ushort value = BinaryPrimitives.ReadUInt16BigEndian(bytes);
        return Unsafe.As<ushort, T>(ref value);
    }

    private static T CreateGeneric_int<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        int value = BinaryPrimitives.ReadInt32BigEndian(bytes);
        return Unsafe.As<int, T>(ref value);
    }

    private static T CreateGeneric_uint<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        uint value = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        return Unsafe.As<uint, T>(ref value);
    }

    private static T CreateGeneric_long<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        long value = BinaryPrimitives.ReadInt64BigEndian(bytes);
        return Unsafe.As<long, T>(ref value);
    }

    private static T CreateGeneric_ulong<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        ulong value = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        return Unsafe.As<ulong, T>(ref value);
    }

    private static T CreateGeneric_float<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        float value = BinaryPrimitives.ReadSingleBigEndian(bytes);
        return Unsafe.As<float, T>(ref value);
    }

    private static T CreateGeneric_double<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        double value = BinaryPrimitives.ReadDoubleBigEndian(bytes);
        return Unsafe.As<double, T>(ref value);
    }
}