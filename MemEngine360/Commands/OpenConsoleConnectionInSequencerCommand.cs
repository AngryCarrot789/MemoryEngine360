﻿// 
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

namespace MemEngine360.Commands;

public class OpenConsoleConnectionInSequencerCommand : Command {
    private IOpenConnectionView? myDialog;

    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ITaskSequenceManagerUI.DataKey.TryGetContext(e.ContextData, out ITaskSequenceManagerUI? ui) || !ui.IsValid) {
            return Executability.Invalid;
        }

        return ui.Manager.MemoryEngine.Connection != null ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (this.myDialog != null && !this.myDialog.IsClosed) {
            this.myDialog.Activate();
            return;
        }

        if (!ITaskSequenceManagerUI.DataKey.TryGetContext(e.ContextData, out ITaskSequenceManagerUI? ui)) {
            return;
        }

        MemoryEngine engine = ui.Manager.MemoryEngine;
        ulong frame = engine.GetNextConnectionChangeFrame();

        if (engine.Connection != null) {
            return;
        }

        this.myDialog = await ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionManager>().ShowOpenConnectionView(engine);
        if (this.myDialog != null) {
            IDisposable? token = null;
            try {
                IConsoleConnection? connection = await this.myDialog.WaitForClose();
                if (connection != null) {
                    // When returned token is null, close the connection since we can't
                    // do anything else with the connection since the user cancelled the operation
                    token = await OpenConsoleConnectionDialogCommand.SetEngineConnectionAndHandleProblemsAsync(engine, connection, frame);
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