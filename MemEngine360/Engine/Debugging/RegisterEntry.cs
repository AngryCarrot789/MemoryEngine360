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

using MemEngine360.Connections.Features;

namespace MemEngine360.Engine.Debugging;

/// <summary>
/// A general purpose register entry in the debugger's register list
/// </summary>
public abstract class RegisterEntry {
    /// <summary>Gets the readable name of this register</summary>
    public string Name { get; }
    
    /// <summary>
    /// A general purpose register entry in the debugger's register list
    /// </summary>
    protected RegisterEntry(string name) {
        ArgumentException.ThrowIfNullOrEmpty(name);
        this.Name = name;
    }
}

public class RegisterEntry32(string name, uint value) : RegisterEntry(name) {
    public readonly uint Value = value;
}

public class RegisterEntry64(string name, ulong value) : RegisterEntry(name) {
    public readonly ulong Value = value;
}

public class RegisterEntryDouble(string name, ulong value) : RegisterEntry(name) {
    public readonly ulong Value = value;
}

public class RegisterEntryVector(string name, Vector128 value) : RegisterEntry(name) {
    public readonly Vector128 Value = value;
}
