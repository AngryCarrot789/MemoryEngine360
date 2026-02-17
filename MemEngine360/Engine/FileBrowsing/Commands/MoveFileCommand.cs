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
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Engine.FileBrowsing.Commands;

public class MoveFileCommand : BaseFileExplorerCommand {
    protected override Executability CanExecuteCore(FileTreeExplorer explorer, CommandEventArgs e) {
        FileTreeExplorerViewState state = FileTreeExplorerViewState.GetInstance(explorer);
        IConsoleConnection? connection = state.Explorer.MemoryEngine.Connection;
        if (connection == null)
            return Executability.ValidButCannotExecute;
        if (!connection.HasFeature<IFeatureFileSystemInfo>())
            return Executability.Invalid;
        if (!state.TreeSelection.HasOneSelectedItem)
            return Executability.ValidButCannotExecute;
        if (state.TreeSelection.SelectedItems.First().IsTopLevelEntry)
            return Executability.ValidButCannotExecute;
        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(FileTreeExplorer explorer, CommandEventArgs e) {
        FileTreeExplorerViewState state = FileTreeExplorerViewState.GetInstance(explorer);
        if (!state.TreeSelection.HasOneSelectedItem) {
            return;
        }

        MemoryEngine engine = explorer.MemoryEngine;
        BaseFileTreeNode item = state.TreeSelection.SelectedItems.First();
        if (item.IsTopLevelEntry) {
            await IMessageDialogService.Instance.ShowMessage("Root Directory", "Root directories cannot be moved");
            return;
        }

        FileTreeNodeDirectory parent = item.ParentDirectory!;
        Debug.Assert(parent != null);
        FileTreeExplorer? fileTree = parent.FileTreeManager;
        if (fileTree == null) {
            return;
        }

        string oldPath = item.FullPath;
        string? newPath = null;
        IFeatureFileSystemInfo? fsInfo = null;

        ConnectionAction action = new ConnectionAction(IConnectionLockPair.Lambda(engine, x => x.BusyLock, x => x.Connection)) {
            ActivityCaption = "Move File",
            Setup = async (action, connection, hasConnectionChanged) => {
                if (!connection.TryGetFeature(out fsInfo)) {
                    await IMessageDialogService.Instance.ShowMessage("Unsupported", "This connection does not support File I/O");
                    return false;
                }

                SingleUserInputInfo info = new SingleUserInputInfo(oldPath) {
                    Caption = "Move",
                    Message = "Move this " + (item is FileTreeNodeDirectory ? "directory" : "file"),
                    Label = "New Path",
                    MinimumDialogWidthHint = 500,
                    Validate = (args) => {
                        if (!fsInfo.IsPathValid(args.Input))
                            args.Errors.Add("Invalid path");
                    },
                    Footer = "Tip: you can use this to rename something too"
                };

                if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) != true) {
                    return false;
                }

                newPath = info.Text;
                return true;
            },
            Execute = async (action, connection) => {
                await fsInfo!.MoveFile(oldPath, newPath!);
                await fileTree.SelectFilePath(newPath!, fsInfo, true);
            }
        };

        await action.RunAsync();
    }
}