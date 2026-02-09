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
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.FileBrowsing.Commands;

public class UploadFileCommand : BaseFileExplorerCommand {
    protected override Executability CanExecuteCore(FileTreeExplorer explorer, CommandEventArgs e) {
        FileTreeExplorerViewState state = FileTreeExplorerViewState.GetInstance(explorer);
        if (state.TreeSelection.HasOneSelectedItem && state.TreeSelection.SelectedItems.First() is FileTreeNodeDirectory) {
            return Executability.Valid;
        }

        return Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(FileTreeExplorer explorer, CommandEventArgs e) {
        FileTreeExplorerViewState state = FileTreeExplorerViewState.GetInstance(explorer);
        if (!state.TreeSelection.HasOneSelectedItem) {
            return;
        }

        BaseFileTreeNode item = state.TreeSelection.SelectedItems.First();
        if (!(item is FileTreeNodeDirectory srcDirectory)) {
            return;
        }

        IFeatureFileSystemInfo? fsInfo = null;
        string srcPath = srcDirectory.FullPath;
        string[]? dstFilePath = null;

        ConnectionAction action = new ConnectionAction(IConnectionLockPair.Lambda(explorer.MemoryEngine, x => x.BusyLock, x => x.Connection)) {
            ActivityCaption = "Upload File",
            Setup = async (action, connection, hasConnectionChanged) => {
                if (!connection.TryGetFeature(out fsInfo)) {
                    await IMessageDialogService.Instance.ShowMessage("Unsupported", "This connection does not support File I/O");
                    return false;
                }

                if (dstFilePath == null) {
                    dstFilePath = await IFilePickDialogService.Instance.OpenMultipleFiles("Select files to upload", [Filters.All]);
                    if (dstFilePath == null) {
                        return false;
                    }
                }

                return true;
            },
            Execute = async (action, connection) => {
                try {
                    foreach (string path in dstFilePath!) {
                        string fileName = Path.GetFileName(path);
                        string newPath = fsInfo!.JoinPaths(srcPath, fileName);

                        await fsInfo!.UploadToFile(newPath, await File.ReadAllBytesAsync(path));   
                    }
                }
                catch (Exception ex) {
                    await IMessageDialogService.Instance.ShowExceptionMessage("Error", "Error uploading file(s)", ex);
                }
            }
        };

        await action.RunAsync();
    }
}