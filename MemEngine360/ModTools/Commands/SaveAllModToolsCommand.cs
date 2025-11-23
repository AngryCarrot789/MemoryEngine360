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

namespace MemEngine360.ModTools.Commands;

public class SaveAllModToolsCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ModToolManager.DataKey.TryGetContext(e.ContextData, out ModToolManager? manager)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    public static async Task<bool> SaveAllAsync(IEnumerable<ModTool> modTools) {
        IList<ModTool> list = modTools as IList<ModTool> ?? modTools.ToList();
        
        using CancellationTokenSource cts = new CancellationTokenSource();
        List<(ModTool, string)> newToolPaths = new List<(ModTool, string)>(list.Count);
        for (int i = 0; i < list.Count; i++) {
            ModTool modTool = list[i];
            ModToolViewState.GetInstance(modTool).RaiseFlushEditorToScript();
            if (!modTool.HasUnsavedChanges) {
                continue;
            }

            if (!File.Exists(modTool.FilePath)) {
                string? path = await IFilePickDialogService.Instance.SaveFile($"Save mod tool '{modTool.Name ?? $"<ModTool #{i + 1}>"}'", [Filters.Lua, Filters.All], modTool.FilePath ?? modTool.Name);
                if (path == null) {
                    return false;
                }

                newToolPaths.Add((modTool, path));
            }
            else {
                newToolPaths.Add((modTool, modTool.FilePath));
            }
        }

        List<(Task Task, string Path, ModTool ModTool)> tasks = new List<(Task Task, string Path, ModTool ModTool)>(newToolPaths.Count);
        foreach ((ModTool tool, string path) pair in newToolPaths) {
            tasks.Add((File.WriteAllTextAsync(pair.path, pair.tool.SourceCode, cts.Token), pair.path, pair.tool));
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

        List<(ModTool, Exception)> exceptions = new List<(ModTool, Exception)>();
        foreach ((Task task, string path, ModTool modTool) in tasks) {
            try {
                await task;
                modTool.SetFilePath(path);
                modTool.HasUnsavedChanges = false;
            }
            catch (OperationCanceledException) {
                // ignroed
            }
            catch (Exception ex) {
                exceptions.Add((modTool, ex));
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
        if (!ModToolManager.DataKey.TryGetContext(e.ContextData, out ModToolManager? manager)) {
            return;
        }

        await SaveAllAsync(manager.ModTools);
    }
}