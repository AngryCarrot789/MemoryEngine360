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

using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity.Selections;

namespace MemEngine360.Engine.FileBrowsing;

public sealed class FileTreeExplorerViewState {
    public FileTreeExplorer Explorer { get; }
    public TreeSelectionModel<BaseFileTreeNode> TreeSelection { get; }

    private FileTreeExplorerViewState(FileTreeExplorer explorer) {
        this.Explorer = explorer;
        this.TreeSelection = new TreeSelectionModel<BaseFileTreeNode>(
            this.Explorer.RootEntry,
            static arg => arg.FileTreeManager != null,
            static arg => arg.ParentDirectory,
            static arg => arg is FileTreeNodeDirectory g ? g.Items : null
        );
    }

    public static FileTreeExplorerViewState GetInstance(FileTreeExplorer explorer) {
        return ((IComponentManager) explorer).GetOrCreateComponent(t => new FileTreeExplorerViewState((FileTreeExplorer) t));
    }
}