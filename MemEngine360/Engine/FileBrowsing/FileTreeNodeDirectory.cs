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

using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Engine.FileBrowsing;

/// <summary>
/// A group entry contains its own entry hierarchy which can be rendered like a raster entry
/// </summary>
public sealed class FileTreeNodeDirectory : BaseFileTreeNode {
    public ObservableList<BaseFileTreeNode> Items { get; }

    public bool HasLoadedContents {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.HasLoadedContentsChanged);
    }

    public event EventHandler? HasLoadedContentsChanged;

    public FileTreeNodeDirectory() {
        this.Items = new ObservableList<BaseFileTreeNode>();
        this.Items.ValidateAdd += (list, e) => {
            foreach (BaseFileTreeNode item in e.Items) {
                if (item == null)
                    throw new InvalidOperationException("Attempt to add null entry");
                if (item.ParentDirectory == this)
                    throw new InvalidOperationException("Entry already exists in this entry. It must be removed first");
                if (item.ParentDirectory != null)
                    throw new InvalidOperationException("Entry already exists in another container. It must be removed first");   
            }
        };

        this.Items.ValidateReplace += (list, e) => {
            if (e.NewItem == null)
                throw new ArgumentException("New item cannot be null");
        };

        this.Items.ItemsAdded += (list, e) => {
            foreach (BaseFileTreeNode? node in e.Items) {
                InternalOnAddedToEntry(node, this);
            }
        };

        this.Items.ItemsRemoved += (list, e) => {
            foreach (BaseFileTreeNode? node in e.Items) {
                InternalOnRemovedFromEntry(node, this);
            }
        };

        this.Items.ItemReplaced += (list, e) => {
            InternalOnRemovedFromEntry(e.OldItem, this);
            InternalOnAddedToEntry(e.NewItem, this);
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