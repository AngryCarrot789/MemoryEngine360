using MemEngine360.Engine;

namespace MemEngine360.Avalonia.Commands;

public abstract class MemEngineRemoteReadButtonCommandUsage : MemEngineConnectionReliantButtonCommandUsage {
    protected MemEngineRemoteReadButtonCommandUsage(string commandId) : base(commandId) {
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

public class EjectDiskTrayCommandUsage() : MemEngineRemoteReadButtonCommandUsage("commands.memengine.remote.EjectDiskTrayCommand");
public class ShutdownCommandUsage() : MemEngineRemoteReadButtonCommandUsage("commands.memengine.remote.ShutdownCommand");
public class SoftRebootCommandUsage() : MemEngineRemoteReadButtonCommandUsage("commands.memengine.remote.SoftRebootCommand");
public class ColdRebootCommandUsage() : MemEngineRemoteReadButtonCommandUsage("commands.memengine.remote.ColdRebootCommand");
public class DebugFreezeCommandUsage() : MemEngineRemoteReadButtonCommandUsage("commands.memengine.remote.DebugFreezeCommand");
public class DebugUnfreezeCommandUsage() : MemEngineRemoteReadButtonCommandUsage("commands.memengine.remote.DebugUnfreezeCommand");
