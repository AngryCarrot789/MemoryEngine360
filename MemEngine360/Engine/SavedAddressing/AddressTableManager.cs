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

namespace MemEngine360.Engine.SavedAddressing;

public class AddressTableManager {
    /// <summary>
    /// Gets the folder that stores this ATM's layer hierarchy
    /// </summary>
    public AddressTableGroupEntry RootEntry { get; }

    /// <summary>
    /// Gets the memory engine associated with this address table manager
    /// </summary>
    public MemoryEngine MemoryEngine { get; }
    
    public AddressTableManager(MemoryEngine memoryEngine) {
        this.MemoryEngine = memoryEngine;
        this.RootEntry = AddressTableGroupEntry.InternalCreateRoot(this);
    }

    public IEnumerable<AddressTableEntry> GetAllAddressEntries() {
        List<AddressTableEntry> entries = new List<AddressTableEntry>();
        this.RootEntry.GetAllEntries(entries);
        return entries;
    }
}