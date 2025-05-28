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

using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Notifications;

namespace MemEngine360.Engine;

/// <summary>
/// An abstraction around the MemEngine360 main window
/// </summary>
public interface IMemEngineUI : ILatestActivityView {
    public static readonly DataKey<IMemEngineUI> MemUIDataKey = DataKey<IMemEngineUI>.Create("IMemEngineUI");
    
    /// <summary>
    /// Gets the memory engine
    /// </summary>
    MemoryEngine360 MemoryEngine360 { get; }
    
    /// <summary>
    /// Gets the memory engine window's notification manager
    /// </summary>
    NotificationManager NotificationManager { get; }
    
    /// <summary>
    /// Gets our top-level menu
    /// </summary>
    TopLevelMenuRegistry TopLevelMenuRegistry { get; }

    /// <summary>
    /// Gets the context entry that contains a list of remote command entries
    /// </summary>
    ContextEntryGroup RemoteCommandsContextEntry { get; }

    /// <summary>
    /// Gets the scan result list selection manager
    /// </summary>
    IListSelectionManager<ScanResultViewModel> ScanResultSelectionManager { get; }
    
    /// <summary>
    /// Gets the saved address list selection manager
    /// </summary>
    IListSelectionManager<IAddressTableEntryUI> AddressTableSelectionManager { get; }

    /// <summary>
    /// Gets or sets if the activity list is visible in the UI
    /// </summary>
    bool IsActivtyListVisible { get; set; }
}