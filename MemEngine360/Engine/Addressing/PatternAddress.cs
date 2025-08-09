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

namespace MemEngine360.Engine.Addressing;

// TODO: implement

public class PatternAddress : IMemoryAddress, IEquatable<PatternAddress> {
    /// <summary>
    /// Gets the pattern
    /// </summary>
    public ImmutableArray<byte?> Pattern { get; }

    /// <summary>
    /// Gets the list of one or more offsets for pointer resolution
    /// </summary>
    public ImmutableArray<int> Offsets { get; }

    // This is stinky but it works

    /// <summary>
    /// Whether we should cache the result of the pattern scan
    /// </summary>
    public bool CachePatternAddress { get; }

    /// <summary>
    /// Gets the amount of offsets that should be used to pre-resolve the full address, and that address will be cached internally
    /// and used as the base address. This is used to optimise pointer resolution by skipping the bottom area that may never actually chance.
    /// </summary>
    public int StaticOffsetCount { get; }

    public PatternAddress(ImmutableArray<byte?> pattern, ImmutableArray<int> offsets, bool cachePatternAddress = true, int staticOffsetCount = 0) {
        if (offsets.IsEmpty)
            throw new ArgumentException("Offsets cannot be empty.", nameof(offsets));
        if (staticOffsetCount < 0)
            throw new ArgumentException("Static offset count cannot be negative", nameof(staticOffsetCount));

        this.Pattern = pattern;
        this.Offsets = offsets;
        this.StaticOffsetCount = staticOffsetCount;
        this.CachePatternAddress = cachePatternAddress;
    }

    public bool AddressEquals(IMemoryAddress other) {
        return ReferenceEquals(this, other) && (other is PatternAddress addr && this.AddressEquals(addr));
    }

    public bool AddressEquals(PatternAddress other) {
        return ReferenceEquals(this, other) || (other.Pattern == this.Pattern && other.Offsets.SequenceEqual(this.Offsets));
    }

    public bool Equals(PatternAddress? other) {
        if (other == null)
            return false;
        return this.AddressEquals(other) && this.StaticOffsetCount == other.StaticOffsetCount;
    }

    public override bool Equals(object? obj) {
        return ReferenceEquals(this, obj) || obj is PatternAddress other && this.Equals(other);
    }
}