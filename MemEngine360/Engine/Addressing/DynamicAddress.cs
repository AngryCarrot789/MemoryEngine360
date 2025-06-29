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

using System.Collections.Immutable;
using System.Text;
using MemEngine360.Connections;

namespace MemEngine360.Engine.Addressing;

/// <summary>
/// A memory address that is resolved from a list of pointer offsets
/// </summary>
public sealed class DynamicAddress : IMemoryAddress, IEquatable<DynamicAddress> {
    /// <summary>
    /// Gets the base address of this memory address.
    /// </summary>
    public uint BaseAddress { get; }
    
    /// <summary>
    /// Gets the list of one or more offsets for pointer resolution
    /// </summary>
    public ImmutableArray<int> Offsets { get; }

    bool IMemoryAddress.IsStatic => false;
    
    public DynamicAddress(uint baseAddress, ImmutableArray<int> offsets) {
        if (offsets.IsEmpty)
            throw new ArgumentException("Offsets cannot be empty.", nameof(offsets));
        
        this.BaseAddress = baseAddress;
        this.Offsets = offsets;
    }
    
    public DynamicAddress(uint baseAddress, IEnumerable<int> offsets) : this(baseAddress, offsets.ToImmutableArray()) {
    }

    /// <summary>
    /// Tries to resolve this pointer and give the address of the effective value. The given address may be a null pointer when this method returns true
    /// </summary>
    /// <returns>The final pointer which points to a useful value. Or returns null when a dereferenced pointer is invalid</returns>
    public Task<uint?> TryResolve(IConsoleConnection connection) => connection.ResolvePointer(this);

    public bool Equals(DynamicAddress? other) {
        if (other == null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return this.BaseAddress == other.BaseAddress && this.Offsets.SequenceEqual(other.Offsets);
    }

    public override bool Equals(object? obj) {
        return ReferenceEquals(this, obj) || obj is DynamicAddress other && this.Equals(other);
    }

    public override int GetHashCode() {
        return HashCode.Combine(this.BaseAddress, this.Offsets);
    }

    public override string ToString() {
        StringBuilder sb = new StringBuilder();
        sb.Append(this.BaseAddress.ToString("X8"));
        foreach (int offset in this.Offsets) {
            sb.Append("->").Append(offset.ToString("X"));
        }

        return sb.ToString(); 
    }
}