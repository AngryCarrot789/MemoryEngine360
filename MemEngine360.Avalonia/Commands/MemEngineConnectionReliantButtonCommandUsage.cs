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
using PFXToolKitUI.Interactivity;

namespace MemEngine360.Avalonia.Commands;

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
    
    protected virtual void OnConnectionChanged(MemoryEngine360 sender, IConsoleConnection? oldConnection, IConsoleConnection? newConnection, ConnectionChangeCause cause) {
        this.UpdateCanExecuteLater();
    }
}

public class RefreshSavedAddressesCommandUsage : MemEngineConnectionReliantButtonCommandUsage {
    public RefreshSavedAddressesCommandUsage() : base("commands.memengine.RefreshSavedAddressesCommand") {
    }
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