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

using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity;

namespace MemEngine360.BaseFrontEnd.Commands;

/// <summary>
/// Base class for a button command usage whose executability changes when the engine's connection changes
/// </summary>
public abstract class MemEngineConnectionReliantButtonCommandUsage : MemEngineButtonCommandUsage {
    protected MemEngineConnectionReliantButtonCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnEngineChanged(MemoryEngine360? oldEngine, MemoryEngine360? newEngine) {
        if (oldEngine != null)
            oldEngine.ConnectionChanged -= this.OnConnectionChanged;
        
        if (newEngine != null)
            newEngine.ConnectionChanged += this.OnConnectionChanged;
    }
    
    protected virtual void OnConnectionChanged(MemoryEngine360 sender, ulong frame, IConsoleConnection? oldC, IConsoleConnection? newC, ConnectionChangeCause cause) {
        this.UpdateCanExecuteLater();
    }
}

public class RefreshSavedAddressesCommandUsage : MemEngineConnectionReliantButtonCommandUsage {
    public RefreshSavedAddressesCommandUsage() : base("commands.memengine.RefreshSavedAddressesCommand") {
    }

    protected override void OnEngineChanged(MemoryEngine360? oldEngine, MemoryEngine360? newEngine) {
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

public class AddSelectedScanResultsToSavedAddressListCommandUsage : MemUIButtonCommandUsage {
    public AddSelectedScanResultsToSavedAddressListCommandUsage() : base("commands.memengine.AddSelectedScanResultsToSavedAddressListCommand") {
    }

    protected override void OnEngineChanged(IMemEngineUI? oldUI, IMemEngineUI? newUI) {
        base.OnEngineChanged(oldUI, newUI);
        if (oldUI != null)
            oldUI.ScanResultSelectionManager.LightSelectionChanged -= this.OnSelectionChanged;
        if (newUI != null)
            newUI.ScanResultSelectionManager.LightSelectionChanged += this.OnSelectionChanged;
    }

    private void OnSelectionChanged(ILightSelectionManager<ScanResultViewModel> sender) {
        this.UpdateCanExecuteLater();
    }
}

public class SelectRangeFromMemoryRegionCommandUsage : MemEngineConnectionReliantButtonCommandUsage {
    public SelectRangeFromMemoryRegionCommandUsage() : base("commands.memengine.SelectRangeFromMemoryRegionCommand") {
    }

    protected override void OnUpdateForCanExecuteState(Executability state) {
        base.OnUpdateForCanExecuteState(state);
        this.Button.IsVisible = state != Executability.Invalid;
    }
}
public class ShowMemoryCommandUsage : MemEngineConnectionReliantButtonCommandUsage {
    public ShowMemoryCommandUsage() : base("commands.memengine.ShowMemoryCommand") {
    }
}

public class ResetScanOptionsCommandUsage : MemEngineConnectionReliantButtonCommandUsage {
    public ResetScanOptionsCommandUsage() : base("commands.memengine.ResetScanOptionsCommand") {
    }

    protected override void OnEngineChanged(MemoryEngine360? oldEngine, MemoryEngine360? newEngine) {
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