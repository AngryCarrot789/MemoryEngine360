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
using MemEngine360.Engine;
using MemEngine360.XboxInfo;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Commands;

public class ShowMemoryRegionsCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine engine, CommandEventArgs e) {
        if (engine.Connection != null) {
            return engine.Connection is IHaveMemoryRegions ? Executability.Valid : Executability.Invalid;
        }

        // limitation of commands API -- this is where we have to add/remove buttons dynamically to get around this
        return Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngine engine, CommandEventArgs e) {
        if (engine.Connection is IHaveMemoryRegions) {
            List<MemoryRegion>? list = await engine.BeginBusyOperationActivityAsync(async (t, c) => {
                if (!(c is IHaveMemoryRegions regions)) {
                    await IMessageDialogService.Instance.ShowMessage("Unsupported", "This console does not support memory region querying");
                    return null;
                }
                
                return await ActivityManager.Instance.RunTask(() => {
                    IActivityProgress prog = ActivityManager.Instance.CurrentTask.Progress;
                    prog.Caption = "Memory Regions";
                    prog.Text = "Reading memory regions...";
                    prog.IsIndeterminate = true;
                    return regions.GetMemoryRegions(false, false);
                });
            });

            if (list == null) {
                return;
            }

            MemoryRegionUserInputInfo info = new MemoryRegionUserInputInfo(list) {
                Caption = "Memory Regions",
                ConfirmText = "Epic", CancelText = "Close", // UserInputDialog limitation -- cannot disable OK :-)
                RegionFlagsToTextConverter = MemoryRegionUserInputInfo.ConvertXboxFlagsToText
            };

            await IUserInputDialogService.Instance.ShowInputDialogAsync(info);
        }
    }
}