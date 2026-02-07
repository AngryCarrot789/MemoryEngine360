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
using System.Text;
using MemEngine360.Engine.Modes;
using PFXToolKitUI.Utils;

namespace MemEngine360.ValueAbstraction;

public sealed class DataValueString : IDataValue {
    public DataType DataType => DataType.String;

    public string Value { get; }

    public StringType StringType { get; }

    public object BoxedValue => this.Value;

    public int ByteCount => this.StringType.GetByteCount(this.Value, false); // Endianness doesn't necessarily matter here, i think

    public DataValueString(string value, StringType stringType) {
        EnumUtils.Validate(stringType);
        this.Value = value;
        this.StringType = stringType;
    }

    public int GetBytes(Span<byte> buffer, bool littleEndian) {
        return this.StringType.ToEncoding(littleEndian).GetBytes(this.Value, buffer);
    }

    /// <summary>
    /// Creates a byte array and encodes our string value into it.
    /// </summary>
    /// <param name="littleEndian">Whether to encode using little endianness</param>
    /// <param name="appendNullChar">Whether to include a null character at the end of the buffer</param>
    /// <param name="length">The amount of encoded string characters, plus 1 if <see cref="appendNullChar"/> is true</param>
    /// <returns>The array containing the encoded string, and optional null char. May be larger than required</returns>
    public byte[] GetBytes(bool littleEndian, bool appendNullChar, out int length) {
        Encoding encoding = this.StringType.ToEncoding(littleEndian);
        int byteCount = encoding.GetByteCount(this.Value);
        int nullCharCount = appendNullChar ? 1 : 0;

        byte[] array = new byte[byteCount + nullCharCount];
        int count = encoding.GetBytes(this.Value, array.AsSpan());
        Debug.Assert(appendNullChar ? count < array.Length : count <= array.Length);
        if (appendNullChar) {
            array[count /* index after last char encoded */] = 0;
        }

        length = count + nullCharCount;
        return array;
    }

    public bool Equals(DataValueString other) {
        Debug.Assert(this.DataType == other.DataType);

        return this.Value == other.Value && this.StringType == other.StringType;
    }

    public bool Equals(IDataValue? other) => other is DataValueString str && this.Equals(str);

    public override bool Equals(object? obj) {
        if (obj == null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        return obj is DataValueString str && this.Equals(str);
    }

    public override int GetHashCode() {
        return HashCode.Combine((int) this.DataType, this.Value);
    }
}