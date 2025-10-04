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
using MemEngine360.Engine.Modes;

namespace MemEngine360.ValueAbstraction;

public sealed class DataValueByteArray : IDataValue {
    public DataType DataType => DataType.ByteArray;

    public byte[] Value { get; }

    public object BoxedValue => this.Value;

    public int ByteCount => this.Value.Length;

    public DataValueByteArray(byte[] value) {
        ArgumentNullException.ThrowIfNull(value);
        this.Value = value;
    }
    
    public int GetBytes(Span<byte> buffer, bool littleEndian) {
        this.Value.CopyTo(buffer);
        return this.Value.Length;
    }

    public bool Equals(DataValueByteArray other) {
        Debug.Assert(this.DataType == other.DataType);
        
        return this.Value.SequenceEqual(other.Value);
    }

    public bool Equals(IDataValue? other) => other is DataValueByteArray str && this.Equals(str);

    public override bool Equals(object? obj) {
        if (obj == null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        return obj is DataValueByteArray str && this.Equals(str);
    }

    public override int GetHashCode() {
        return HashCode.Combine((int) this.DataType, this.Value);
    }
}