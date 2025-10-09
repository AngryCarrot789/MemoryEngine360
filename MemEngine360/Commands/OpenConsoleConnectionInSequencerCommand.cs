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

using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Sequencing;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands;

public class OpenConsoleConnectionInSequencerCommand : Command {
    private IOpenConnectionView? myDialog;

    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!TaskSequenceManager.DataKey.TryGetContext(e.ContextData, out TaskSequenceManager? manager)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (this.myDialog != null && this.myDialog.IsWindowOpen) {
            this.myDialog.Activate();
            return;
        }

        if (!TaskSequenceManager.DataKey.TryGetContext(e.ContextData, out TaskSequenceManager? manager))
            return;
        if (!ITopLevel.TopLevelDataKey.TryGetContext(e.ContextData, out ITopLevel? topLevel))
            return;

        MemoryEngine engine = manager.MemoryEngine;
        ulong frame = engine.GetNextConnectionChangeFrame();
        if (engine.Connection != null && !engine.Connection.IsClosed) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(
                "Already Connected",
                "Already connected to a console. Close existing connection first?",
                MessageBoxButton.OKCancel, MessageBoxResult.OK,
                persistentDialogName: OpenConsoleConnectionDialogCommand.AlreadyOpenDialogName);
            if (result != MessageBoxResult.OK) {
                return;
            }

            if (!await OpenConsoleConnectionDialogCommand.DisconnectInActivity(topLevel, engine, frame)) {
                return;
            }
        }

        this.myDialog = await ApplicationPFX.GetComponent<ConsoleConnectionManager>().ShowOpenConnectionView();
        if (this.myDialog != null) {
            IBusyToken? token = null;
            try {
                IConsoleConnection? connection = await this.myDialog.WaitForConnection();
                if (connection != null) {
                    // When returned token is null, close the connection since we can't
                    // do anything else with the connection since the user cancelled the operation
                    token = await OpenConsoleConnectionDialogCommand.SetEngineConnectionAndHandleProblemsAsync(manager.MemoryEngine, connection, frame);
                    if (token == null) {
                        connection.Close();
                    }
                }
            }
            finally {
                this.myDialog = null;
                token?.Dispose();
            }
        }
    }
}