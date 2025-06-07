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
using System.Diagnostics.CodeAnalysis;
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.SavedAddressing;

public delegate void BaseAddressTableEntryEventHandler(BaseAddressTableEntry sender);

public delegate void BaseAddressTableEntryParentChangedEventHandler(BaseAddressTableEntry sender, AddressTableGroupEntry? oldPar, AddressTableGroupEntry? newPar);

public delegate void BaseAddressTableEntryManagerChangedEventHandler(BaseAddressTableEntry sender, AddressTableManager? oldATM, AddressTableManager? newATM);

/// <summary>
/// The base class for an object in the entry hierarchy for a address table manager
/// </summary>
public abstract class BaseAddressTableEntry : ITransferableData {
    public static readonly DataKey<BaseAddressTableEntry> DataKey = DataKey<BaseAddressTableEntry>.Create("BaseLayerTreeObject");

    private string? description;

    // Updated by group entry add/remove/move operations
    // !! Do not use to check if this entry has a parent or not !!
    // It is 0 by default, and may be invalid if an add/rem/move operations fails
    private int indexInParent;

    /// <summary>
    /// Gets the address table manager this entry currently exists in
    /// </summary>
    public AddressTableManager? AddressTableManager { get; private set; }

    /// <summary>
    /// Gets or sets the group entry that is a direct parent to this entry
    /// </summary>
    public AddressTableGroupEntry? Parent { get; private set; }

    public TransferableData TransferableData { get; }

    public string? Description {
        get => this.description;
        set => PropertyHelper.SetAndRaiseINE(ref this.description, value, this, static t => t.DescriptionChanged?.Invoke(t));
    }

    /// <summary>
    /// An event fired when our <see cref="Parent"/> property changes.
    /// If the new parent is attached to a address table manager, our address table manager will be updates
    /// after this event is fired (see <see cref="ATMChanged"/>)
    /// </summary>
    public event BaseAddressTableEntryParentChangedEventHandler? ParentChanged;

    /// <summary>
    /// An event fired when our <see cref="AddressTableManager"/> property changes due to address table manager attachment or detachment.
    /// </summary>
    public event BaseAddressTableEntryManagerChangedEventHandler? ATMChanged;

    public event BaseAddressTableEntryEventHandler? DescriptionChanged;

    protected BaseAddressTableEntry() {
        this.TransferableData = new TransferableData(this);
    }

    static BaseAddressTableEntry() {
    }

    public int GetIndexInParent() {
        return this.Parent != null ? this.indexInParent : -1;
    }

    /// <summary>
    /// Invoked when this child entry is added to or removed from a parent object as a child.
    /// This method is called before our child entries have their <see cref="OnHierarchicalParentChanged"/> method invoked.
    /// <para>
    /// Even if the old/new parent object is a address table manager, this is invoked before <see cref="OnAttachedToATM"/> or <see cref="OnDetachedFromATM"/>
    /// </para>
    /// </summary>
    /// <param name="oldParent">The previous parent (non-null when removing or moving)</param>
    /// <param name="newParent">The new parent (non-null when adding or moving)</param>
    protected virtual void OnParentChanged(AddressTableGroupEntry? oldParent, AddressTableGroupEntry? newParent) {
    }

    /// <summary>
    /// Invoked when one of our hierarchical parents is added to or removed from a parent object as a child to a entry as a child. 
    /// This method is just for clarity and most likely isn't needed
    /// <para>    /// <param name="newParent">The origin's new parent (non-null when adding or moving)</param>
    /// Even if the old/new parent object is a address table manager, this is invoked before <see cref="OnAttachedToATM"/> or <see cref="OnDetachedFromATM"/>
    /// </para>
    /// <para>
    /// The origin's new parent will equal its <see cref="Parent"/>
    /// </para>
    /// </summary>
    /// <param name="origin">The parent that was actually added. It may equal this entry's parent entry</param>
    /// <param name="oldParent">The origin's previous parent (non-null when removing or moving)</param>
    protected virtual void OnHierarchicalParentChanged(BaseAddressTableEntry origin, AddressTableGroupEntry? originOldParent) {
    }

    /// <summary>
    /// Invoked when this entry is added to a address table manager. This can be fired by either this entry being added to
    /// a group entry which exists in a address table manager, or when we are added as a top-level entry in a address table manager
    /// <para>
    /// This is invoked BEFORE <see cref="ATMChanged"/>
    /// </para>
    /// </summary>
    /// <param name="origin">
    /// The entry that directly caused the address table manager to become attached (either by being added as a top-level entry
    /// or being added into a group entry that exists in a address table manager). May equal the current instance
    /// </param>
    protected virtual void OnAttachedToATM(BaseAddressTableEntry origin) {
    }

    /// <summary>
    /// Invoked when this entry is removed from a address table manager. This can be fired by either this entry being removed from
    /// a group entry which exists in a address table manager, or when we are removed as a top-level entry from a address table manager.
    /// <para>
    /// This is invoked BEFORE <see cref="ATMChanged"/>
    /// </para>
    /// </summary>
    /// <param name="origin">
    /// The entry that directly caused the address table manager to become detached (either by being removed as a top-level entry
    /// or being removed from a container entry that exists in a address table manager). May equal the current instance
    /// </param>
    /// <param name="oldAddressTableManager">The address table manager that we previously existed in</param>
    protected virtual void OnDetachedFromATM(BaseAddressTableEntry origin, AddressTableManager oldAddressTableManager) {
    }

    public static bool CheckHaveParentsAndAllMatch(ISelectionManager<BaseAddressTableEntry> manager, [NotNullWhen(true)] out AddressTableGroupEntry? sameParent) {
        return CheckHaveParentsAndAllMatch(manager.SelectedItems, out sameParent);
    }
    
    public static bool CheckHaveParentsAndAllMatch(IEnumerable<BaseAddressTableEntry> items, [NotNullWhen(true)] out AddressTableGroupEntry? sameParent) {
        using IEnumerator<BaseAddressTableEntry> enumerator = items.GetEnumerator();
        if (!enumerator.MoveNext())
            throw new InvalidOperationException("Expected items to contain at least 1 item");

        if ((sameParent = enumerator.Current.Parent) == null)
            return false;

        while (enumerator.MoveNext()) {
            if (!ReferenceEquals(enumerator.Current.Parent, sameParent)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A helper method for removing a list of items from their parent containers
    /// </summary>
    /// <param name="list"></param>
    public static void RemoveListFromTree(List<BaseAddressTableEntry> list) {
        foreach (BaseAddressTableEntry entry in list) {
            entry.Parent?.RemoveEntry(entry);
        }
    }

    internal static void InternalOnPreRemoveFromOwner(BaseAddressTableEntry entry) {
    }

    internal static void InternalSetATM(BaseAddressTableEntry entry, AddressTableManager addressTableManager) {
        entry.AddressTableManager = addressTableManager;
    }

    // User added some entry into group entry
    internal static void InternalOnAddedToEntry(int index, BaseAddressTableEntry entry, AddressTableGroupEntry newParent) {
        Debug.Assert(entry.Parent == null, "Did not expect entry to be in a group entry when adding it to another");
        Debug.Assert(entry.AddressTableManager == null, "Did not expect entry to be in a address table manager when adding to a group entry");

        entry.Parent = newParent;
        entry.indexInParent = index;
        entry.OnParentChanged(null, newParent);
        entry.ParentChanged?.Invoke(entry, null, newParent);

        if (newParent.AddressTableManager != null) {
            entry.AddressTableManager = newParent.AddressTableManager;
            entry.OnAttachedToATM(entry);
            entry.ATMChanged?.Invoke(entry, null, entry.AddressTableManager);
        }

        if (entry is AddressTableGroupEntry asGroup) {
            foreach (BaseAddressTableEntry child in asGroup.Items) {
                RecurseChildren(child, entry);
            }
        }

        return;

        static void RecurseChildren(BaseAddressTableEntry child, BaseAddressTableEntry origin) {
            child.OnHierarchicalParentChanged(origin, null);
            if (origin.AddressTableManager != null) {
                child.AddressTableManager = origin.AddressTableManager;
                child.OnAttachedToATM(origin);
                child.ATMChanged?.Invoke(child, null, child.AddressTableManager);
            }

            if (child is AddressTableGroupEntry childAsComposition) {
                foreach (BaseAddressTableEntry nextChild in childAsComposition.Items) {
                    RecurseChildren(nextChild, origin);
                }
            }
        }
    }

    internal static void InternalOnRemovedFromEntry(BaseAddressTableEntry entry, AddressTableGroupEntry oldParent) {
        Debug.Assert(entry.Parent != null, "Did not expect entry to not be in a group entry when removing it from another");

        // While child entries are notified of address table manager detachment first, should we do the same here???
        AddressTableManager? oldATM = entry.AddressTableManager;
        if (oldATM != null) {
            entry.AddressTableManager = null;
            entry.OnDetachedFromATM(entry, oldATM);
            entry.ATMChanged?.Invoke(entry, oldATM, null);
        }

        entry.Parent = null;
        entry.OnParentChanged(oldParent, null);
        entry.ParentChanged?.Invoke(entry, oldParent, null);

        if (entry is AddressTableGroupEntry asComposition) {
            foreach (BaseAddressTableEntry child in asComposition.Items) {
                RecurseChildren(child, entry, oldParent, oldATM);
            }
        }

        return;

        static void RecurseChildren(BaseAddressTableEntry child, BaseAddressTableEntry origin, AddressTableGroupEntry originOldParent, AddressTableManager? oldATM) {
            // Detach from address table manager first, then notify hierarchical parent removed from entry
            if (child.AddressTableManager != null) {
                Debug.Assert(oldATM == child.AddressTableManager, "Expected oldATM and our address table manager to match");

                child.AddressTableManager = null;
                child.OnDetachedFromATM(origin, oldATM);
                child.ATMChanged?.Invoke(child, oldATM, null);
            }

            child.OnHierarchicalParentChanged(origin, originOldParent);
            if (child is AddressTableGroupEntry entry) {
                foreach (BaseAddressTableEntry nextChild in entry.Items) {
                    RecurseChildren(nextChild, origin, originOldParent, oldATM);
                }
            }
        }
    }

    protected internal static void InternalSetIndexInParent(BaseAddressTableEntry entry, int index) {
        entry.indexInParent = index;
    }

    protected internal static int InternalIndexInParent(BaseAddressTableEntry entry) {
        return entry.indexInParent;
    }
}