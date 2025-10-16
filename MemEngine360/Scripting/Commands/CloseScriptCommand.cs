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
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Scripting.Commands;

public class CloseScriptCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!Script.DataKey.TryGetContext(e.ContextData, out Script? script) || script.Manager == null) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }
    
    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!Script.DataKey.TryGetContext(e.ContextData, out Script? script)) {
            return;
        }

        if (!await StopScriptCommand.StopScriptAsync(script, true)) {
            return;
        }

        if (script.IsCompiling) {
            // Create a CTS that gets cancelled when the compilation finishes, regardless of successful or cancelled
            using CancellationTokenSource cts = TaskUtils.CreateCompletionSource(script.CompileTask);
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Compiling", "Script is still compiling. Cancel the compilation?", MessageBoxButtons.OKCancel, MessageBoxResult.OK, dialogCancellation: cts.Token);
            if (result != MessageBoxResult.OK && script.IsCompiling) {
                // In this case, the user either said do not cancel or something caused
                // the window to close that wasn't the compilation completing (via cts)
                return;
            }

            if (script.IsCompiling) {
                script.RequestCancelCompilation();
                await script.CompileTask; // CompileTask only gets set as completed, even if cancelled
            }
        }

        Debug.Assert(!script.IsCompiling);
        ScriptViewState.GetInstance(script).RaiseFlushEditorToScript();
        if (script.HasUnsavedChanges) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Unsaved changes", "You still have saved changes. Do you want to save to a file?", MessageBoxButtons.YesNoCancel, MessageBoxResult.Yes);
            if (result == MessageBoxResult.Yes) {
                if (!await SaveScriptCommand.SaveScriptAsync(script, false)) {
                    return;
                }
            }
            else if (result != MessageBoxResult.No) {
                return; // user clicked cancel or closed dialog
            }
        }

        if (script.IsRunning) {
            await IMessageDialogService.Instance.ShowMessage("Unexpected", "Script running again for some reason. Please try again.");
            return;
        }
        
        if (script.IsCompiling) {
            await IMessageDialogService.Instance.ShowMessage("Unexpected", "Script compiling again for some reason. Please try again.");
            return;
        }

        if (script.Manager != null) {
            script.Manager.Scripts.Remove(script);
        }
    }
}