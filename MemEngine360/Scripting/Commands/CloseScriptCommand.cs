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
using PFXToolKitUI.Services.FilePicking;
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

        if (script.HasUnsavedChanges) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Unsaved changes", "You still have saved changes. Do you want to save to a file?", MessageBoxButton.YesNoCancel, MessageBoxResult.Yes);
            if (result == MessageBoxResult.Yes) {
                string? path = await IFilePickDialogService.Instance.SaveFile("Save script", [Filters.Lua, Filters.All]);
                if (path == null) {
                    return;
                }

                try {
                    await File.WriteAllTextAsync(path, script.SourceCode);
                }
                catch (Exception ex) {
                    await IMessageDialogService.Instance.ShowMessage("File Error", "Error saving contents to path", ex.Message);
                }

                // Mark just because why not
                script.HasUnsavedChanges = false;
            }
            else if (result != MessageBoxResult.No) {
                return; // user clicked cancel or closed dialog
            }
        }

        if (script.IsRunning) {
            await IMessageDialogService.Instance.ShowMessage("Unexpected", "Script running again for some reason. Please try again.");
            return;
        }

        if (script.Manager != null) {
            script.Manager.Scripts.Remove(script);
        }
    }
}