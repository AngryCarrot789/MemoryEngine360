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

using MemEngine360.Connections.Traits;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Engine.Debugging.Commands;

public class UnfreezeConsoleCommand : BaseDebuggerCommand {
    protected override Executability CanExecuteCore(ConsoleDebugger debugger, CommandEventArgs e) {
        if (debugger.Connection == null)
            return Executability.ValidButCannotExecute;

        bool? run = debugger.IsConsoleRunning;
        return !run.HasValue || !run.Value ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(ConsoleDebugger debugger, CommandEventArgs e) {
        if (debugger.Connection == null)
            return;

        using IDisposable? token = await debugger.BusyLock.BeginBusyOperationActivityAsync("Unfreeze Console");
        if (token != null && debugger.Connection != null && debugger.Connection is IHaveIceCubes iceCubes) {
            debugger.IsConsoleRunning = true;
            debugger.ConsoleExecutionState = null;

            try {
                await iceCubes.DebugUnFreeze();
            }
            catch (Exception ex) when (ex is IOException || ex is TimeoutException) {
                await IMessageDialogService.Instance.ShowMessage("Network error", ex.Message);
            }
            catch (Exception ex) {
                await IMessageDialogService.Instance.ShowMessage("Error", ex.Message);
            }
        }
    }
}