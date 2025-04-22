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