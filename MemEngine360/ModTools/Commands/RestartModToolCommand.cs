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

using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.ModTools.Commands;

public class RestartModToolCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ModTool.DataKey.TryGetContext(e.ContextData, out ModTool? tool) || tool.Manager == null) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ModTool.DataKey.TryGetContext(e.ContextData, out ModTool? tool) || tool.Manager == null) {
            return;
        }

        if (await StopModTool(tool, false)) {
            if (!await tool.StartCommand()) {
                return;
            }

            await ApplicationPFX.GetComponent<IModToolViewService>().ShowOrFocusGui(tool);
        }
    }

    public static async Task<bool> StopModTool(ModTool tool, bool askToStop) {
        if (!tool.IsRunning) {
            return true;
        }

        using CancellationTokenSource ctsFinished = TaskUtils.CreateCompletionSource(tool.ScriptTask);
        if (askToStop) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Mod tool is running", "The Mod tool is still running, so it will be stopped.", MessageBoxButtons.OKCancel, MessageBoxResult.OK, dialogCancellation: ctsFinished.Token);
            if (result != MessageBoxResult.OK && tool.IsRunning)
                return false;
            if (!tool.IsRunning)
                return true;
        }

        if (tool.IsRunning) {
            tool.RequestStop(false);

            // wait 2 seconds for the script to complete normally
            await Task.WhenAny(tool.ScriptTask, Task.Delay(2000, ctsFinished.Token));
            if (tool.IsRunning) {
                MessageBoxResult result2 = await IMessageDialogService.Instance.ShowMessage("Mod tool still running", $"The Mod tool is still running. Force kill the mod tool?{Environment.NewLine}{Environment.NewLine}Note, due to .NET restrictions, the lua thread cannot be 'killed' safely.", MessageBoxButtons.OKCancel, MessageBoxResult.OK, dialogCancellation: ctsFinished.Token);
                if (result2 != MessageBoxResult.OK && tool.IsRunning)
                    return false;
                if (!tool.IsRunning)
                    return true;

                tool.RequestStop(true);

                // Wait 2 seconds for the script to complete normally
                await Task.WhenAny(tool.ScriptTask, Task.Delay(2000, ctsFinished.Token));
                if (tool.IsRunning) {
                    await IMessageDialogService.Instance.ShowMessage("Mod tool still running", $"Could not stop the mod tool! Please try again later.", MessageBoxButtons.OKCancel, MessageBoxResult.OK, dialogCancellation: ctsFinished.Token);
                    return false;
                }
            }
        }

        return true;
    }
}