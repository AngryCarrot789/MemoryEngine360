﻿// 
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
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.FileBrowsing.Commands;

public class RenameFileCommand : BaseFileExplorerCommand {
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
        IConsoleConnection? connection = engine.Connection;
        if (item.IsTopLevelEntry) {
            await IMessageDialogService.Instance.ShowMessage("Root Directory", "Root directories cannot be renamed");
            return;
        }

        FileTreeNodeDirectory parent = item.ParentDirectory!;
        Debug.Assert(parent != null);

        if (connection == null) {
            await IMessageDialogService.Instance.ShowMessage("Disconnected", "Not connected to a console");
            return;
        }

        if (!connection.TryGetFeature(out IFeatureFileSystemInfo? fsInfo)) {
            await IMessageDialogService.Instance.ShowMessage("Unsupported", "This connection does not support File I/O");
            return;
        }

        string oldPath = item.FullPath;
        string directory = fsInfo.GetDirectoryPath(oldPath);
        string oldName = fsInfo.GetFileName(oldPath);

        SingleUserInputInfo info = new SingleUserInputInfo(oldName) {
            Caption = "Rename",
            Message = "Rename this " + (item is FileTreeNodeDirectory ? "directory" : "file") + $" located at {directory}",
            Label = "New file name",
            Validate = (args) => {
                // ReSharper disable once AccessToModifiedClosure
                if (!fsInfo.IsPathValid(args.Input))
                    args.Errors.Add("Invalid file name");
            }
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) != true) {
            return;
        }

        string newPath = fsInfo.JoinPaths(directory, info.Text);
        Task<IDisposable?> task = TopLevelContextUtils.GetUsefulTopLevel() is ITopLevel topLevel
            ? engine.BusyLocker.BeginBusyOperationWithForegroundActivityAsync(topLevel, "Rename File")
            : engine.BusyLocker.BeginBusyOperationActivityAsync("Rename File");

        using (IDisposable? token = await task) {
            if (token == null || (connection = engine.Connection) == null) {
                return;
            }

            // Connection changed while getting token
            if (!connection.TryGetFeature(out fsInfo)) {
                await IMessageDialogService.Instance.ShowMessage("Unsupported", "This connection does not support File I/O");
                return;
            }

            if (!fsInfo.IsPathValid(newPath)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid path", "Path is invalid: " + newPath);
                return;
            }

            await fsInfo.MoveFile(oldPath, newPath);
        }

        if (parent.FileTreeManager != null) {
            await parent.FileTreeManager.LoadContentsCommand(parent);
            
            BaseFileTreeNode? theItem = parent.Items.FirstOrDefault(x => x.FullPath.EqualsIgnoreCase(newPath));
            if (theItem != null) {
                state.TreeSelection.SetSelection(theItem);
            }
        }
    }
}