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

using MemEngine360.Commands;
using MemEngine360.Engine;
using MemEngine360.XboxInfo;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.UserInputs;
using XDevkit;

namespace MemEngine360.Xbox360XDevkit;

public class ModulesCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        if (engine.Connection != null) {
            return engine.Connection is Devkit360Connection ? Executability.Valid : Executability.Invalid;
        }

        // limitation of commands API -- this is where we have to add/remove buttons dynamically to get around this
        return Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        if (engine.Connection is Devkit360Connection conn) {
            ModuleUserInputInfo info = new ModuleUserInputInfo() {
                Caption = "Modules",
                ConfirmText = "Epic", CancelText = "Close"
            };

            foreach (IXboxModule module in conn.Console.DebugTarget.Modules) {
                info.MemoryRegions.Add(module.ModuleInfo.Name);
            }

            await IUserInputDialogService.Instance.ShowInputDialogAsync(info);
        }
    }
}