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

using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Engine.View;

/// <summary>
/// Represents the persistent state of the address table
/// </summary>
public sealed class AddressTablePanelViewState {
    public MemoryEngine Engine { get; }
    
    /// <summary>
    /// Gets the list of selected address table entries
    /// </summary>
    public ObservableList<BaseAddressTableEntry> SelectedEntries { get; }
    
    public AddressTablePanelViewState(MemoryEngine engine) {
        this.Engine = engine;
    }
    
    public static AddressTablePanelViewState GetInstance(MemoryEngine engine) {
        return ((IComponentManager) engine).GetOrCreateComponent((t) => new AddressTablePanelViewState((MemoryEngine) t));
    }
}