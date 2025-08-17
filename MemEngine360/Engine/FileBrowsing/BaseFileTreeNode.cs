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
using System.Diagnostics.CodeAnalysis;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.FileBrowsing;

public delegate void BaseAddressTableEntryEventHandler(BaseFileTreeNode sender);

public delegate void BaseAddressTableEntryParentChangedEventHandler(BaseFileTreeNode sender, FileTreeNodeDirectory? oldPar, FileTreeNodeDirectory? newPar);

public delegate void BaseAddressTableEntryManagerChangedEventHandler(BaseFileTreeNode sender, FileTreeManager? oldATM, FileTreeManager? newATM);

/// <summary>
/// Base class for files within a file tree
/// </summary>
public abstract class BaseFileTreeNode {
    public static readonly DataKey<BaseFileTreeNode> DataKey = DataKey<BaseFileTreeNode>.Create(nameof(BaseFileTreeNode));

    private string? fileName, fullPath;
    private string? errorText;

    /// <summary>
    /// Gets the file tree manager this entry currently exists in
    /// </summary>
    public FileTreeManager? FileTreeManager { get; private set; }

    /// <summary>
    /// Gets or sets the group entry that is a direct parent to this entry
    /// </summary>
    public FileTreeNodeDirectory? ParentDirectory { get; private set; }

    public string? FileName {
        get => this.fileName;
        set {
            if (this.fileName != value) {
                this.fullPath = null;
                this.fileName = value;
                this.FileNameChanged?.Invoke(this);
            }
        }
    }

    public string FullPath {
        get {
            if (this.fullPath != null)
                return this.fullPath;

            if (string.IsNullOrEmpty(this.FileName))
                throw new InvalidOperationException("File entry is invalid: no name");

            FileTreeNodeDirectory? parent = this.ParentDirectory;
            return this.fullPath = (parent != null && !parent.IsRootEntry ? (parent.FullPath + '\\' + this.FileName) : this.FileName);

            // List<BaseFileTreeNode> nodes = new List<BaseFileTreeNode>();
            // for (BaseFileTreeNode? node = this; node != null; node = node.ParentDirectory) {
            //     nodes.Add(node);
            // }
            // return this.fullPath = string.Join('\\', nodes.Select(n => n.FileName));
        }
    }

    public string? ErrorText {
        get => this.errorText;
        set => PropertyHelper.SetAndRaiseINE(ref this.errorText, value, this, static t => t.ErrorTextChanged?.Invoke(t));
    }
    
    /// <summary>
    /// An event fired when our <see cref="ParentDirectory"/> property changes.
    /// If the new parent is attached to a address table manager, our address table manager will be updates
    /// after this event is fired (see <see cref="FileTreeManagerChanged"/>)
    /// </summary>
    public event BaseAddressTableEntryParentChangedEventHandler? ParentChanged;

    /// <summary>
    /// An event fired when our <see cref="FileTreeManager"/> property changes due to address table manager attachment or detachment.
    /// </summary>
    public event BaseAddressTableEntryManagerChangedEventHandler? FileTreeManagerChanged;

    public event BaseAddressTableEntryEventHandler? FileNameChanged;
    public event BaseAddressTableEntryEventHandler? ErrorTextChanged;

    protected BaseFileTreeNode() {
    }

    /// <summary>
    /// Invoked when this child entry is added to or removed from a parent object as a child.
    /// This method is called before our child entries have their <see cref="OnHierarchicalParentChanged"/> method invoked.
    /// <para>
    /// Even if the old/new parent object is a address table manager, this is invoked before <see cref="OnAttachedToFileTreeManager"/> or <see cref="OnDetachedFromFileTreeManager"/>
    /// </para>
    /// </summary>
    /// <param name="oldParent">The previous parent (non-null when removing or moving)</param>
    /// <param name="newParent">The new parent (non-null when adding or moving)</param>
    protected virtual void OnParentChanged(FileTreeNodeDirectory? oldParent, FileTreeNodeDirectory? newParent) {
    }

    /// <summary>
    /// Invoked when one of our hierarchical parents is added to or removed from a parent object as a child to a entry as a child. 
    /// This method is just for clarity and most likely isn't needed
    /// <para>    /// <param name="newParent">The origin's new parent (non-null when adding or moving)</param>
    /// Even if the old/new parent object is a address table manager, this is invoked before <see cref="OnAttachedToFileTreeManager"/> or <see cref="OnDetachedFromFileTreeManager"/>
    /// </para>
    /// <para>
    /// The origin's new parent will equal its <see cref="ParentDirectory"/>
    /// </para>
    /// </summary>
    /// <param name="origin">The parent that was actually added. It may equal this entry's parent entry</param>
    /// <param name="oldParent">The origin's previous parent (non-null when removing or moving)</param>
    protected virtual void OnHierarchicalParentChanged(BaseFileTreeNode origin, FileTreeNodeDirectory? originOldParent) {
    }

    /// <summary>
    /// Invoked when this entry is added to a address table manager. This can be fired by either this entry being added to
    /// a group entry which exists in a address table manager, or when we are added as a top-level entry in a address table manager
    /// <para>
    /// This is invoked BEFORE <see cref="FileTreeManagerChanged"/>
    /// </para>
    /// </summary>
    /// <param name="origin">
    /// The entry that directly caused the address table manager to become attached (either by being added as a top-level entry
    /// or being added into a group entry that exists in a address table manager). May equal the current instance
    /// </param>
    protected virtual void OnAttachedToFileTreeManager(BaseFileTreeNode origin) {
    }

    /// <summary>
    /// Invoked when this entry is removed from a address table manager. This can be fired by either this entry being removed from
    /// a group entry which exists in a address table manager, or when we are removed as a top-level entry from a address table manager.
    /// <para>
    /// This is invoked BEFORE <see cref="FileTreeManagerChanged"/>
    /// </para>
    /// </summary>
    /// <param name="origin">
    /// The entry that directly caused the address table manager to become detached (either by being removed as a top-level entry
    /// or being removed from a container entry that exists in a address table manager). May equal the current instance
    /// </param>
    /// <param name="oldFileTreeManager">The address table manager that we previously existed in</param>
    protected virtual void OnDetachedFromFileTreeManager(BaseFileTreeNode origin, FileTreeManager oldFileTreeManager) {
    }

    public static bool CheckHaveParentsAndAllMatch(ISelectionManager<BaseFileTreeNode> manager, [NotNullWhen(true)] out FileTreeNodeDirectory? sameParent) {
        return CheckHaveParentsAndAllMatch(manager.SelectedItems, out sameParent);
    }

    public static bool CheckHaveParentsAndAllMatch(IEnumerable<BaseFileTreeNode> items, [NotNullWhen(true)] out FileTreeNodeDirectory? sameParent) {
        using IEnumerator<BaseFileTreeNode> enumerator = items.GetEnumerator();
        if (!enumerator.MoveNext())
            throw new InvalidOperationException("Expected items to contain at least 1 item");

        if ((sameParent = enumerator.Current.ParentDirectory) == null)
            return false;

        while (enumerator.MoveNext()) {
            if (!ReferenceEquals(enumerator.Current.ParentDirectory, sameParent)) {
                return false;
            }
        }

        return true;
    }

    internal static void InternalSetATM(BaseFileTreeNode entry, FileTreeManager fileTreeManager) {
        entry.FileTreeManager = fileTreeManager;
    }

    // User added some entry into group entry
    internal static void InternalOnAddedToEntry(BaseFileTreeNode entry, FileTreeNodeDirectory newParent) {
        Debug.Assert(entry.ParentDirectory == null, "Did not expect entry to be in a group entry when adding it to another");
        Debug.Assert(entry.FileTreeManager == null, "Did not expect entry to be in a address table manager when adding to a group entry");

        entry.ParentDirectory = newParent;
        entry.fullPath = null;
        entry.OnParentChanged(null, newParent);
        entry.ParentChanged?.Invoke(entry, null, newParent);

        if (newParent.FileTreeManager != null) {
            entry.FileTreeManager = newParent.FileTreeManager;
            entry.OnAttachedToFileTreeManager(entry);
            entry.FileTreeManagerChanged?.Invoke(entry, null, entry.FileTreeManager);
        }

        if (entry is FileTreeNodeDirectory asGroup) {
            foreach (BaseFileTreeNode child in asGroup.Items) {
                RecurseChildren(child, entry);
            }
        }

        return;

        static void RecurseChildren(BaseFileTreeNode child, BaseFileTreeNode origin) {
            child.OnHierarchicalParentChanged(origin, null);
            if (origin.FileTreeManager != null) {
                child.FileTreeManager = origin.FileTreeManager;
                child.OnAttachedToFileTreeManager(origin);
                child.FileTreeManagerChanged?.Invoke(child, null, child.FileTreeManager);
            }

            if (child is FileTreeNodeDirectory childAsComposition) {
                foreach (BaseFileTreeNode nextChild in childAsComposition.Items) {
                    RecurseChildren(nextChild, origin);
                }
            }
        }
    }

    internal static void InternalOnRemovedFromEntry(BaseFileTreeNode entry, FileTreeNodeDirectory oldParent) {
        Debug.Assert(entry.ParentDirectory != null, "Did not expect entry to not be in a group entry when removing it from another");

        // While child entries are notified of address table manager detachment first, should we do the same here???
        FileTreeManager? oldATM = entry.FileTreeManager;
        if (oldATM != null) {
            entry.FileTreeManager = null;
            entry.OnDetachedFromFileTreeManager(entry, oldATM);
            entry.FileTreeManagerChanged?.Invoke(entry, oldATM, null);
        }

        entry.ParentDirectory = null;
        entry.fullPath = null;
        entry.OnParentChanged(oldParent, null);
        entry.ParentChanged?.Invoke(entry, oldParent, null);

        if (entry is FileTreeNodeDirectory asComposition) {
            foreach (BaseFileTreeNode child in asComposition.Items) {
                RecurseChildren(child, entry, oldParent, oldATM);
            }
        }

        return;

        static void RecurseChildren(BaseFileTreeNode child, BaseFileTreeNode origin, FileTreeNodeDirectory originOldParent, FileTreeManager? oldATM) {
            // Detach from address table manager first, then notify hierarchical parent removed from entry
            if (child.FileTreeManager != null) {
                Debug.Assert(oldATM == child.FileTreeManager, "Expected oldATM and our address table manager to match");

                child.FileTreeManager = null;
                child.OnDetachedFromFileTreeManager(origin, oldATM);
                child.FileTreeManagerChanged?.Invoke(child, oldATM, null);
            }

            child.OnHierarchicalParentChanged(origin, originOldParent);
            if (child is FileTreeNodeDirectory entry) {
                foreach (BaseFileTreeNode nextChild in entry.Items) {
                    RecurseChildren(nextChild, origin, originOldParent, oldATM);
                }
            }
        }
    }
}