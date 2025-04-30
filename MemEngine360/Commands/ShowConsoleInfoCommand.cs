// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of FramePFX.
// 
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3 of the License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with FramePFX. If not, see <https://www.gnu.org/licenses/>.
// 

using System.Net;
using System.Text;
using MemEngine360.Connections.XBOX;
using MemEngine360.Connections.XBOX.Threads;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands;

public class ShowConsoleInfoCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        return engine.Connection != null ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        return engine.BeginBusyOperationActivityAsync(async (t, c) => {
            if (c is IXbox360Connection xbox) {
                string debugName = await xbox.GetDebugName();
                ExecutionState execState = await xbox.GetExecutionState();
                IPAddress currTitleAddr = await xbox.GetTitleIPAddress();
                uint currProcId = await xbox.GetProcessID();

                StringBuilder sb = new StringBuilder();
                sb.Append("Debug Name: ").Append(debugName).AppendLine();
                sb.Append("Execution State: ").Append(execState).AppendLine();
                sb.Append("Current Title IP: ").Append(currTitleAddr).AppendLine();
                sb.Append("Current Process ID: ").Append(currProcId.ToString("X8")).AppendLine();
                sb.AppendLine("Named Threads below");
                foreach (ConsoleThread info in await xbox.GetThreadDump()) {
                    if (!string.IsNullOrEmpty(info.readableName)) {
                        sb.AppendLine(info.ToString());
                    }
                }

                await IMessageDialogService.Instance.ShowMessage("Information", "Console Information as follows", sb.ToString(), MessageBoxButton.OK, MessageBoxResult.OK);     
            }
        }, "Console Info");
    }
}