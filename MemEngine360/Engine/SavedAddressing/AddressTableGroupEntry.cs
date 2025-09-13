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
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Engine.SavedAddressing;

/// <summary>
/// A group entry contains its own entry hierarchy which can be rendered like a raster entry
/// </summary>
public sealed class AddressTableGroupEntry : BaseAddressTableEntry {
    /// <summary>
    /// Gets this group's entries
    /// </summary>
    public ObservableList<BaseAddressTableEntry> Items { get; }

    public bool IsRootEntry => this.Parent == null;

    public AddressTableGroupEntry() {
        this.Items = new ObservableList<BaseAddressTableEntry>();
        this.Items.BeforeItemAdded += (list, index, item) => {
            if (item == null)
                throw new ArgumentNullException(nameof(item), "Cannot add a null entry");
            if (item.Parent == this)
                throw new InvalidOperationException("Entry already exists in this entry. It must be removed first");
            if (item.Parent != null)
                throw new InvalidOperationException("Entry already exists in another container. It must be removed first");
        };
        this.Items.ItemsAdded += (list, index, items) => items.ForEach(this, InternalOnAddedToEntry);
        this.Items.ItemsRemoved += (list, index, removedItems) => removedItems.ForEach(InternalOnRemovedFromEntry);
        this.Items.BeforeItemReplace += (list, index, oldItem, newItem) => {
            if (newItem == null)
                throw new ArgumentNullException(nameof(newItem), "Cannot replace item with null");
            if (newItem.Parent == this)
                throw new InvalidOperationException("Replacement entry already exists in this entry. It must be removed first");
            if (newItem.Parent != null)
                throw new InvalidOperationException("Replacement entry already exists in another container. It must be removed first");
        };
        this.Items.ItemReplaced += (list, index, oldItem, newItem) => {
            InternalOnRemovedFromEntry(oldItem);
            InternalOnAddedToEntry(newItem, this);
        };
    }

    static AddressTableGroupEntry() {
    }

    public void MoveEntryTo(BaseAddressTableEntry entry, AddressTableGroupEntry newParent) {
        if (!ReferenceEquals(entry.Parent, this))
            throw new InvalidOperationException("Entry does not exist in this group");

        bool removed = this.Items.Remove(entry);
        Debug.Assert(removed, "Not in list but parent reference is set");

        newParent.Items.Add(entry);
    }

    public int IndexOf(BaseAddressTableEntry entry) {
        return ReferenceEquals(entry.Parent, this) ? this.Items.IndexOf(entry) : -1;
    }

    public bool Contains(BaseAddressTableEntry entry) {
        return this.IndexOf(entry) != -1;
    }

    /// <summary>
    /// Returns true when the given item is equal to one of the parents in our hierarchy
    /// </summary>
    /// <param name="item">The item to check if it's a hierarchical parent relative to this entry</param>
    /// <param name="startAtThis">
    /// When true, this method returns true when the item is equal to the current instance
    /// </param>
    /// <returns>See summary</returns>
    public bool IsParentInHierarchy(AddressTableGroupEntry? item, bool startAtThis = true) {
        for (AddressTableGroupEntry? parent = (startAtThis ? this : this.Parent); item != null; item = item.Parent) {
            if (ReferenceEquals(parent, item)) {
                return true;
            }
        }

        return false;
    }

    public int LowestIndexOf(IEnumerable<BaseAddressTableEntry> items) {
        int minIndex = int.MaxValue;
        foreach (BaseAddressTableEntry entry in items) {
            int index = this.IndexOf(entry);
            if (index != -1) {
                minIndex = Math.Min(minIndex, index);
            }
        }

        return minIndex == int.MaxValue ? -1 : minIndex;
    }

    internal static AddressTableGroupEntry InternalCreateRoot(AddressTableManager addressTableManager) {
        AddressTableGroupEntry entry = new AddressTableGroupEntry();
        InternalSetATM(entry, addressTableManager);
        return entry;
    }

    public void GetAllEntries(List<AddressTableEntry> entries) {
        foreach (BaseAddressTableEntry entry in this.Items) {
            if (entry is AddressTableGroupEntry group)
                group.GetAllEntries(entries);
            else
                entries.Add((AddressTableEntry) entry);
        }
    }

    public void Clear() => this.Items.Clear();

    public override BaseAddressTableEntry CreateClone() {
        AddressTableGroupEntry entry = new AddressTableGroupEntry() {
            Description = this.Description
        };

        entry.Items.AddRange(this.Items.Select(x => x.CreateClone()));
        return entry;
    }
}