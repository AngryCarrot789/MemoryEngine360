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

using MemEngine360.Connections.XBOX;
using MemEngine360.Engine;
using MemEngine360.MemRegions;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Commands;

public class MemProtectionCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        return engine.Connection != null ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        List<MemoryRegion>? list = await engine.BeginBusyOperationActivityAsync(async (t, c) => {
            return await ActivityManager.Instance.RunTask(() => {
                IActivityProgress prog = ActivityManager.Instance.CurrentTask.Progress;
                prog.Text = "Reading memory regions...";
                prog.IsIndeterminate = true;
                return c.GetMemoryRegions();
            });
        });

        if (list == null) {
            return;
        }
        
        MemoryRegionUserInputInfo info = new MemoryRegionUserInputInfo(list) {
            Caption = "Memory Regions",
            ConfirmText = "Epic", CancelText = "Close" // UserInputDialog limitation -- cannot disable OK :-)
        };

        await IUserInputDialogService.Instance.ShowInputDialogAsync(info);
    }
}