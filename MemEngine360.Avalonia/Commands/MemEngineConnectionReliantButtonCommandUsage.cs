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

    protected override void OnEngineChanged(MemoryEngine360? oldEngine, MemoryEngine360? newEngine) {
        base.OnEngineChanged(oldEngine, newEngine);
        if (oldEngine != null)
            oldEngine.IsBusyChanged -= this.ConnectionOnIsBusyChanged;
        if (newEngine != null)
            newEngine.IsBusyChanged += this.ConnectionOnIsBusyChanged;
    }

    private void ConnectionOnIsBusyChanged(MemoryEngine360 sender) {
        this.UpdateCanExecuteLater();
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