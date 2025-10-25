using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.StructViewing.ClassBuilding;

public delegate void FieldElementEventHandler(FieldElement sender);

public delegate void FieldElementOwnerChangedEventHandler(FieldElement sender, ClassType? oldOwner, ClassType? newOwner);

public delegate void FieldElementFieldTypeChangedEventHandler(FieldElement sender, TypeDescriptor oldFieldType, TypeDescriptor newFieldType);

/// <summary>
/// A field entry in a <see cref="ClassType"/>
/// </summary>
public sealed class FieldElement {
    private ClassType? owner;
    private uint offset;
    private TypeDescriptor fieldType;
    private string fieldName;

    /// <summary>
    /// Gets or sets the class that owns this field
    /// </summary>
    public ClassType? Owner {
        get => this.owner;
        internal set => PropertyHelper.SetAndRaiseINE(ref this.owner, value, this, static (t, o, n) => t.OwnerChanged?.Invoke(t, o, n));
    }

    /// <summary>
    /// Gets or sets the offset of this field from its owner class
    /// </summary>
    public uint Offset {
        get => this.offset;
        internal set {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            PropertyHelper.SetAndRaiseINE(ref this.offset, value, this, static t => t.OffsetChanged?.Invoke(t));
        }
    }

    /// <summary>
    /// Gets or sets the type of this field
    /// </summary>
    public TypeDescriptor FieldType {
        get => this.fieldType;
        set {
            ArgumentNullException.ThrowIfNull(value);
            PropertyHelper.SetAndRaiseINE(ref this.fieldType, value, this, static (t, o, n) => t.FieldTypeChanged?.Invoke(t, o, n));
        }
    }

    /// <summary>
    /// Gets or sets the name of this field
    /// </summary>
    public string FieldName {
        get => this.fieldName;
        set => PropertyHelper.SetAndRaiseINE(ref this.fieldName, value, this, static t => t.FieldNameChanged?.Invoke(t));
    }
    
    public event FieldElementOwnerChangedEventHandler? OwnerChanged;
    public event FieldElementEventHandler? OffsetChanged;
    public event FieldElementFieldTypeChangedEventHandler? FieldTypeChanged;
    public event FieldElementEventHandler? FieldNameChanged;
    
    public FieldElement(uint offset, string fieldName, TypeDescriptor fieldType) {
        this.offset = offset;
        this.fieldName = fieldName;
        this.fieldType = fieldType;
    }
}