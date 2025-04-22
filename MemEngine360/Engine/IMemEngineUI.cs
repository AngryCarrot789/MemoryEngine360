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

using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Engine;

public interface IMemEngineUI {
    public static readonly DataKey<IMemEngineUI> DataKey = DataKey<IMemEngineUI>.Create("IMemEngineUI");

    /// <summary>
    /// Gets the memory engine
    /// </summary>
    MemoryEngine360 MemoryEngine360 { get; }

    /// <summary>
    /// Gets the scan result list selection manager
    /// </summary>
    IListSelectionManager<ScanResultViewModel> ScanResultSelectionManager { get; }
    
    /// <summary>
    /// Gets the saved address list selection manager
    /// </summary>
    IListSelectionManager<SavedAddressViewModel> SavedAddressesSelectionManager { get; }
}