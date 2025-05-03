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

using System.Collections.ObjectModel;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Shortcuts;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands;

public abstract class BaseRemoteConsoleCommand : BaseMemoryEngineCommand {
    protected abstract string ActivityText { get; }

    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        return engine.Connection != null ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected virtual Task<bool> TryBeginExecuteAsync(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        return Task.FromResult(true);
    }

    protected sealed override async Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        engine.CheckConnection();
        using IDisposable? token = engine.BeginBusyOperation();
        if (token == null) {
            await IMessageDialogService.Instance.ShowMessage("Disconnected", "Connection is busy elsewhere");
            return;
        }

        IConsoleConnection? connection = engine.Connection;
        if (connection == null || !connection.IsConnected) {
            IEnumerable<ShortcutEntry> scList = ShortcutManager.Instance.GetShortcutsByCommandId("commands.memengine.OpenConsoleConnectionDialogCommand") ?? ReadOnlyCollection<ShortcutEntry>.Empty;
            string shortcuts = string.Join(Environment.NewLine, scList.Select(x => x.Shortcut.ToString()));
            if (!string.IsNullOrEmpty(shortcuts))
                shortcuts = " Use the shortcut(s) to connect: " + Environment.NewLine + shortcuts;

            await IMessageDialogService.Instance.ShowMessage("Disconnected", "Not connected to a console." + shortcuts);
        }
        else if (engine.ScanningProcessor.IsScanning) {
            await IMessageDialogService.Instance.ShowMessage("Disconnected", "Scan in progress");
        }
        else if (await this.TryBeginExecuteAsync(engine, connection, e)) {
            // we won't bother using the async versions because they will most likely have an
            // activity running (e.g. scan progress) and at the moment there's no list of
            // activities displayable in the main UI (unimplemented but possible)
            await ActivityManager.Instance.RunTask(async () => {
                IActivityProgress prog = ActivityManager.Instance.GetCurrentProgressOrEmpty();
                prog.Caption = "Remote Command";
                prog.Text = this.ActivityText;
                try {
                    await this.ExecuteRemoteCommand(engine, connection, e);
                }
                catch (Exception exception) {
                    await IMessageDialogService.Instance.ShowMessage("Error", "Error while executing remote command", exception.GetToString());
                }
            });

            engine.CheckConnection();
        }
    }

    protected abstract Task ExecuteRemoteCommand(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e);
}

public class ShutdownCommand : BaseRemoteConsoleCommand {
    protected override string ActivityText => "Shutting down console...";

    protected override async Task ExecuteRemoteCommand(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        await connection.ShutdownConsole();
    }
}

public class SoftRebootCommand : BaseRemoteConsoleCommand {
    protected override string ActivityText => "Rebooting title...";

    protected override async Task ExecuteRemoteCommand(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        await connection.RebootConsole(false);
    }
}

public class ColdRebootCommand : BaseRemoteConsoleCommand {
    protected override string ActivityText => "Rebooting console...";

    protected override async Task ExecuteRemoteCommand(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        await connection.RebootConsole(true);
    }
}