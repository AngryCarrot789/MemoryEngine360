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

using System.Collections.ObjectModel;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Shortcuts;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands;

public abstract class BaseRemoteConsoleCommand : BaseMemoryEngineCommand {
    protected abstract string ActivityText { get; }

    protected sealed override Executability CanExecuteCore(MemoryEngine engine, CommandEventArgs e) {
        IConsoleConnection? connection = engine.Connection;
        return connection != null ? this.CanExecuteCore(engine, connection, e) : Executability.ValidButCannotExecute;
    }

    protected virtual Executability CanExecuteCore(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        return Executability.Valid;
    }

    protected virtual Task<bool> TryBeginExecuteAsync(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        return Task.FromResult(true);
    }

    protected sealed override async Task ExecuteCommandAsync(MemoryEngine engine, CommandEventArgs e) {
        using IDisposable? token = await engine.BeginBusyOperationActivityAsync(this.ActivityText);
        if (token == null) {
            return;
        }

        ConnectionChangeCause likelyCause = ConnectionChangeCause.LostConnection;
        IConsoleConnection? connection = engine.Connection;
        if (connection == null || !connection.IsConnected) {
            IEnumerable<ShortcutEntry> scList = ShortcutManager.Instance.GetShortcutsByCommandId("commands.memengine.OpenConsoleConnectionDialogCommand") ?? ReadOnlyCollection<ShortcutEntry>.Empty;
            string shortcuts = string.Join(Environment.NewLine, scList.Select(x => x.Shortcut.ToString()));
            if (!string.IsNullOrEmpty(shortcuts))
                shortcuts = " Use the shortcut(s) to connect: " + Environment.NewLine + shortcuts;

            engine.CheckConnection(token);
            await IMessageDialogService.Instance.ShowMessage("Disconnected", "Not connected to a console." + shortcuts);
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
                    await this.ExecuteRemoteCommandInActivity(engine, connection, e);
                }
                catch (IOException ex) {
                    likelyCause = ConnectionChangeCause.ConnectionError;
                    await IMessageDialogService.Instance.ShowMessage("IO Error", "An IO error occurred", ex.Message);
                }
                catch (TimeoutException ex) {
                    await IMessageDialogService.Instance.ShowMessage("Timed out", "Connection timed out", ex.Message);
                }
                catch (Exception ex) {
                    await IMessageDialogService.Instance.ShowMessage("Unexpected Error", "Error while executing remote command", ex.GetToString());
                }
            });
        }

        engine.CheckConnection(token, likelyCause);
    }

    protected abstract Task ExecuteRemoteCommandInActivity(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e);
}

public class ShutdownCommand : BaseRemoteConsoleCommand {
    protected override string ActivityText => "Shutting down console...";

    protected override Executability CanExecuteCore(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        Executability canExec = base.CanExecuteCore(engine, connection, e);
        return canExec != Executability.Valid
            ? canExec
            : connection.HasFeature<IFeaturePowerFunctions>()
                ? Executability.Valid
                : Executability.ValidButCannotExecute;
    }

    protected override async Task<bool> TryBeginExecuteAsync(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        if (connection.HasFeature<IFeaturePowerFunctions>())
            return true;
        await IMessageDialogService.Instance.ShowMessage("No power functions", "This connection cannot trigger power functions");
        return false;
    }

    protected override async Task ExecuteRemoteCommandInActivity(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        await connection.GetFeatureOrDefault<IFeaturePowerFunctions>()!.ShutdownConsole();
    }
}

public class SoftRebootCommand : BaseRemoteConsoleCommand {
    protected override string ActivityText => "Rebooting title...";

    protected override async Task<bool> TryBeginExecuteAsync(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        if (connection.HasFeature<IFeaturePowerFunctions>())
            return true;
        await IMessageDialogService.Instance.ShowMessage("No power functions", "This connection cannot trigger power functions");
        return false;
    }

    protected override async Task ExecuteRemoteCommandInActivity(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        await connection.GetFeatureOrDefault<IFeaturePowerFunctions>()!.RebootConsole(false);
    }
}

public class ColdRebootCommand : BaseRemoteConsoleCommand {
    protected override string ActivityText => "Rebooting console...";

    protected override async Task<bool> TryBeginExecuteAsync(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        if (connection.HasFeature<IFeaturePowerFunctions>())
            return true;
        await IMessageDialogService.Instance.ShowMessage("No power functions", "This connection cannot trigger power functions");
        return false;
    }

    protected override async Task ExecuteRemoteCommandInActivity(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        await connection.GetFeatureOrDefault<IFeaturePowerFunctions>()!.RebootConsole(true);
    }
}

public class DebugFreezeCommand : BaseRemoteConsoleCommand {
    protected override string ActivityText => "Freezing console...";

    protected override async Task<bool> TryBeginExecuteAsync(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        if (!connection.HasFeature<IFeatureIceCubes>()) {
            await IMessageDialogService.Instance.ShowMessage("Not freezable", "This console does not support freezing");
            return false;
        }

        return true;
    }

    protected override async Task ExecuteRemoteCommandInActivity(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        await connection.GetFeatureOrDefault<IFeatureIceCubes>()!.DebugFreeze();
    }
}

public class DebugUnfreezeCommand : BaseRemoteConsoleCommand {
    protected override string ActivityText => "Unfreezing console...";

    protected override async Task<bool> TryBeginExecuteAsync(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        if (!connection.HasFeature<IFeatureIceCubes>()) {
            await IMessageDialogService.Instance.ShowMessage("Not freezable", "This console does not support freezing");
            return false;
        }

        return true;
    }

    protected override async Task ExecuteRemoteCommandInActivity(MemoryEngine engine, IConsoleConnection connection, CommandEventArgs e) {
        await connection.GetFeatureOrDefault<IFeatureIceCubes>()!.DebugUnFreeze();
    }
}