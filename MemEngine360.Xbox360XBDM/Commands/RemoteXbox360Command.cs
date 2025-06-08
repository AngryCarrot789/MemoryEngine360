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

using MemEngine360.Commands;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Xbox360XBDM.Commands;

public abstract class RemoteXbox360Command : BaseRemoteConsoleCommand {
    protected override async Task<bool> TryBeginExecuteAsync(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        if (!(connection is IXbdmConnection)) {
            await IMessageDialogService.Instance.ShowMessage("Not an xbox console", "This command cannot be used because we are not connected to an xbox 360");
            return false;
        }

        return true;
    }

    protected sealed override Task ExecuteRemoteCommandInActivity(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        return this.ExecuteRemoteCommandInActivity(engine, (IXbdmConnection) connection, e);
    }

    protected abstract Task ExecuteRemoteCommandInActivity(MemoryEngine engine, IXbdmConnection connection, CommandEventArgs e);
}

public class EjectDiskTrayCommand : RemoteXbox360Command {
    protected override string ActivityText => "Ejecting disk tray...";

    protected override async Task ExecuteRemoteCommandInActivity(MemoryEngine engine, IXbdmConnection connection, CommandEventArgs e) {
        await connection.OpenDiskTray();
    }
}