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
using PFXToolKitUI.Interactivity.Windowing;

namespace MemEngine360.Engine.Debugging.Commands;

public class ShowDebuggerCommand : Command {
    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine.EngineDataKey.TryGetContext(e.ContextData, out MemoryEngine? engine)) {
            return;
        }

        ConsoleDebugger debugger = engine.ConsoleDebugger;
        IDebuggerViewService service = ApplicationPFX.GetComponent<IDebuggerViewService>();
        ITopLevel? topLevel = await service.OpenOrFocusWindow(debugger);
        if (topLevel == null) {
            return; // could not find or create window for some reason
        }

        if (debugger.Connection == null) {
            // Run as command action to push the debugger view as the primary contextual top level
            await CommandManager.Instance.RunActionAsync(async ex => {
                IOpenConnectionView? dialog = await ApplicationPFX.GetComponent<ConsoleConnectionManager>().ShowOpenConnectionView();
                if (dialog == null) {
                    return;
                }

                IConsoleConnection? connection = await dialog.WaitForConnection();
                if (connection != null) {
                    // When returned token is null, close the connection since we can't
                    // do anything else with the connection since the user cancelled the operation
                    if (!await OpenDebuggerConnectionCommand.TrySetConnectionAndHandleProblems(debugger, connection)) {
                        connection.Close();
                    }
                }
            }, new ContextData().Set(ITopLevel.TopLevelDataKey, topLevel));
        }

        await debugger.UpdateAllThreads(CancellationToken.None);
    }
}