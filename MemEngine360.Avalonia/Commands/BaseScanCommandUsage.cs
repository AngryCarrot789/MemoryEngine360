using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.Avalonia.CommandUsages;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Avalonia.Commands;

public class BaseScanCommandUsage : SimpleButtonCommandUsage {
    private MemoryEngine360? engine;
    
    public BaseScanCommandUsage(string commandId) : base(commandId) {
        
    }

    protected override void OnContextChanged() {
        base.OnContextChanged();
        MemoryEngine360? newEngine = null;
        bool hasEngine = this.GetContextData() is IContextData data && MemoryEngine360.DataKey.TryGetContext(data, out newEngine);
        if (hasEngine && this.engine == newEngine) {
            return;
        }
        
        if (this.engine != null) {
            this.engine.ScanningProcessor.IsScanningChanged -= this.OnIsScanningChanged;
            this.engine.ScanningProcessor.HasFirstScanChanged -= this.OnHasFirstScanChanged;
            this.engine.ConnectionChanged -= this.OnConnectionChanged;
        }

        if ((this.engine = newEngine) != null) {
            this.engine.ScanningProcessor.IsScanningChanged += this.OnIsScanningChanged;
            this.engine.ScanningProcessor.HasFirstScanChanged += this.OnHasFirstScanChanged;
            this.engine.ConnectionChanged += this.OnConnectionChanged;
        }
    }

    private void OnConnectionChanged(MemoryEngine360 sender, IConsoleConnection? oldconnection, IConsoleConnection? newconnection) {
        this.UpdateCanExecuteLater();
    }

    private void OnIsScanningChanged(ScanningProcessor sender) {
        this.UpdateCanExecuteLater();
    }
    
    private void OnHasFirstScanChanged(ScanningProcessor sender) {
        this.UpdateCanExecuteLater();
    }
}

public class FirstScanCommandUsage() : BaseScanCommandUsage("commands.memengine.FirstScanCommand");
public class NextScanCommandUsage() : BaseScanCommandUsage("commands.memengine.NextScanCommand");
public class ResetScanCommandUsage() : BaseScanCommandUsage("commands.memengine.ResetScanCommand");