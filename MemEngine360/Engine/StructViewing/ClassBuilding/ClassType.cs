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
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Engine.StructViewing.ClassBuilding;

/// <summary>
/// Represents a class
/// </summary>
public sealed class ClassType {
    private string name;

    /// <summary>
    /// Gets or sets this class' parent class
    /// </summary>
    public ClassType? Parent {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.ParentChanged);
    }

    /// <summary>
    /// Gets or sets the class' name
    /// </summary>
    public string Name {
        get => this.name;
        set => PropertyHelper.SetAndRaiseINE(ref this.name, value, this, this.NameChanged);
    }
    
    /// <summary>
    /// Gets this class' fields
    /// </summary>
    public ObservableList<FieldElement> Fields { get; }

    public event EventHandler? ParentChanged;
    public event EventHandler? NameChanged;

    public ClassType(string name) {
        this.name = name;
        this.Fields = new ObservableList<FieldElement>();
        this.Fields.ValidateAdd += (list, e) => {
            foreach (FieldElement element in e.Items) {
                if (element.Owner != null) {
                    throw new InvalidOperationException("Cannot add a field element that already exists in another class");
                }
            }
        };

        this.Fields.ValidateReplace += (list, e) => {
            if (e.NewItem.Owner != null) {
                throw new InvalidOperationException("Cannot add a field element that already exists in another class");
            }
        };

        this.Fields.ItemsAdded += (list, e) => {
            foreach (FieldElement element in e.Items) {
                Debug.Assert(element.Owner == null);
                element.Owner = this;
            }
        };

        this.Fields.ItemReplaced += (list, e) => {
            Debug.Assert(e.OldItem.Owner == this);
            Debug.Assert(e.NewItem.Owner == null);
            e.OldItem.Owner = null;
            e.NewItem.Owner = this;
        };

        this.Fields.ItemsRemoved += (list, e) => {
            foreach (FieldElement element in e.Items) {
                Debug.Assert(element.Owner == this);
                element.Owner = null;
            }
        };
    }
}