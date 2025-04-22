using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.Avalonia.CommandUsages;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Avalonia.Commands;

public class BaseScanCommandUsage : MemEngineButtonCommandUsage {
    public BaseScanCommandUsage(string commandId) : base(commandId) {
        
    }

    protected override void OnEngineChanged(MemoryEngine360? oldEngine, MemoryEngine360? newEngine) {
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

    private void OnConnectionChanged(MemoryEngine360 sender, IConsoleConnection? oldconnection, IConsoleConnection? newconnection) {
        this.UpdateCanExecuteLater();
    }

    private void OnIsScanningChanged(ScanningProcessor sender) {
        this.UpdateCanExecuteLater();
    }
    
    private void OnIsBusyChanged(MemoryEngine360 sender) {
        this.UpdateCanExecuteLater();
    }
    
    private void OnHasFirstScanChanged(ScanningProcessor sender) {
        this.UpdateCanExecuteLater();
    }
}

public class FirstScanCommandUsage() : BaseScanCommandUsage("commands.memengine.FirstScanCommand");
public class NextScanCommandUsage() : BaseScanCommandUsage("commands.memengine.NextScanCommand");
public class ResetScanCommandUsage() : BaseScanCommandUsage("commands.memengine.ResetScanCommand");