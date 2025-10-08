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
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Engine.FileBrowsing.Commands;

public class CreateDirectoryInDirectoryCommand : BaseFileExplorerCommand {
    protected override Executability CanExecuteCore(FileTreeExplorer explorer, CommandEventArgs e) {
        if (explorer.MemoryEngine.Connection?.HasFeature<IFeatureFileSystemInfo>() != true)
            return Executability.ValidButCannotExecute;

        FileTreeExplorerViewState state = FileTreeExplorerViewState.GetInstance(explorer);
        if (!state.TreeSelection.HasOneSelectedItem)
            return Executability.ValidButCannotExecute;
        
        BaseFileTreeNode item = state.TreeSelection.SelectedItems.First();
        if (!(item is FileTreeNodeDirectory))
            return Executability.ValidButCannotExecute;
        
        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(FileTreeExplorer explorer, CommandEventArgs e) {
        FileTreeExplorerViewState state = FileTreeExplorerViewState.GetInstance(explorer);
        if (!state.TreeSelection.HasOneSelectedItem) {
            return;
        }

        BaseFileTreeNode item = state.TreeSelection.SelectedItems.First();
        if (!(item is FileTreeNodeDirectory)) {
            return;
        }
        
        string parentDirPath = item.FullPath;

        IFeatureFileSystemInfo? fsInfo = null;
        string? newPath = null;

        ConnectionAction action = new ConnectionAction(IConnectionLockPair.Lambda(explorer.MemoryEngine, x => x.BusyLock, x => x.Connection)) {
            ActivityCaption = "Launch File",
            Setup = async (action, connection, hasConnectionChanged) => {
                if (!connection.TryGetFeature(out fsInfo)) {
                    await IMessageDialogService.Instance.ShowMessage("Unsupported", "This connection does not support File I/O");
                    return false;
                }

                SingleUserInputInfo info = new SingleUserInputInfo("") {
                    Caption = "Create Directory",
                    Label = "Directory Name",
                    MinimumDialogWidthHint = 500,
                    Validate = (args) => {
                        if (!fsInfo.IsPathValid(args.Input))
                            args.Errors.Add("Invalid name");
                    }
                };

                info.TextChanged += s => {
                    s.Footer = "Full Path: " + fsInfo.JoinPaths(parentDirPath, s.Text);
                };

                info.Text = "New Directory";

                if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) != true) {
                    return false;
                }

                newPath = fsInfo.JoinPaths(parentDirPath, info.Text);
                if (!fsInfo.IsPathValid(newPath)) {
                    await IMessageDialogService.Instance.ShowMessage("Invalid path", "Path is invalid: " + newPath);
                    return false;
                }
                
                return true;
            },
            Execute = async (action, connection) => {
                await fsInfo!.CreateDirectory(newPath!);
                await state.Explorer.SelectFilePath(newPath!, fsInfo!, true);
            }
        };

        await action.RunAsync();
    }
}