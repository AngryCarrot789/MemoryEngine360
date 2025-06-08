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

namespace MemEngine360.BaseFrontEnd.Commands;

public class BaseScanCommandUsage : EngineButtonCommandUsage {
    public BaseScanCommandUsage(string commandId) : base(commandId) {
        
    }

    protected override void OnEngineChanged(MemoryEngine? oldEngine, MemoryEngine? newEngine) {
        base.OnEngineChanged(oldEngine, newEngine);
        if (oldEngine != null) {
            oldEngine.ScanningProcessor.IsScanningChanged -= this.OnIsScanningChanged;
            oldEngine.ScanningProcessor.HasFirstScanChanged -= this.OnHasFirstScanChanged;
            oldEngine.IsBusyChanged -= this.OnIsBusyChanged;
            oldEngine.ConnectionChanged -= this.OnConnectionChanged;
        }

        if (newEngine != null) {
            newEngine.ScanningProcessor.IsScanningChanged += this.OnIsScanningChanged;
            newEngine.ScanningProcessor.HasFirstScanChanged += this.OnHasFirstScanChanged;
            newEngine.IsBusyChanged += this.OnIsBusyChanged;
            newEngine.ConnectionChanged += this.OnConnectionChanged;
        }
    }

    private void OnConnectionChanged(MemoryEngine sender, ulong frame, IConsoleConnection? oldC, IConsoleConnection? newC, ConnectionChangeCause cause) {
        this.UpdateCanExecuteLater();
    }

    private void OnIsScanningChanged(ScanningProcessor sender) {
        this.UpdateCanExecuteLater();
    }
    
    private void OnIsBusyChanged(MemoryEngine sender) {
        this.UpdateCanExecuteLater();
    }
    
    private void OnHasFirstScanChanged(ScanningProcessor sender) {
        this.UpdateCanExecuteLater();
    }
}

public class FirstScanCommandUsage() : BaseScanCommandUsage("commands.memengine.FirstScanCommand");
public class NextScanCommandUsage() : BaseScanCommandUsage("commands.memengine.NextScanCommand");
public class ResetScanCommandUsage() : BaseScanCommandUsage("commands.memengine.ResetScanCommand");