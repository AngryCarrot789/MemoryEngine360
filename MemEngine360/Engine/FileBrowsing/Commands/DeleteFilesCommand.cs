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

using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using PFXToolKitUI.Activities;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.FileBrowsing.Commands;

public class DeleteFilesCommand : BaseFileExplorerCommand {
    protected override Executability CanExecuteCore(FileTreeExplorer explorer, CommandEventArgs e) {
        FileTreeExplorerViewState state = FileTreeExplorerViewState.GetInstance(explorer);
        return state.TreeSelection.Count > 0 ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(FileTreeExplorer explorer, CommandEventArgs e) {
        FileTreeExplorerViewState state = FileTreeExplorerViewState.GetInstance(explorer);
        if (state.TreeSelection.Count < 1) {
            return;
        }

        IFeatureFileSystemInfo? fsInfo = null;
        List<string>? paths = null;
        List<FileTreeNodeDirectory>? refreshParents = null;

        ConnectionAction action = new ConnectionAction(IConnectionLockPair.Lambda(explorer.MemoryEngine, x => x.BusyLock, x => x.Connection)) {
            ActivityCaption = "Launch File",
            Setup = async (action, connection, hasConnectionChanged) => {
                if (!connection.TryGetFeature(out fsInfo)) {
                    await IMessageDialogService.Instance.ShowMessage("Unsupported", "This connection does not support File I/O");
                    return false;
                }

                List<BaseFileTreeNode> allSelectedItems = state.TreeSelection.SelectedItems.ToList();
                Dictionary<FileTreeNodeDirectory, List<(BaseFileTreeNode, int)>> trueSelection =
                    HierarchicalDuplicationUtils.GetEffectiveOrderedDuplication(allSelectedItems, x => x.ParentDirectory, x => x.ParentDirectory!.IndexOf(x));

                paths = new List<string>();
                refreshParents = new List<FileTreeNodeDirectory>();
                foreach (KeyValuePair<FileTreeNodeDirectory, List<(BaseFileTreeNode, int)>> entry in trueSelection) {
                    refreshParents.Add(entry.Key);
                    paths.AddRange(entry.Value.Select(tuple => tuple.Item1.FullPath));
                }

                MessageBoxInfo info = new MessageBoxInfo {
                    Caption = $"Delete {paths.Count} item{Lang.S(paths.Count)}",
                    Message = "Delete the selection?" + Environment.NewLine + string.Join(Environment.NewLine, paths),
                    YesOkText = "Delete",
                    Buttons = MessageBoxButtons.OKCancel
                };

                return await IMessageDialogService.Instance.ShowMessage(info) == MessageBoxResult.OK;
            },
            Execute = async (action, connection) => {
                ActivityTask activity = ActivityManager.Instance.RunTask(async () => {
                    IActivityProgress progress = ActivityTask.Current.Progress;
                    progress.Caption = $"Deleting {paths!.Count} item{Lang.S(paths.Count)}";

                    using PopCompletionStateRangeToken token = progress.CompletionState.PushCompletionRange(0, 1.0 / paths.Count);
                    foreach (string path in paths!) {
                        progress.Text = path;
                        progress.CompletionState.OnProgress(1);
                        await fsInfo!.DeleteFileSystemEntryRecursive(path);
                    }
                });

                if (IForegroundActivityService.TryGetInstance(out IForegroundActivityService? service)) {
                    ITopLevel? topLevel = TopLevelContextUtils.GetTopLevelFromContext();
                    if (topLevel != null) {
                        await service.DelayedWaitForActivity(topLevel, activity, 100);
                    }
                }

                await activity;

                foreach (FileTreeNodeDirectory dir in refreshParents!) {
                    if (dir.FileTreeManager != null && dir.ParentDirectory != null) {
                        await state.Explorer.ReloadContentsOrParent(dir, fsInfo!, false, true);
                    }
                }
            }
        };
        await action.RunAsync();
    }
}