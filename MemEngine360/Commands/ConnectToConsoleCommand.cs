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

using System.Net;
using System.Text;
using MemEngine360.Connections;
using MemEngine360.Connections.Impl;
using MemEngine360.Connections.Impl.Threads;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands;

public class ConnectToConsoleCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine)) {
            return Executability.Invalid;
        }
        
        return !engine.IsConnectionBusy ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine)) {
            return;
        }

        using IDisposable? token = await engine.BeginBusyOperationActivityAsync();
        if (token == null) {
            return;
        }
        
        IConsoleConnection? existingConnection = engine.GetConnection(token);
        if (existingConnection != null) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to an xbox. Close existing connection and then connect", MessageBoxButton.OKCancel, MessageBoxResult.OK);
            if (result != MessageBoxResult.OK) {
                return;
            }
            
            existingConnection.Dispose();
            engine.SetConnection(token, null, ConnectionChangeCause.User);
            
            if (ILatestActivityView.DataKey.TryGetContext(e.ContextData, out ILatestActivityView? view))
                view.Activity = "Disconnected from xbox 360";
        }

        IConsoleConnection? connection = await ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionService>().OpenDialogAndConnect();
        if (connection != null) {
            engine.SetConnection(token, connection, ConnectionChangeCause.User);
            if (ILatestActivityView.DataKey.TryGetContext(e.ContextData, out ILatestActivityView? view))
                view.Activity = "Connected to xbox 360.";

            string debugName = await connection.GetDebugName();
            ExecutionState execState = await connection.GetExecutionState();
            IPAddress currTitleAddr = await connection.GetTitleIPAddress();
            uint currProcId = await connection.GetProcessID();

            StringBuilder sb = new StringBuilder();
            sb.Append("Debug Name: ").Append(debugName).AppendLine();
            sb.Append("Execution State: ").Append(execState).AppendLine();
            sb.Append("Current Title IP: ").Append(currTitleAddr).AppendLine();
            sb.Append("Current Process ID: ").Append(currProcId.ToString("X8")).AppendLine();
            sb.AppendLine("Named Threads below");
            foreach (ConsoleThread info in await connection.GetThreadDump()) {
                if (!string.IsNullOrEmpty(info.readableName)) {
                    sb.AppendLine(info.ToString());
                }
            }
            
            await IMessageDialogService.Instance.ShowMessage("Information", "Console Information as follows", sb.ToString());
        }
    }
}