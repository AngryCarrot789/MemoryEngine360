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

using MemEngine360.Commands;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Xbox360XBDM.Commands;

public abstract class RemoteXboxCommand : BaseRemoteConsoleCommand {
    protected override async Task<bool> TryBeginExecuteAsync(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        if (!(connection is IXbox360Connection)) {
            await IMessageDialogService.Instance.ShowMessage("Not an xbox console", "This command cannot be used because we are not connected to an xbox 360");
            return false;
        }

        return true;
    }

    protected sealed override Task ExecuteRemoteCommand(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        return this.ExecuteRemoteCommand(engine, (IXbox360Connection) connection, e);
    }
    
    protected abstract Task ExecuteRemoteCommand(MemoryEngine360 engine, IXbox360Connection connection, CommandEventArgs e);
}

public class EjectDiskTrayCommand : RemoteXboxCommand {
    protected override string ActivityText => "Ejecting disk tray...";

    protected override async Task ExecuteRemoteCommand(MemoryEngine360 engine, IXbox360Connection connection, CommandEventArgs e) {
        await connection.OpenDiskTray();
    }
}

public class DebugFreezeCommand : RemoteXboxCommand {
    protected override string ActivityText => "Freezing console...";

    protected override async Task ExecuteRemoteCommand(MemoryEngine360 engine, IXbox360Connection connection, CommandEventArgs e) {
        await connection.DebugFreeze();
    }
}

public class DebugUnfreezeCommand : RemoteXboxCommand {
    protected override string ActivityText => "Unfreezing console...";

    protected override async Task ExecuteRemoteCommand(MemoryEngine360 engine, IXbox360Connection connection, CommandEventArgs e) {
        await connection.DebugUnFreeze();
    }
}