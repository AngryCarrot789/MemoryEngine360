using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public abstract class RemoteXboxCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        return engine.Connection != null ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        if (engine.Connection != null && engine.Connection.IsConnected && !engine.IsConnectionBusy) {
            await this.ExecuteRemoteCommand(engine, engine.Connection, e);
        }
    }

    protected abstract ValueTask ExecuteRemoteCommand(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e);
}

public class EjectDiskTrayCommand : RemoteXboxCommand {
    protected override ValueTask ExecuteRemoteCommand(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        return connection.OpenDiskTray();
    }
}

public class ShutdownCommand : RemoteXboxCommand {
    protected override ValueTask ExecuteRemoteCommand(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        return connection.ShutdownConsole();
    }
}

public class SoftRebootCommand : RemoteXboxCommand {
    protected override ValueTask ExecuteRemoteCommand(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        return connection.RebootConsole(false);
    }
}

public class ColdRebootCommand : RemoteXboxCommand {
    protected override ValueTask ExecuteRemoteCommand(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        return connection.RebootConsole(true);
    }
}

public class DebugFreezeCommand : RemoteXboxCommand {
    protected override ValueTask ExecuteRemoteCommand(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        return connection.DebugFreeze();
    }
}

public class DebugUnfreezeCommand : RemoteXboxCommand {
    protected override ValueTask ExecuteRemoteCommand(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        return connection.DebugUnFreeze();
    }
}