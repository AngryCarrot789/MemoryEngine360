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

using PFXToolKitUI.Activities;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Scripting.Commands;

public class SaveScriptCommand : Command {
    public bool SaveAs { get; }

    public SaveScriptCommand(bool saveAs) {
        this.SaveAs = saveAs;
    }

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

        if (this.SaveAs || script.HasUnsavedChanges || !File.Exists(script.FilePath)) {
            await SaveScriptAsync(script, this.SaveAs);
        }
    }

    /// <summary>
    /// Save the script source code to a file
    /// </summary>
    /// <param name="script">The script</param>
    /// <param name="saveAs">True to select a new file path and update <see cref="Script.FilePath"/> </param>
    /// <returns>True when saved, False when user said not to save or failed to save</returns>
    public static async Task<bool> SaveScriptAsync(Script script, bool saveAs) {
        string? filePath = script.FilePath;
        if (saveAs || !File.Exists(filePath)) {
            string? path = await IFilePickDialogService.Instance.SaveFile("Save script", [Filters.Lua, Filters.All], filePath ?? script.Name);
            if (path == null) {
                return false;
            }

            script.SetFilePath(filePath = path);
        }

        try {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Task writeTask = File.WriteAllTextAsync(filePath, script.Document.Text, cts.Token);
            await Task.WhenAny(writeTask, Task.Delay(500, cts.Token));
            if (!writeTask.IsCompleted) {
                ActivityTask activity = ActivityManager.Instance.RunTask(() => {
                    ActivityTask.Current.Progress.SetCaptionAndText("Save Script", "Saving file...", newIsIndeterminate: true);
                    return writeTask;
                }, cts);

                if (IForegroundActivityService.TryGetInstance(out IForegroundActivityService? service)) {
                    ITopLevel? topLevel = TopLevelContextUtils.GetTopLevelFromContext();
                    if (topLevel != null) {
                        await service.WaitForActivity(topLevel, activity, CancellationToken.None);
                    }
                }

                await activity;
            }

            await cts.CancelAsync();
            await writeTask;

            script.HasUnsavedChanges = false;
            return true;
        }
        catch (Exception ex) {
            await IMessageDialogService.Instance.ShowMessage("File Error", "Error saving contents to path", ex.Message);
            return false;
        }
    }
}