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
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Sequencing.Commands;

public class ConnectToDedicatedConsoleCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!TaskSequence.DataKey.TryGetContext(e.ContextData, out TaskSequence? seq)) {
            return Executability.Invalid;
        }

        return seq.IsRunning ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!TaskSequence.DataKey.TryGetContext(e.ContextData, out TaskSequence? sequence))
            return;
        if (sequence.IsRunning)
            return;

        IConsoleConnection? oldConnection = sequence.DedicatedConnection;
        if (oldConnection != null) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to a console. Close existing connection first?", MessageBoxButton.OKCancel, MessageBoxResult.OK);
            if (result != MessageBoxResult.OK) {
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
        
        IConsoleConnection? newConnection;
        IOpenConnectionView? dialog = await ApplicationPFX.GetService<ConsoleConnectionManager>().ShowOpenConnectionView(sequence.Manager?.MemoryEngine);
        if (dialog == null || (newConnection = await dialog.WaitForClose()) == null) {
            return;
        }
        
        // just in case it somehow starts running, quickly escape
        if (sequence.IsRunning) {
            newConnection.Close();
            return;
        }

        oldConnection = sequence.DedicatedConnection;
        sequence.UseEngineConnection = false;
        sequence.DedicatedConnection = newConnection;
        if (oldConnection != null) {
            oldConnection.Close();
        }
    }
}