using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Engine.StructViewing.ClassBuilding;

/// <summary>
/// A field entry in a <see cref="ClassType"/>
/// </summary>
public sealed class FieldElement {
    private uint offset;
    private TypeDescriptor fieldType;
    private string fieldName;

    /// <summary>
    /// Gets or sets the class that owns this field
    /// </summary>
    public ClassType? Owner {
        get => field;
        internal set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.OwnerChanged);
    }

    /// <summary>
    /// Gets or sets the offset of this field from its owner class
    /// </summary>
    public uint Offset {
        get => this.offset;
        internal set {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            PropertyHelper.SetAndRaiseINE(ref this.offset, value, this, this.OffsetChanged);
        }
    }

    /// <summary>
    /// Gets or sets the type of this field
    /// </summary>
    public TypeDescriptor FieldType {
        get => this.fieldType;
        set {
            ArgumentNullException.ThrowIfNull(value);
            PropertyHelper.SetAndRaiseINE(ref this.fieldType, value, this, this.FieldTypeChanged);
        }
    }

    /// <summary>
    /// Gets or sets the name of this field
    /// </summary>
    public string FieldName {
        get => this.fieldName;
        set => PropertyHelper.SetAndRaiseINE(ref this.fieldName, value, this, this.FieldNameChanged);
    }
    
    public event EventHandler? OwnerChanged;
    public event EventHandler? OffsetChanged;
    public event EventHandler? FieldTypeChanged;
    public event EventHandler? FieldNameChanged;
    
    public FieldElement(uint offset, string fieldName, TypeDescriptor fieldType) {
        this.offset = offset;
        this.fieldName = fieldName;
        this.fieldType = fieldType;
    }
}