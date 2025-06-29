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

namespace MemEngine360.Engine.Addressing;

public sealed class StaticAddress : IMemoryAddress, IEquatable<StaticAddress> {
    public static readonly StaticAddress Zero = new StaticAddress(0);
    
    bool IMemoryAddress.IsStatic => true;
    
    public uint Address { get; }

    public StaticAddress(uint address) {
        this.Address = address;
    }
    
    public bool Equals(StaticAddress? other) {
        if (other == null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return this.Address == other.Address;
    }

    public override bool Equals(object? obj) {
        return ReferenceEquals(this, obj) || obj is StaticAddress other && this.Equals(other);
    }

    public override int GetHashCode() {
        return (int) this.Address;
    }

    public override string ToString() {
        return this.Address.ToString("X8");
    }
}