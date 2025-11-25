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

using System.Diagnostics;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.ModTools.Commands;

public class CloseModToolCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ModTool.DataKey.TryGetContext(e.ContextData, out ModTool? script) || script.Manager == null) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ModTool.DataKey.TryGetContext(e.ContextData, out ModTool? tool)) {
            return;
        }

        if (!await RestartModToolCommand.StopModTool(tool, true)) {
            return;
        }

        if (tool.IsCompiling) {
            // Create a CTS that gets cancelled when the compilation finishes, regardless of successful or cancelled
            using CancellationTokenSource cts = TaskUtils.CreateCompletionSource(tool.CompileTask);
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Compiling", "ModTool is still compiling. Cancel the compilation?", MessageBoxButtons.OKCancel, MessageBoxResult.OK, dialogCancellation: cts.Token);
            if (result != MessageBoxResult.OK && tool.IsCompiling) {
                // In this case, the user either said do not cancel or something caused
                // the window to close that wasn't the compilation completing (via cts)
                return;
            }

            if (tool.IsCompiling) {
                tool.RequestCancelCompilation();
                await tool.CompileTask; // CompileTask only gets set as completed, even if cancelled
            }
        }

        Debug.Assert(!tool.IsCompiling);
        if (tool.HasUnsavedChanges) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Unsaved changes", "You still have saved changes. Do you want to save to a file?", MessageBoxButtons.YesNoCancel, MessageBoxResult.Yes);
            if (result == MessageBoxResult.Yes) {
                if (!await SaveModToolCommand.SaveModToolAsync(tool, false)) {
                    return;
                }
            }
            else if (result != MessageBoxResult.No) {
                return; // user clicked cancel or closed dialog
            }
        }

        if (tool.IsRunning) {
            await IMessageDialogService.Instance.ShowMessage("Unexpected", "ModTool running again for some reason. Please try again.");
            return;
        }

        if (tool.IsCompiling) {
            await IMessageDialogService.Instance.ShowMessage("Unexpected", "ModTool compiling again for some reason. Please try again.");
            return;
        }

        await ApplicationPFX.GetComponent<IModToolViewService>().CloseGui(tool);
        
        if (tool.Manager != null) {
            tool.Manager.RemoveModTool(tool);
        }
    }
}