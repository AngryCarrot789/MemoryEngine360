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
using PFXToolKitUI;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands;

public class OpenConsoleConnectionDialogCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? memUi)) {
            return Executability.Invalid;
        }

        return !memUi.MemoryEngine360.IsConnectionBusy ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? memUi)) {
            return;
        }

        using (IDisposable? token = await memUi.MemoryEngine360.BeginBusyOperationActivityAsync("Connect to console")) {
            if (token == null) {
                return;
            }

            IConsoleConnection? existingConnection = memUi.MemoryEngine360.GetConnection(token);
            if (existingConnection != null) {
                MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to a console. Close existing connection first?", MessageBoxButton.OKCancel, MessageBoxResult.OK);
                if (result != MessageBoxResult.OK) {
                    return;
                }

                using (ErrorList errors = new ErrorList()) {
                    try {
                        memUi.MemoryEngine360.SetConnection(token, null, ConnectionChangeCause.User);
                    }
                    catch (Exception ex) {
                        errors.Add(ex);
                    }

                    try {
                        existingConnection.Close();
                    }
                    catch (Exception ex) {
                        errors.Add(ex);
                    }

                    if (ILatestActivityView.LatestActivityDataKey.TryGetContext(e.ContextData, out ILatestActivityView? view))
                        view.Activity = "Disconnected from console";

                    memUi.RemoteCommandsContextEntry.Items.Clear();
                }
            }
        }

        await ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionManager>().OpenDialog(memUi);
    }
}