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

using System.Text;
using MemEngine360.Engine.Modes;

namespace MemEngine360.Sequencing;

public readonly struct TypedAddress(DataType dataType, uint address) : IEquatable<TypedAddress> {
    public DataType DataType { get; } = dataType;

    public uint Address { get; } = address;

    public override string ToString() {
        StringBuilder builder = new StringBuilder();
        builder.Append("TypedAddress");
        builder.Append(" { ");
        if (this.PrintMembers(builder))
            builder.Append(' ');
        builder.Append('}');
        return builder.ToString();
    }

    private bool PrintMembers(StringBuilder builder) {
        builder.Append("type = ");
        builder.Append(this.DataType.ToString());
        builder.Append(", address = ");
        builder.Append(this.Address.ToString());
        return true;
    }

    public override int GetHashCode() {
        return ((int) this.DataType).GetHashCode() * -1521134295 + this.Address.GetHashCode();
    }

    public override bool Equals(object? obj) {
        return obj is TypedAddress other && this.Equals(other);
    }

    public bool Equals(TypedAddress other) {
        return this.DataType == other.DataType && this.Address == other.Address;
    }

    public static bool operator ==(TypedAddress left, TypedAddress right) => left.Equals(right);

    public static bool operator !=(TypedAddress left, TypedAddress right) => !left.Equals(right);
}