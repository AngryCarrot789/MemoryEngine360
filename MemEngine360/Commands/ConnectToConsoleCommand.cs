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
using MemEngine360.Connections.XBOX;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands;

public class ConnectToConsoleCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? memUi)) {
            return Executability.Invalid;
        }

        return !memUi.MemoryEngine360.IsConnectionBusy ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? memUi)) {
            return;
        }

        using IDisposable? token = await memUi.MemoryEngine360.BeginBusyOperationActivityAsync("Connect to console");
        if (token == null) {
            return;
        }

        IConsoleConnection? existingConnection = memUi.MemoryEngine360.GetConnection(token);
        if (existingConnection != null) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to a console. Close existing connection first?", MessageBoxButton.OKCancel, MessageBoxResult.OK);
            if (result != MessageBoxResult.OK) {
                return;
            }

            existingConnection.Dispose();
            memUi.MemoryEngine360.SetConnection(token, null, ConnectionChangeCause.User);

            if (ILatestActivityView.DataKey.TryGetContext(e.ContextData, out ILatestActivityView? view))
                view.Activity = "Disconnected from console";
            
            memUi.RemoteCommandsContextEntry.Items.Clear();
        }

        IConsoleConnection? connection = await ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionService>().OpenDialogAndConnect();
        if (connection != null) {
            memUi.MemoryEngine360.SetConnection(token, connection, ConnectionChangeCause.User);
            if (ILatestActivityView.DataKey.TryGetContext(e.ContextData, out ILatestActivityView? view))
                view.Activity = "Connected to console.";

            if (connection is IXbox360Connection) {
                ContextEntryGroup entry = memUi.RemoteCommandsContextEntry;
                entry.Items.Add(new CommandContextEntry("commands.memengine.remote.ListHelpCommand", "List all commands in popup"));
                entry.Items.Add(new CommandContextEntry("commands.memengine.remote.ShowConsoleInfoCommand", "Console info"));
                entry.Items.Add(new CommandContextEntry("commands.memengine.remote.ShowXbeInfoCommand", "Show XBE info"));
                entry.Items.Add(new CommandContextEntry("commands.memengine.remote.MemProtectionCommand", "Show Memory Regions"));
                entry.Items.Add(new SeparatorEntry());
                entry.Items.Add(new CommandContextEntry("commands.memengine.remote.EjectDiskTrayCommand", "Open Disk Tray"));
                entry.Items.Add(new CommandContextEntry("commands.memengine.remote.DebugFreezeCommand", "Debug Freeze"));
                entry.Items.Add(new CommandContextEntry("commands.memengine.remote.DebugUnfreezeCommand", "Debug Un-freeze"));
                entry.Items.Add(new CommandContextEntry("commands.memengine.remote.SoftRebootCommand", "Soft Reboot (restart title)"));
                entry.Items.Add(new CommandContextEntry("commands.memengine.remote.ColdRebootCommand", "Cold Reboot"));
                entry.Items.Add(new CommandContextEntry("commands.memengine.remote.ShutdownCommand", "Shutdown"));
            }
        }
    }
}