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

using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Engine.FileBrowsing;

public delegate void FileTreeNodeDirectoryEventHandler(FileTreeNodeDirectory sender);

/// <summary>
/// A group entry contains its own entry hierarchy which can be rendered like a raster entry
/// </summary>
public sealed class FileTreeNodeDirectory : BaseFileTreeNode {
    private bool hasLoadedContents;

    public ObservableList<BaseFileTreeNode> Items { get; }

    public bool HasLoadedContents {
        get => this.hasLoadedContents;
        set => PropertyHelper.SetAndRaiseINE(ref this.hasLoadedContents, value, this, static t => t.HasLoadedContentsChanged?.Invoke(t));
    }

    public event FileTreeNodeDirectoryEventHandler? HasLoadedContentsChanged;

    public FileTreeNodeDirectory() {
        this.Items = new ObservableList<BaseFileTreeNode>();
        this.Items.BeforeItemAdded += (list, index, item) => {
            if (item == null)
                throw new ArgumentNullException(nameof(item), "Cannot add a null operation");
            if (item.ParentDirectory == this)
                throw new InvalidOperationException("Entry already exists in this entry. It must be removed first");
            if (item.ParentDirectory != null)
                throw new InvalidOperationException("Entry already exists in another container. It must be removed first");
        };

        this.Items.BeforeItemReplace += (list, index, oldItem, newItem) => {
            if (newItem == null)
                throw new ArgumentNullException(nameof(newItem), "Cannot replace operation with null");
        };

        this.Items.ItemsAdded += (list, index, items) => {
            foreach (BaseFileTreeNode? operation in items) {
                InternalOnAddedToEntry(operation, this);
            }
        };

        this.Items.ItemsRemoved += (list, index, removedItems) => {
            foreach (BaseFileTreeNode? operation in removedItems) {
                InternalOnRemovedFromEntry(operation, this);
            }
        };

        this.Items.ItemReplaced += (list, index, oldItem, newItem) => {
            InternalOnRemovedFromEntry(oldItem, this);
            InternalOnAddedToEntry(newItem, this);
        };
    }

    static FileTreeNodeDirectory() {
    }

    public int IndexOf(BaseFileTreeNode entry) {
        return ReferenceEquals(entry.ParentDirectory, this) ? this.Items.IndexOf(entry) : -1;
    }

    public bool Contains(BaseFileTreeNode entry) {
        return this.IndexOf(entry) != -1;
    }

    internal static FileTreeNodeDirectory InternalCreateRoot(FileTreeExplorer fileTreeExplorer) {
        FileTreeNodeDirectory entry = new FileTreeNodeDirectory();
        InternalSetATM(entry, fileTreeExplorer);
        return entry;
    }

    public void Clear() => this.Items.Clear();
}