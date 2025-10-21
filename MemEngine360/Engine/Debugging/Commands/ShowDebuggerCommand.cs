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
using PFXToolKitUI.Interactivity.Windowing;

namespace MemEngine360.Engine.Debugging.Commands;

public class ShowDebuggerCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngine.EngineDataKey.TryGetContext(e.ContextData, out MemoryEngine? engine))
            return Executability.Invalid;
        if (OpenDebuggerConnectionCommand.ExistingOCVDataKey.TryGetContext(engine.UserContext, out IOpenConnectionView? view))
            return Executability.ValidButCannotExecute;
        
        return base.CanExecuteCore(e);
    }

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
            _ = CommandManager.Instance.Execute("commands.debugger.OpenDebuggerConnectionCommand", new ContextData(e.ContextData).Set(ITopLevel.TopLevelDataKey, topLevel), null, null);
            
            // Run as command action to push the debugger view as the primary contextual top level
            if (OpenDebuggerConnectionCommand.ExistingOCVDataKey.TryGetContext(debugger.UserContext, out IOpenConnectionView? view)) {
                if (view.DialogOperation is IDesktopDialogOperation<ConnectionResult> op)
                    op.Activate();
            }
            else {
                await CommandManager.Instance.RunActionAsync(async ex => {
                    OpenConnectionInfo info = OpenConnectionInfo.CreateDefault(isEnabledFilter: t => t.MaybeSupportsDebugging);
                    IOpenConnectionView? dialog = await ApplicationPFX.GetComponent<ConsoleConnectionManager>().ShowOpenConnectionView(info);
                    if (dialog != null) {
                        debugger.UserContext.Set(OpenDebuggerConnectionCommand.ExistingOCVDataKey, dialog);
                        CommandUsageSignal signal = CommandUsageSignal.GetOrCreate(debugger.UserContext, OpenDebuggerConnectionCommand.CommandUsageSignalDataKey);
                        signal.RaiseCanExecuteChanged();
                        
                        try {
                            ConnectionResult? result = await dialog.WaitForConnection();
                            if (result.HasValue) {
                                // When returned token is null, close the connection since we can't
                                // do anything else with the connection since the user cancelled the operation
                                if (!await OpenDebuggerConnectionCommand.TrySetConnectionAndHandleProblems(debugger, result.Value.Connection)) {
                                    result.Value.Connection.Close();
                                }
                            }
                        }
                        finally {
                            debugger.UserContext.Remove(OpenDebuggerConnectionCommand.ExistingOCVDataKey);
                            signal.RaiseCanExecuteChanged();
                        }
                    }
                }, new ContextData().Set(ITopLevel.TopLevelDataKey, topLevel));
            }
        }

        await debugger.UpdateAllThreads(CancellationToken.None);
    }
}