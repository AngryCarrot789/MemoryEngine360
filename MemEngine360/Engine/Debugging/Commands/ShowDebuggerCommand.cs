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

namespace MemEngine360.Engine.Debugging.Commands;

public class ShowDebuggerCommand : Command {
    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine.EngineDataKey.TryGetContext(e.ContextData, out MemoryEngine? engine))
            return;

        ConsoleDebugger debugger = engine.ConsoleDebugger;
        IDebuggerViewService service = ApplicationPFX.GetComponent<IDebuggerViewService>();
        await service.ShowDebugger(debugger);
        if (debugger.Connection == null) {
            IOpenConnectionView? dialog = await ApplicationPFX.GetComponent<ConsoleConnectionManager>().ShowOpenConnectionView(debugger.Engine);
            if (dialog == null) {
                return;
            }

            IDisposable? token = null;

            try {
                IConsoleConnection? connection = await dialog.WaitForClose();
                if (connection != null) {
                    // When returned token is null, close the connection since we can't
                    // do anything else with the connection since the user cancelled the operation
                    if (!await OpenDebuggerConnectionCommand.TrySetConnectionAndHandleProblems(debugger, connection)) {
                        connection.Close();
                    }
                }
            }
            finally {
                token?.Dispose();
            }
        }

        await debugger.UpdateAllThreads(CancellationToken.None);
    }
}