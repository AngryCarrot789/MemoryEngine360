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

    public event BaseAddressTableEntryEventHandler? DescriptionChanged;

    protected BaseAddressTableEntry() {
        this.TransferableData = new TransferableData(this);
    }

    static BaseAddressTableEntry() {
    }

    public int GetIndexInParent() {
        return this.Parent?.IndexOf(this) ?? -1;
    }

    /// <summary>
    /// Creates a deep clone of this object, as if the user created it from scratch
    /// </summary>
    /// <returns>The new clone</returns>
    public abstract BaseAddressTableEntry CreateClone();

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

    internal static void InternalSetATM(BaseAddressTableEntry entry, AddressTableManager addressTableManager) {
        entry.AddressTableManager = addressTableManager;
    }

    // User added some entry into group entry
    internal static void InternalOnAddedToEntry(BaseAddressTableEntry entry, AddressTableGroupEntry newParent) {
        Debug.Assert(entry.Parent == null, "Did not expect entry to be in a group entry when adding it to another");
        Debug.Assert(entry.AddressTableManager == null, "Did not expect entry to be in a address table manager when adding to a group entry");

        entry.Parent = newParent;
        entry.AddressTableManager = newParent.AddressTableManager;
        if (entry is AddressTableGroupEntry asGroup) {
            foreach (BaseAddressTableEntry child in asGroup.Items) {
                RecurseChildren(child, entry);
            }
        }

        return;

        static void RecurseChildren(BaseAddressTableEntry child, BaseAddressTableEntry origin) {
            child.AddressTableManager = origin.AddressTableManager;
            if (child is AddressTableGroupEntry childAsComposition) {
                foreach (BaseAddressTableEntry nextChild in childAsComposition.Items) {
                    RecurseChildren(nextChild, origin);
                }
            }
        }
    }

    internal static void InternalOnRemovedFromEntry(BaseAddressTableEntry entry) {
        Debug.Assert(entry.Parent != null, "Did not expect entry to not be in a group entry when removing it from another");

        // While child entries are notified of address table manager detachment first, should we do the same here???
        entry.AddressTableManager = null;
        entry.Parent = null;
        if (entry is AddressTableGroupEntry asComposition) {
            foreach (BaseAddressTableEntry child in asComposition.Items) {
                RecurseChildren(child);
            }
        }

        return;

        static void RecurseChildren(BaseAddressTableEntry child) {
            child.AddressTableManager = null;
            if (child is AddressTableGroupEntry entry) {
                foreach (BaseAddressTableEntry nextChild in entry.Items) {
                    RecurseChildren(nextChild);
                }
            }
        }
    }
}