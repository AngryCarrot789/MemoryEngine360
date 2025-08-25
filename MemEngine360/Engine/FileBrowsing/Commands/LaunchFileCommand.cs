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

namespace MemEngine360.Engine.FileBrowsing.Commands;

public class LaunchFileCommand : BaseFileExplorerCommand {
    protected override Executability CanExecuteCore(IFileExplorerUI explorer, CommandEventArgs e) {
        return explorer.SelectionManager.Count == 1 ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(IFileExplorerUI explorer, CommandEventArgs e) {
        if (explorer.SelectionManager.Count != 1) {
            return;
        }

        IFileTreeNodeUI item = explorer.SelectionManager.SelectedItemList[0];
        if (item.Entry is FileTreeNodeFile file) {
            IConsoleConnection? connection;
            MemoryEngine engine = explorer.FileTreeExplorer.MemoryEngine;
            using IDisposable? token = await engine.BeginBusyOperationActivityAsync("Launch File");
            if (token != null && (connection = engine.Connection) != null) {
                if (connection.TryGetFeature(out IFeatureFileSystemInfo? fsInfo)) {
                    await fsInfo.LaunchFile(file.FullPath);
                }
            }
        }
    }
}