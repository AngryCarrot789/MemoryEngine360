// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

namespace MemEngine360.Connections;

public readonly struct MemoryRegion {
    /// <summary>
    /// The address of the memory region
    /// </summary>
    public readonly uint BaseAddress;
    
    /// <summary>
    /// The size of the memory region
    /// </summary>
    public readonly uint Size;
    
    /// <summary>
    /// The protection flags the region has, e.g. ReadWrite, Execute, etc.
    /// </summary>
    public readonly uint Protection;
    
    /// <summary>
    /// Optional address of this region in physical memory... probably
    /// </summary>
    public readonly uint PhysicalAddress;
    
    public uint EndAddress => this.BaseAddress + this.Size;

    public MemoryRegion(uint baseAddress, uint size, uint protection, uint physicalAddress) {
        this.BaseAddress = baseAddress;
        this.Size = size;
        this.Protection = protection;
        this.PhysicalAddress = physicalAddress;
    }
}