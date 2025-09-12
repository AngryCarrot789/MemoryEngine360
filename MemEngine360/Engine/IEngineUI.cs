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
using MemEngine360.Engine.View;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.Notifications;

namespace MemEngine360.Engine;

/// <summary>
/// An abstraction around the engine window
/// </summary>
public interface IEngineUI {
    public static readonly DataKey<IEngineUI> DataKey = DataKey<IEngineUI>.Create(nameof(IEngineUI));

    /// <summary>
    /// A data key used by the connection change notification to tell whether a disconnection originated from the notification's "Disconnect" command
    /// </summary>
    public static readonly DataKey<bool> IsDisconnectFromNotification = DataKey<bool>.Create("IsDisconnectFromNotification");

    /// <summary>
    /// Gets the memory engine
    /// </summary>
    MemoryEngine MemoryEngine { get; }

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
    /// Gets the saved address list selection manager
    /// </summary>
    IListSelectionManager<IAddressTableEntryUI> AddressTableSelectionManager { get; }

    /// <summary>
    /// Gets or sets if the activity list is visible in the UI
    /// </summary>
    bool IsActivtyListVisible { get; set; }

    ListSelectionModel<ScanResultViewModel> ScanResultSelectionManager => MemoryEngineViewState.GetInstance(this.MemoryEngine).SelectedScanResults;

    /// <summary>
    /// Gets the UI component of an ATE
    /// </summary>
    IAddressTableEntryUI GetATEntryUI(BaseAddressTableEntry entry);
}