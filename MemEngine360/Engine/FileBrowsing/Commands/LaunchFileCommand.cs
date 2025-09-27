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
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.FileBrowsing.Commands;

public class LaunchFileCommand : BaseFileExplorerCommand {
    protected override Executability CanExecuteCore(FileTreeExplorer explorer, CommandEventArgs e) {
        FileTreeExplorerViewState state = FileTreeExplorerViewState.GetInstance(explorer);
        return state.TreeSelection.HasOneSelectedItem ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(FileTreeExplorer explorer, CommandEventArgs e) {
        FileTreeExplorerViewState state = FileTreeExplorerViewState.GetInstance(explorer);
        if (!state.TreeSelection.HasOneSelectedItem) {
            return;
        }

        BaseFileTreeNode item = state.TreeSelection.SelectedItems.First();
        if (item is FileTreeNodeFile file) {
            IConsoleConnection? connection;
            MemoryEngine engine = explorer.MemoryEngine;

            Task<IDisposable?> task = TopLevelContextUtils.GetUsefulTopLevel() is ITopLevel topLevel
                ? engine.BusyLocker.BeginBusyOperationWithForegroundActivityAsync(topLevel, "Launch File")
                : engine.BusyLocker.BeginBusyOperationActivityAsync("Launch File");

            using IDisposable? token = await task;
            if (token != null && (connection = engine.Connection) != null) {
                if (connection.TryGetFeature(out IFeatureFileSystemInfo? fsInfo)) {
                    await fsInfo.LaunchFile(file.FullPath);
                }
            }
        }
    }
}