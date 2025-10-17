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

using System.Runtime.Versioning;
using MemEngine360.Commands;
using MemEngine360.Engine;
using MemEngine360.PS3.CC;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.PS3.Commands;

[SupportedOSPlatform("windows")]
public class SetProcessToActiveGameCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine engine, CommandEventArgs e) {
        if (engine.Connection == null || engine.Connection.IsClosed)
            return Executability.ValidButCannotExecute;
        if (!(engine.Connection is ConsoleConnectionCCAPI api))
            return Executability.Invalid;

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngine engine, CommandEventArgs e) {
        if (engine.Connection == null || engine.Connection.IsClosed)
            return;
        if (!(engine.Connection is ConsoleConnectionCCAPI api))
            return;

        uint newPid;
        try {
            (uint pid, string? processName) = await api.FindGameProcessId();
            if (pid == 0) {
                await IMessageDialogService.Instance.ShowMessage("No game", "No game is running");
                return;
            }

            newPid = pid;
        }
        catch (Exception ex) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Error while trying to attach to process: " + ex.Message);
            return;
        }
        
        api.AttachedProcess = newPid;
    }
}