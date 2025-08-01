﻿// 
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

namespace MemEngine360.Engine.Debugging;

public class FunctionCallEntry {
    /// <summary>
    /// Gets the name of the module which this functions exists in
    /// </summary>
    public string ModuleName { get; }
    
    /// <summary>
    /// Gets the address of the function, relative to the owner module
    /// </summary>
    public uint Address { get; }

    /// <summary>
    /// Gets the size of the function
    /// </summary>
    public uint Size { get; }

    public readonly ulong UnwindInfo;

    public FunctionCallEntry(string moduleName, uint address, uint size, ulong UnwindInfo) {
        this.ModuleName = moduleName;
        this.Address = address;
        this.Size = size;
        this.UnwindInfo = UnwindInfo;
    }
}