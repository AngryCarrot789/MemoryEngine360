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
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Dialogs;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Scripting.Commands;

public class ConnectScriptToConsoleCommand : Command {
    private static readonly DataKey<IOpenConnectionView> ExistingOCVDataKey = DataKeys.Create<IOpenConnectionView>(nameof(ConnectScriptToConsoleCommand) + "_" + nameof(ExistingOCVDataKey));

    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!Script.DataKey.TryGetContext(e.ContextData, out Script? script) || script.Manager == null)
            return Executability.Invalid;
        if (ExistingOCVDataKey.TryGetContext(script.UserContext, out IOpenConnectionView? view))
            return Executability.ValidButCannotExecute;

        return script.IsRunning ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!Script.DataKey.TryGetContext(e.ContextData, out Script? script))
            return;
        if (script.IsRunning)
            return;

        if (ExistingOCVDataKey.TryGetContext(script.UserContext, out IOpenConnectionView? view)) {
            if (view.DialogOperation is IDesktopDialogOperation<ConnectionResult> op)
                op.Activate();
            return;
        }

        IConsoleConnection? oldConnection = script.DedicatedConnection;
        if (oldConnection != null && !oldConnection.IsClosed) {
            MessageBoxResult mbr = await MessageBoxes.AlreadyConnectedToConsole.ShowMessage();
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

        script.UserContext.Set(ExistingOCVDataKey, dialog);
        
        try {
            ConnectionResult? result = await dialog.WaitForConnection();
            if (result.HasValue) {
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
        }
        finally {
            script.UserContext.Remove(ExistingOCVDataKey);
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