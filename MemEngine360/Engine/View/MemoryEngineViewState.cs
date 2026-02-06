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
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Engine.View;

public sealed class MemoryEngineViewState {
    /// <summary>
    /// Gets the engine model instance
    /// </summary>
    public MemoryEngine Engine { get; }

    /// <summary>
    /// Gets the menu registry for the engine window
    /// </summary>
    public TopLevelMenuRegistry TopLevelMenuRegistry { get; } = new TopLevelMenuRegistry();

    /// <summary>
    /// Returns the selection model for the scan results
    /// </summary>
    public ListSelectionModel<ScanResultViewModel> SelectedScanResults { get; }

    /// <summary>
    /// Returns the selection model for the saved address table
    /// </summary>
    public TreeSelectionModel<BaseAddressTableEntry> AddressTableSelectionManager { get; }

    /// <summary>
    /// Gets or sets if the activity list is visible or not
    /// </summary>
    public bool IsActivityListVisible {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsActivityListVisibleChanged);
    }

    /// <summary>
    /// Fired when someone requests for the engine window to be focused
    /// </summary>
    public event EventHandler? RequestWindowFocus;

    /// <summary>
    /// Requests a saved address to be focused in the UI. This will also select the item
    /// </summary>
    public event EventHandler<BaseAddressTableEntry>? RequestFocusOnSavedAddress;

    public event EventHandler? IsActivityListVisibleChanged;
    
    private MemoryEngineViewState(MemoryEngine engine) {
        this.Engine = engine;
        this.SelectedScanResults = new ListSelectionModel<ScanResultViewModel>(this.Engine.ScanningProcessor.ScanResults);

        AddressTableManager atm = engine.AddressTableManager;
        this.AddressTableSelectionManager = new TreeSelectionModel<BaseAddressTableEntry>(
            atm.RootEntry,
            static arg => arg.AddressTableManager != null,
            static arg => arg.Parent,
            static arg => arg is AddressTableGroupEntry g ? g.Items : null);
    }

    public void RaiseRequestWindowFocus() => this.RequestWindowFocus?.Invoke(this, EventArgs.Empty);

    public void RaiseRequestFocusOnSavedAddress(BaseAddressTableEntry entry) {
        ArgumentNullException.ThrowIfNull(entry);
        this.AddressTableSelectionManager.Select(entry);
        this.RequestFocusOnSavedAddress?.Invoke(this, entry);
    }

    /// <summary>
    /// Gets the singleton view state associated with the memory engine model. Only one view per model is supported
    /// </summary>
    public static MemoryEngineViewState GetInstance(MemoryEngine engine) {
        return ((IComponentManager) engine).GetOrCreateComponent((t) => new MemoryEngineViewState((MemoryEngine) t));
    }
}