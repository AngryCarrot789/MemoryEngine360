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

public class SaveAllScriptsCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ScriptingManager.DataKey.TryGetContext(e.ContextData, out ScriptingManager? manager)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    public static async Task<bool> SaveAllAsync(IEnumerable<Script> scripts) {
        IList<Script> list = scripts as IList<Script> ?? scripts.ToList();
        
        using CancellationTokenSource cts = new CancellationTokenSource();
        List<(Script, string)> newScriptPaths = new List<(Script, string)>(list.Count);

        for (int i = 0; i < list.Count; i++) {
            Script script = list[i];
            if (!script.HasUnsavedChanges) {
                continue;
            }

            if (!File.Exists(script.FilePath)) {
                string? path = await IFilePickDialogService.Instance.SaveFile($"Save script '{script.Name ?? $"<script #{i + 1}>"}'", [Filters.Lua, Filters.All], script.FilePath ?? script.Name);
                if (path == null) {
                    return false;
                }

                newScriptPaths.Add((script, path));
            }
            else {
                newScriptPaths.Add((script, script.FilePath));
            }
        }
        
        List<(Task Task, string Path, Script Script)> tasks = new List<(Task Task, string Path, Script Script)>(newScriptPaths.Count);
        foreach ((Script script, string path) pair in newScriptPaths) {
            tasks.Add((File.WriteAllTextAsync(pair.path, pair.script.Document.Text, cts.Token), pair.path, pair.script));
        }

        Task saveAllTask = Task.WhenAll(tasks.Select(x => x.Task));
        await Task.WhenAny(saveAllTask, Task.Delay(500, cts.Token));
        if (!saveAllTask.IsCompleted) {
            ActivityTask activity = ActivityManager.Instance.RunTask(async () => {
                ActivityTask activity = ActivityTask.Current;
                using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(activity.CancellationToken);
                CancellationToken linkedToken = linkedCts.Token; // becomes cancelled when activity completes or is marked as cancelled
                _ = Task.Run(async () => {
                    while (!linkedToken.IsCancellationRequested) {
                        try {
                            int count = tasks.Count(x => !x.Task.IsCompleted);
                            if (count < 1) {
                                activity.Progress.SetCaptionAndText("Saving files", $"All files saved");
                                return; // exit when all are done
                            }

                            activity.Progress.SetCaptionAndText("Saving files", $"Saving {count} file{Lang.S(count)}");
                            await Task.Delay(250, linkedToken);
                        }
                        catch (OperationCanceledException) {
                            return;
                        }
                    }
                }, CancellationToken.None);

                await saveAllTask;
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

        List<(Script, Exception)> exceptions = new List<(Script, Exception)>();
        foreach ((Task task, string path, Script script) in tasks) {
            try {
                await task;
                script.SetFilePath(path);
                script.HasUnsavedChanges = false;
            }
            catch (OperationCanceledException) {
                // ignroed
            }
            catch (Exception ex) {
                exceptions.Add((script, ex));
            }
        }

        if (exceptions.Count > 0) {
            string NL = Environment.NewLine;
            string errorMsg = string.Join(Environment.NewLine, exceptions.Select(x => x.Item1.Name != null ? (x.Item1.Name + ": " + x.Item2.Message) : x.Item2.Message));
            await IMessageDialogService.Instance.ShowMessage("Error", $"One or more errors occurred while saving files{NL}{NL}{errorMsg}");
        }

        return true;
    }
    
    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ScriptingManager.DataKey.TryGetContext(e.ContextData, out ScriptingManager? manager)) {
            return;
        }
        
        await SaveAllAsync(manager.Scripts);
    }
}