// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using System.Diagnostics;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Engine.SavedAddressing;

public delegate void AddressTableGroupEntryGroupAddressChangedEventHandler(AddressTableGroupEntry sender, uint? oldGroupAddress, uint? newGroupAddress);

public delegate void AddressTableGroupEntryEventHandler(AddressTableGroupEntry sender);

/// <summary>
/// A group entry contains its own entry hierarchy which can be rendered like a raster entry
/// </summary>
public sealed class AddressTableGroupEntry : BaseAddressTableEntry {
    private readonly SuspendableObservableList<BaseAddressTableEntry> items;
    private uint groupAddress;
    private bool isAddressAbsolute = true;

    public ReadOnlyObservableList<BaseAddressTableEntry> Items { get; }

    public bool IsRootEntry => this.Parent == null;

    /// <summary>
    /// Gets or sets this group's base address, used for relative addressing. May be relative
    /// to parent, so use <see cref="AbsoluteAddress"/> for absolute address
    /// </summary>
    public uint GroupAddress {
        get => this.groupAddress;
        set {
            uint? oldGroupAddress = this.groupAddress;
            if (oldGroupAddress == value)
                return;

            this.groupAddress = value;
            this.GroupAddressChanged?.Invoke(this, oldGroupAddress, value);
        }
    }

    /// <summary>
    /// Gets or sets if <see cref="GroupAddress"/> is absolute and not relative to <see cref="BaseAddressTableEntry.Parent"/>
    /// </summary>
    public bool IsAddressAbsolute {
        get => this.isAddressAbsolute;
        set {
            if (this.isAddressAbsolute == value)
                return;

            this.isAddressAbsolute = value;
            this.IsAddressAbsoluteChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets the absolute resolved address for this group
    /// </summary>
    public uint AbsoluteAddress => this.isAddressAbsolute ? this.groupAddress : ((this.Parent?.AbsoluteAddress ?? 0) + this.groupAddress);

    public event AddressTableGroupEntryGroupAddressChangedEventHandler? GroupAddressChanged;
    public event AddressTableGroupEntryEventHandler? IsAddressAbsoluteChanged;

    public AddressTableGroupEntry() {
        this.items = new SuspendableObservableList<BaseAddressTableEntry>();
        this.Items = new ReadOnlyObservableList<BaseAddressTableEntry>(this.items);
    }

    static AddressTableGroupEntry() {
    }

    public void AddEntry(BaseAddressTableEntry entry) => this.InsertEntry(this.items.Count, entry);

    public void InsertEntry(int index, BaseAddressTableEntry entry) {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Negative indices not allowed");
        if (index > this.items.Count)
            throw new ArgumentOutOfRangeException(nameof(index), $"Index is beyond the range of this list: {index} > count({this.items.Count})");

        if (entry == null)
            throw new ArgumentNullException(nameof(entry), "Cannot add a null entry");
        if (entry.Parent == this)
            throw new InvalidOperationException("Entry already exists in this entry. It must be removed first");
        if (entry.Parent != null)
            throw new InvalidOperationException("Entry already exists in another container. It must be removed first");

        // Update before insertion since it fires an event, IndexInParent would be -1 in handlers
        for (int i = this.items.Count - 1; i >= index; i--) {
            InternalSetIndexInParent(this.items[i], i + 1);
        }

        using (this.items.SuspendEvents()) {
            this.items.Insert(index, entry);
            InternalOnAddedToEntry(index, entry, this);
        }
    }

    public void AddEntries(IEnumerable<BaseAddressTableEntry> layers) {
        foreach (BaseAddressTableEntry entry in layers) {
            this.AddEntry(entry);
        }
    }

    public bool RemoveEntry(BaseAddressTableEntry entry) {
        if (!ReferenceEquals(entry.Parent, this))
            return false;
        this.RemoveEntryAt(InternalIndexInParent(entry));

        Debug.Assert(entry.Parent != this, "Entry parent not updated, still ourself");
        Debug.Assert(entry.Parent == null, "Entry parent not updated to null");
        return true;
    }

    public void RemoveEntryAt(int index) {
        BaseAddressTableEntry entry = this.items[index];
        InternalOnPreRemoveFromOwner(entry);
        for (int i = this.items.Count - 1; i > index /* not >= since we remove one at index */; i--) {
            InternalSetIndexInParent(this.items[i], i - 1);
        }

        using (this.items.SuspendEvents()) {
            this.items.RemoveAt(index);
            InternalOnRemovedFromEntry(entry, this);
        }
    }

    public void RemoveEntries(IEnumerable<BaseAddressTableEntry> entries) {
        foreach (BaseAddressTableEntry entry in entries) {
            this.RemoveEntry(entry);
        }
    }

    public int IndexOf(BaseAddressTableEntry entry) {
        return ReferenceEquals(entry.Parent, this) ? InternalIndexInParent(entry) : -1;
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
        foreach (BaseAddressTableEntry entry in this.items) {
            if (entry is AddressTableGroupEntry group)
                group.GetAllEntries(entries);
            else
                entries.Add((AddressTableEntry) entry);
        }
    }

    public void Clear() {
        for (int i = this.items.Count - 1; i >= 0; i--) {
            this.RemoveEntryAt(i);
        }
    }
}