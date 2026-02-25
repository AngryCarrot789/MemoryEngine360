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

namespace MemEngine360.BaseFrontEnd.Commands;

public class BaseScanCommandUsage : EngineButtonCommandUsage {
    public BaseScanCommandUsage(string commandId) : base(commandId) {
        
    }

    protected override void OnEngineChanged(MemoryEngineViewState? oldEngineVs, MemoryEngineViewState? newEngineVs) {
        base.OnEngineChanged(oldEngineVs, newEngineVs);
        if (oldEngineVs != null) {
            oldEngineVs.Engine.ScanningProcessor.IsScanningChanged -= this.OnIsScanningChanged;
            oldEngineVs.Engine.ScanningProcessor.HasFirstScanChanged -= this.OnHasFirstScanChanged;
            oldEngineVs.Engine.IsBusyChanged -= this.OnIsBusyChanged;
            oldEngineVs.Engine.ConnectionChanged -= this.OnConnectionChanged;
        }

        if (newEngineVs != null) {
            newEngineVs.Engine.ScanningProcessor.IsScanningChanged += this.OnIsScanningChanged;
            newEngineVs.Engine.ScanningProcessor.HasFirstScanChanged += this.OnHasFirstScanChanged;
            newEngineVs.Engine.IsBusyChanged += this.OnIsBusyChanged;
            newEngineVs.Engine.ConnectionChanged += this.OnConnectionChanged;
        }
    }

    private void OnConnectionChanged(object? o, ConnectionChangedEventArgs args) {
        this.UpdateCanExecuteLater();
    }

    private void OnIsScanningChanged(object? o, EventArgs e) {
        this.UpdateCanExecuteLater();
    }
    
    private void OnIsBusyChanged(object? o, EventArgs e) {
        this.UpdateCanExecuteLater();
    }
    
    private void OnHasFirstScanChanged(object? o, EventArgs e) {
        this.UpdateCanExecuteLater();
    }
}

public class FirstScanCommandUsage() : BaseScanCommandUsage("commands.memengine.FirstScanCommand");
public class NextScanCommandUsage() : BaseScanCommandUsage("commands.memengine.NextScanCommand");
public class ResetScanCommandUsage() : BaseScanCommandUsage("commands.memengine.ResetScanCommand");
public class CancelScanCommandUsage() : BaseScanCommandUsage("commands.memengine.CancelScanCommand");