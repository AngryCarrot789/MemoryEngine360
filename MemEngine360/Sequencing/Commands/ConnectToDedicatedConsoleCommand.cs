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
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Sequencing.Commands;

public class ConnectToDedicatedConsoleCommand : Command {
    private static readonly DataKey<IOpenConnectionView> ExistingOCVDataKey = DataKeys.Create<IOpenConnectionView>(nameof(ConnectToDedicatedConsoleCommand) + "_" + nameof(ExistingOCVDataKey));
    
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!TaskSequence.DataKey.TryGetContext(e.ContextData, out TaskSequence? seq))
            return Executability.Invalid;
        if (ExistingOCVDataKey.TryGetContext(seq.UserContext, out IOpenConnectionView? view))
            return Executability.ValidButCannotExecute;

        return seq.IsRunning ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!TaskSequence.DataKey.TryGetContext(e.ContextData, out TaskSequence? sequence))
            return;
        if (sequence.IsRunning)
            return;
        
        if (ExistingOCVDataKey.TryGetContext(sequence.UserContext, out IOpenConnectionView? view)) {
            if (view.DialogOperation is IDesktopDialogOperation<ConnectionResult> op)
                op.Activate();
            return;
        }

        IConsoleConnection? oldConnection = sequence.DedicatedConnection;
        if (oldConnection != null && !oldConnection.IsClosed) {
            MessageBoxResult mbr = await MessageBoxes.AlreadyConnectedToConsole.ShowMessage();
            if (mbr != MessageBoxResult.OK) {
                return;
            }

            // just in case it somehow starts running, quickly escape
            if (sequence.IsRunning) {
                return;
            }

            // just in case it changes between dialog which is possible
            if ((oldConnection = sequence.DedicatedConnection) != null) {
                oldConnection.Close();
                sequence.DedicatedConnection = null;
            }
        }
        
        IOpenConnectionView? dialog = await ApplicationPFX.GetComponent<ConsoleConnectionManager>().ShowOpenConnectionView(OpenConnectionInfo.CreateDefault());
        if (dialog != null) {
            sequence.UserContext.Set(ExistingOCVDataKey, dialog);

            try {
                ConnectionResult? result = await dialog.WaitForConnection();
                if (!result.HasValue) {
                    return;
                }

                // just in case it somehow starts running, quickly escape
                if (sequence.IsRunning) {
                    result.Value.Connection.Close();
                    return;
                }

                oldConnection = sequence.DedicatedConnection;
                sequence.UseEngineConnection = false;
                sequence.DedicatedConnection = result.Value.Connection;
                oldConnection?.Close();
            }
            finally {
                sequence.UserContext.Remove(ExistingOCVDataKey);
            }
        }
    }
}