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
        if (!ITaskSequenceEntryUI.DataKey.TryGetContext(e.ContextData, out ITaskSequenceEntryUI? seq)) {
            return Executability.Invalid;
        }

        return seq.TaskSequence.IsRunning ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ITaskSequenceEntryUI.DataKey.TryGetContext(e.ContextData, out ITaskSequenceEntryUI? seqUI)) {
            return;
        }

        TaskSequence seq = seqUI.TaskSequence;
        if (seq.IsRunning) {
            return;
        }

        IConsoleConnection? oldConnection = seq.DedicatedConnection;
        if (oldConnection != null) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to a console. Close existing connection first?", MessageBoxButton.OKCancel, MessageBoxResult.OK);
            if (result != MessageBoxResult.OK) {
                return;
            }

            // just in case it somehow starts running, quickly escape
            if (seq.IsRunning) {
                return;
            }
            
            // just in case it changes between dialog which is possible
            if ((oldConnection = seq.DedicatedConnection) != null) {
                await oldConnection.Close();
                seq.DedicatedConnection = null;
            }
        }
        
        IConsoleConnection? newConnection;
        IOpenConnectionView? dialog = await ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionManager>().ShowOpenConnectionView(seq.Manager?.MemoryEngine);
        if (dialog == null || (newConnection = await dialog.WaitForClose()) == null) {
            return;
        }
        
        // just in case it somehow starts running, quickly escape
        if (seq.IsRunning) {
            await newConnection.Close();
            return;
        }

        oldConnection = seq.DedicatedConnection;
        seq.UseEngineConnection = false;
        seq.DedicatedConnection = newConnection;
        if (oldConnection != null) {
            await oldConnection.Close();
        }
    }
}