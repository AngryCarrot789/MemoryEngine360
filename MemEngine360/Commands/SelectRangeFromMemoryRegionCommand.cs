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

using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.MemRegions;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Commands;

public class SelectRangeFromMemoryRegionCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        if (engine.Connection != null) {
            return engine.Connection is IHaveMemoryRegions ? Executability.Valid : Executability.Invalid;
        }

        // limitation of commands API -- this is where we have to add/remove buttons dynamically to get around this
        return Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        List<MemoryRegion>? list;
        using (IDisposable? token = await engine.BeginBusyOperationActivityAsync("Reading memory regions")) {
            if (token == null) {
                return;
            }

            IConsoleConnection? connection = engine.Connection;
            if (connection == null) {
                await IMessageDialogService.Instance.ShowMessage("Error", "No connection present -- cannot fetch memory regions");
                return;
            }

            if (!(connection is IHaveMemoryRegions regions)) {
                await IMessageDialogService.Instance.ShowMessage("Unsupported", "This console does not support memory region querying");
                return;
            }

            list = await ActivityManager.Instance.RunTask(() => {
                IActivityProgress prog = ActivityManager.Instance.CurrentTask.Progress;
                prog.Caption = "Memory Regions";
                prog.Text = "Reading memory regions...";
                prog.IsIndeterminate = true;
                return regions.GetMemoryRegions();
            });
        }

        MemoryRegionUserInputInfo info = new MemoryRegionUserInputInfo(list) {
            Caption = "Change scan region",
            Message = "Select a memory region to set as the start/length fields",
            ConfirmText = "Select"
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true && info.SelectedRegion != null) {
            engine.ScanningProcessor.StartAddress = info.SelectedRegion.BaseAddress;
            engine.ScanningProcessor.ScanLength = info.SelectedRegion.Size;
            engine.ScanningProcessor.ScanMemoryPages = false;
        }
    }
}