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

using MemEngine360.Engine;
using MemEngine360.Engine.View;
using PFXToolKitUI.Interactivity.Selections;

namespace MemEngine360.BaseFrontEnd.Commands;

/// <summary>
/// Base class for a button command usage whose executability changes when the engine's connection changes
/// </summary>
public abstract class EngineConnectionReliantButtonCommandUsage : EngineButtonCommandUsage {
    protected EngineConnectionReliantButtonCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnEngineChanged(MemoryEngineViewState? oldEngineVs, MemoryEngineViewState? newEngineVs) {
        base.OnEngineChanged(oldEngineVs, newEngineVs);
        if (oldEngineVs != null)
            oldEngineVs.Engine.ConnectionChanged -= this.OnConnectionChanged;
        
        if (newEngineVs != null)
            newEngineVs.Engine.ConnectionChanged += this.OnConnectionChanged;
    }
    
    protected virtual void OnConnectionChanged(object? o, ConnectionChangedEventArgs args) {
        this.UpdateCanExecuteLater();
    }
}

public class RefreshSavedAddressesCommandUsage : EngineConnectionReliantButtonCommandUsage {
    public RefreshSavedAddressesCommandUsage() : base("commands.memengine.RefreshSavedAddressesCommand") {
    }

    protected override void OnEngineChanged(MemoryEngineViewState? oldEngineVs, MemoryEngineViewState? newEngineVs) {
        base.OnEngineChanged(oldEngineVs, newEngineVs);
        if (oldEngineVs != null) {
            oldEngineVs.Engine.ScanningProcessor.IsRefreshingAddressesChanged -= this.DoUpdate;
            oldEngineVs.Engine.ScanningProcessor.IsScanningChanged -= this.DoUpdate;
        }

        if (newEngineVs != null) {
            newEngineVs.Engine.ScanningProcessor.IsRefreshingAddressesChanged += this.DoUpdate;
            newEngineVs.Engine.ScanningProcessor.IsScanningChanged += this.DoUpdate;
        }
    }

    private void DoUpdate(object? o, EventArgs e) => this.UpdateCanExecuteLater();
}

public class AddSelectedScanResultsToSavedAddressListCommandUsage : EngineButtonCommandUsage {
    public AddSelectedScanResultsToSavedAddressListCommandUsage() : base("commands.memengine.AddSelectedScanResultsToSavedAddressListCommand") {
    }

    protected override void OnEngineChanged(MemoryEngineViewState? oldEngineVs, MemoryEngineViewState? newEngineVs) {
        base.OnEngineChanged(oldEngineVs, newEngineVs);
        if (oldEngineVs != null)
            oldEngineVs.SelectedScanResults.SelectionChanged -= this.OnSelectionChanged;
        if (newEngineVs != null)
            newEngineVs.SelectedScanResults.SelectionChanged += this.OnSelectionChanged;
    }

    private void OnSelectionChanged(object? sender, ListSelectionModelChangedEventArgs<ScanResultViewModel> e) {
        this.UpdateCanExecuteLater();
    }
}

public class SelectRangeFromRegionCommandUsage() : EngineConnectionReliantButtonCommandUsage("commands.memengine.SelectRangeFromMemoryRegionCommand");
public class ShowCommandUsage() : EngineConnectionReliantButtonCommandUsage("commands.memengine.ShowMemoryViewCommand");

public class ResetScanOptionsCommandUsage() : EngineConnectionReliantButtonCommandUsage("commands.memengine.ResetScanOptionsCommand") {
    protected override void OnEngineChanged(MemoryEngineViewState? oldEngineVs, MemoryEngineViewState? newEngineVs) {
        base.OnEngineChanged(oldEngineVs, newEngineVs);
        if (oldEngineVs != null)
            oldEngineVs.Engine.ScanningProcessor.IsScanningChanged -= this.OnIsScanningChanged;
        if (newEngineVs != null)
            newEngineVs.Engine.ScanningProcessor.IsScanningChanged += this.OnIsScanningChanged;
    }

    private void OnIsScanningChanged(object? o, EventArgs e) {
        this.UpdateCanExecuteLater();
    }
}