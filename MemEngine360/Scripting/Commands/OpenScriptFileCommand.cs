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

public class OpenScriptFileCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ScriptingManagerViewState.DataKey.TryGetContext(e.ContextData, out _))
            return Executability.Invalid;
        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ScriptingManagerViewState.DataKey.TryGetContext(e.ContextData, out ScriptingManagerViewState? manager))
            return;

        string? path = await IFilePickDialogService.Instance.OpenFile("Open lua file", [Filters.Lua, Filters.All]);
        if (path == null)
            return;

        string sourceCode;
        try {
            using CancellationTokenSource ctsActivity = new CancellationTokenSource();
            Task<string> readTextTask = File.ReadAllTextAsync(path, ctsActivity.Token);
            if (!readTextTask.IsCompleted) {
                await Task.WhenAny(readTextTask, Task.Delay(500, ctsActivity.Token));
                if (!readTextTask.IsCompleted) {
                    ActivityTask activity = ActivityManager.Instance.RunTask(async () => {
                        ActivityTask.Current.Progress.SetCaptionAndText("Read file", "Reading script file...", true);
                        await readTextTask;
                    }, ctsActivity);

                    ITopLevel? topLevel;
                    if (IForegroundActivityService.TryGetInstance(out IForegroundActivityService? service) && (topLevel = TopLevelContextUtils.GetTopLevelFromContext()) != null) {
                        await service.WaitForActivity(topLevel, activity, CancellationToken.None);
                    }
                }
            }

            sourceCode = await readTextTask;
        }
        catch (OperationCanceledException) {
            return;
        }
        catch (Exception ex) {
            await IMessageDialogService.Instance.ShowExceptionMessage("File Error", $"Failed to read file '{path}': " + ex.Message, ex);
            return;
        }
        
        Script script = new Script();
        script.SetFilePath(path);
        script.Document.Text = sourceCode;
        script.HasUnsavedChanges = false;
        
        manager.ScriptingManager.Scripts.Add(script);
        manager.SelectedScript = script;
    }
}