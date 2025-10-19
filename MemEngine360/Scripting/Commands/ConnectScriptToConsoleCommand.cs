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
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Scripting.Commands;

public class ConnectScriptToConsoleCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!Script.DataKey.TryGetContext(e.ContextData, out Script? script) || script.Manager == null) {
            return Executability.Invalid;
        }

        return script.IsRunning ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!Script.DataKey.TryGetContext(e.ContextData, out Script? script))
            return;
        if (script.IsRunning)
            return;

        IConsoleConnection? oldConnection = script.DedicatedConnection;
        if (oldConnection != null && !oldConnection.IsClosed) {
            MessageBoxResult mbr = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to a console. Close existing connection first?", MessageBoxButtons.OKCancel, MessageBoxResult.OK, persistentDialogName: OpenConsoleConnectionDialogCommand.AlreadyOpenDialogName);
            if (mbr != MessageBoxResult.OK) {
                return;
            }

            // just in case it somehow starts running, quickly escape
            if (script.IsRunning) {
                return;
            }

            // just in case it changes between dialog which is possible
            if ((oldConnection = script.DedicatedConnection) != null) {
                CloseConnection(oldConnection);
                script.DedicatedConnection = null;
            }
        }

        IOpenConnectionView? dialog = await ApplicationPFX.GetComponent<ConsoleConnectionManager>().ShowOpenConnectionView(OpenConnectionInfo.CreateDefault());
        if (dialog == null) {
            return;
        }

        ConnectionResult? result = await dialog.WaitForConnection();
        if (!result.HasValue) {
            return;
        }

        // just in case it somehow starts running, quickly escape
        if (script.IsRunning) {
            CloseConnection(result.Value.Connection);
            return;
        }

        oldConnection = script.DedicatedConnection;
        script.DedicatedConnection = result.Value.Connection;
        if (oldConnection != null) {
            CloseConnection(oldConnection);
        }
    }

    private static void CloseConnection(IConsoleConnection connection) {
        try {
            connection.Close();
        }
        catch (Exception ex) {
            AppLogger.Instance.WriteLine("Error closing connection");
            AppLogger.Instance.WriteLine(ex.GetToString());
        }
    }
}