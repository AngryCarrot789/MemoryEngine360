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

namespace MemEngine360.ModTools.Commands;

public class ConnectModToolToConsoleCommand : Command {
    private static readonly DataKey<IOpenConnectionView> ExistingOCVDataKey = DataKeys.Create<IOpenConnectionView>(nameof(ConnectModToolToConsoleCommand) + "_" + nameof(ExistingOCVDataKey));

    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ModTool.DataKey.TryGetContext(e.ContextData, out ModTool? tool) || tool.Manager == null)
            return Executability.Invalid;
        if (ExistingOCVDataKey.TryGetContext(tool.UserContext, out IOpenConnectionView? view))
            return Executability.ValidButCannotExecute;

        return tool.IsRunning ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ModTool.DataKey.TryGetContext(e.ContextData, out ModTool? tool))
            return;
        if (tool.IsRunning)
            return;

        if (ExistingOCVDataKey.TryGetContext(tool.UserContext, out IOpenConnectionView? view)) {
            if (view.DialogOperation is IDesktopDialogOperation<ConnectionResult> op)
                op.Activate();
            return;
        }

        IConsoleConnection? oldConnection = tool.DedicatedConnection;
        if (oldConnection != null && !oldConnection.IsClosed) {
            MessageBoxResult mbr = await MessageBoxes.AlreadyConnectedToConsole.ShowMessage();
            if (mbr != MessageBoxResult.OK) {
                return;
            }

            // just in case it somehow starts running, quickly escape
            if (tool.IsRunning) {
                return;
            }

            // just in case it changes between dialog which is possible
            if ((oldConnection = tool.DedicatedConnection) != null) {
                CloseConnection(oldConnection);
                tool.DedicatedConnection = null;
            }
        }

        IOpenConnectionView? dialog = await ApplicationPFX.GetComponent<ConsoleConnectionManager>().ShowOpenConnectionView(OpenConnectionInfo.CreateDefault());
        if (dialog == null) {
            return;
        }

        tool.UserContext.Set(ExistingOCVDataKey, dialog);
        
        try {
            ConnectionResult? result = await dialog.WaitForConnection();
            if (result.HasValue) {
                // just in case it somehow starts running, quickly escape
                if (tool.IsRunning) {
                    CloseConnection(result.Value.Connection);
                    return;
                }

                oldConnection = tool.DedicatedConnection;
                tool.DedicatedConnection = result.Value.Connection;
                if (oldConnection != null) {
                    CloseConnection(oldConnection);
                }
            }
        }
        finally {
            tool.UserContext.Remove(ExistingOCVDataKey);
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