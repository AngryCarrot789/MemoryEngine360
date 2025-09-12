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

using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.Utils;

namespace MemEngine360.BaseFrontEnd.Commands;

/// <summary>
/// Base class for a button command usage whose executability changes when the engine's connection changes
/// </summary>
public abstract class EngineConnectionReliantButtonCommandUsage : EngineButtonCommandUsage {
    protected EngineConnectionReliantButtonCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnEngineChanged(MemoryEngine? oldEngine, MemoryEngine? newEngine) {
        if (oldEngine != null)
            oldEngine.ConnectionChanged -= this.OnConnectionChanged;
        
        if (newEngine != null)
            newEngine.ConnectionChanged += this.OnConnectionChanged;
    }
    
    protected virtual void OnConnectionChanged(MemoryEngine sender, ulong frame, IConsoleConnection? oldC, IConsoleConnection? newC, ConnectionChangeCause cause) {
        this.UpdateCanExecuteLater();
    }
}

public class RefreshSavedAddressesCommandUsage : EngineConnectionReliantButtonCommandUsage {
    public RefreshSavedAddressesCommandUsage() : base("commands.memengine.RefreshSavedAddressesCommand") {
    }

    protected override void OnEngineChanged(MemoryEngine? oldEngine, MemoryEngine? newEngine) {
        base.OnEngineChanged(oldEngine, newEngine);
        if (oldEngine != null) {
            oldEngine.ScanningProcessor.IsRefreshingAddressesChanged -= this.DoUpdate;
            oldEngine.ScanningProcessor.IsScanningChanged -= this.DoUpdate;
        }

        if (newEngine != null) {
            newEngine.ScanningProcessor.IsRefreshingAddressesChanged += this.DoUpdate;
            newEngine.ScanningProcessor.IsScanningChanged += this.DoUpdate;
        }
    }

    private void DoUpdate(ScanningProcessor sender) => this.UpdateCanExecuteLater();
}

public class AddSelectedScanResultsToSavedAddressListCommandUsage : EngineUIButtonCommandUsage {
    public AddSelectedScanResultsToSavedAddressListCommandUsage() : base("commands.memengine.AddSelectedScanResultsToSavedAddressListCommand") {
    }

    protected override void OnEngineChanged(IEngineUI? oldUI, IEngineUI? newUI) {
        base.OnEngineChanged(oldUI, newUI);
        if (oldUI != null)
            oldUI.ScanResultSelectionManager.SelectionChanged -= this.OnSelectionChanged;
        if (newUI != null)
            newUI.ScanResultSelectionManager.SelectionChanged += this.OnSelectionChanged;
    }

    private void OnSelectionChanged(ListSelectionModel<ScanResultViewModel> sender, IList<IntRange> addedIndices, IList<IntRange> removedIndices) {
        this.UpdateCanExecuteLater();
    }
}

public class SelectRangeFromRegionCommandUsage() : EngineConnectionReliantButtonCommandUsage("commands.memengine.SelectRangeFromMemoryRegionCommand");
public class ShowCommandUsage() : EngineConnectionReliantButtonCommandUsage("commands.memengine.ShowMemoryViewCommand");

public class ResetScanOptionsCommandUsage() : EngineConnectionReliantButtonCommandUsage("commands.memengine.ResetScanOptionsCommand") {
    protected override void OnEngineChanged(MemoryEngine? oldEngine, MemoryEngine? newEngine) {
        base.OnEngineChanged(oldEngine, newEngine);
        if (oldEngine != null)
            oldEngine.ScanningProcessor.IsScanningChanged -= this.OnIsScanningChanged;
        if (newEngine != null)
            newEngine.ScanningProcessor.IsScanningChanged += this.OnIsScanningChanged;
    }

    private void OnIsScanningChanged(ScanningProcessor sender) {
        this.UpdateCanExecuteLater();
    }
}