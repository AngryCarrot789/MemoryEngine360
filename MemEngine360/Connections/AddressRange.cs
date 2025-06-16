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

namespace MemEngine360.Connections;

public readonly struct AddressRange : IEquatable<AddressRange> {
    public readonly uint BaseAddress, Length;

    public uint EndAddress => this.BaseAddress + this.Length;

    public AddressRange(uint baseAddress, uint length) {
        if ((baseAddress + length) < baseAddress)
            throw new InvalidOperationException("Base + Length exceeds uint max value");

        this.BaseAddress = baseAddress;
        this.Length = length;
    }

    public bool Equals(AddressRange other) => this.BaseAddress == other.BaseAddress && this.Length == other.Length;

    public override bool Equals(object? obj) => obj is AddressRange other && this.Equals(other);

    public override int GetHashCode() => HashCode.Combine(this.BaseAddress, this.Length);

    public static bool operator ==(AddressRange left, AddressRange right) => left.Equals(right);

    public static bool operator !=(AddressRange left, AddressRange right) => !left.Equals(right);

    public bool IsInRange(uint address) {
        return address >= this.BaseAddress && address < this.EndAddress;
    }
    
    public bool IsInRange(uint address, uint length) {
        uint endAddr = address + length;
        if (endAddr < address)
            throw new ArgumentException("Address + Length exceeds uint max value");
        return address >= this.BaseAddress && endAddr <= this.EndAddress;
    }
    
    public bool IsInRange(AddressRange range) {
        return range.BaseAddress >= this.BaseAddress && range.EndAddress <= this.EndAddress;
    }

    public uint ClampLength(uint address, uint length) {
        uint endAddr = address + length;
        if (endAddr < address)
            throw new ArgumentException("Address + Length exceeds uint max value");

        return Math.Min(this.EndAddress, endAddr) - address;
    }
    
    public uint ClampLength(AddressRange range) {
        return Math.Min(this.EndAddress, range.EndAddress) - range.BaseAddress;
    }
}