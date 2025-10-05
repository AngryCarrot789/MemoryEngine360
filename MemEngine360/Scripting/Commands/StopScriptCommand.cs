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

using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Scripting.Commands;

public class StopScriptCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!Script.DataKey.TryGetContext(e.ContextData, out Script? script) || script.Manager == null) {
            return Executability.Invalid;
        }

        return script.IsRunning ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!Script.DataKey.TryGetContext(e.ContextData, out Script? script) || script.Manager == null) {
            return;
        }

        await StopScriptAsync(script, false);
    }

    public static async Task<bool> StopScriptAsync(Script script, bool askToStop) {
        if (!script.IsRunning) {
            return true;
        }

        using CancellationTokenSource ctsFinished = TaskUtils.CreateCompletionSource(script.ScriptTask);
        if (askToStop) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Script is running", "The script is still running, so it will be stopped.", MessageBoxButton.OKCancel, MessageBoxResult.OK, dialogCancellation: ctsFinished.Token);
            if (result != MessageBoxResult.OK) {
                return false;
            }
        }

        script.RequestStop(false);

        // wait 2 seconds for the script to complete normally
        await Task.WhenAny(script.ScriptTask, Task.Delay(2000, ctsFinished.Token));
        if (script.IsRunning) {
            MessageBoxResult result2 = await IMessageDialogService.Instance.ShowMessage("Script still running", $"The script is still running. Force kill the script?{Environment.NewLine}{Environment.NewLine}Note, due to .NET restrictions, the lua thread cannot be 'killed' safely.", MessageBoxButton.OKCancel, MessageBoxResult.OK, dialogCancellation: ctsFinished.Token);
            if (result2 != MessageBoxResult.OK) {
                return false;
            }

            script.RequestStop(true);
        }

        // Wait 2 seconds for the script to complete normally
        await Task.WhenAny(script.ScriptTask, Task.Delay(2000, ctsFinished.Token));
        if (script.IsRunning) {
            await IMessageDialogService.Instance.ShowMessage("Script still running", $"Could not stop the script! Please try again later.", MessageBoxButton.OKCancel, MessageBoxResult.OK);
            return false;
        }

        return true;
    }
}