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
        this.Fields.ValidateAdd += (list, index, items) => {
            foreach (FieldElement element in items) {
                if (element.Owner != null) {
                    throw new InvalidOperationException("Cannot add a field element that already exists in another class");
                }
            }
        };

        this.Fields.ValidateReplace += (list, index, oldItem, newItem) => {
            if (newItem.Owner != null) {
                throw new InvalidOperationException("Cannot add a field element that already exists in another class");
            }
        };

        this.Fields.ItemsAdded += (list, index, items) => {
            foreach (FieldElement element in items) {
                Debug.Assert(element.Owner == null);
                element.Owner = this;
            }
        };

        this.Fields.ItemReplaced += (list, index, oldItem, newItem) => {
            Debug.Assert(oldItem.Owner == this);
            Debug.Assert(newItem.Owner == null);
            oldItem.Owner = null;
            newItem.Owner = this;
        };

        this.Fields.ItemsRemoved += (list, index, items) => {
            foreach (FieldElement element in items) {
                Debug.Assert(element.Owner == this);
                element.Owner = null;
            }
        };
    }
}