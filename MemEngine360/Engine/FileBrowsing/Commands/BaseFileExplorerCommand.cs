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

using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Engine.FileBrowsing.Commands;

public abstract class BaseFileExplorerCommand : Command {
    protected sealed override Executability CanExecuteCore(CommandEventArgs e) {
        if (!FileTreeExplorer.DataKey.TryGetContext(e.ContextData, out FileTreeExplorer? explorer))
            return Executability.Invalid;

        return this.CanExecuteCore(explorer, e);
    }

    protected sealed override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!FileTreeExplorer.DataKey.TryGetContext(e.ContextData, out FileTreeExplorer? explorer))
            return Task.CompletedTask;

        return this.ExecuteCommandAsync(explorer, e);
    }

    protected abstract Executability CanExecuteCore(FileTreeExplorer explorer, CommandEventArgs e);
    
    protected abstract Task ExecuteCommandAsync(FileTreeExplorer explorer, CommandEventArgs e);
}