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

using MemEngine360.Commands;
using MemEngine360.Engine;
using MemEngine360.Engine.View;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Xbox360XBDM.Commands;

public class ShowXbeInfoCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine engine, CommandEventArgs e) {
        return engine.Connection is XbdmConsoleConnection ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngineViewState engineVs, MemoryEngine engine, CommandEventArgs e) {
        await engine.BeginBusyOperationUsingActivityAsync(async (t, c) => {
            if (c is XbdmConsoleConnection xbox) {
                string? path = await xbox.GetXbeInfo(null);
                if (!string.IsNullOrEmpty(path)) {
                    await IMessageDialogService.Instance.ShowMessage("File Path", path);
                }
                else {
                    await IMessageDialogService.Instance.ShowMessage("Error", "No name attribute in Xbe info");
                }
            }
        }, "Xbe Info");
    }
}