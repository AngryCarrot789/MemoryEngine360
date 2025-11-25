using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Engine.StructViewing.ClassBuilding;

/// <summary>
/// Describes a type
/// </summary>
public abstract class TypeDescriptor {
    /// <summary>
    /// Gets the readable name of this type descriptor. Note this does not update on renames
    /// </summary>
    public abstract string ReadableName { get; }

    /// <summary>
    /// An event fired when the <see cref="ReadableName"/> property changed. Note, you must remove handlers
    /// once no longer required, because adding a handler may require an event proxy to other objects
    /// </summary>
    public abstract event EventHandler? ReadableNameChanged;

    protected TypeDescriptor() {
    }

    public static PrimitiveTypeDescriptor Primitive(PrimitiveTypeDescriptor.Type type) => new PrimitiveTypeDescriptor(type);

    public static ClassTypeDescriptor OfType(ClassType type) => new ClassTypeDescriptor(type);

    public static PointerTypeDescriptor PointerTo(ClassType type) => new PointerTypeDescriptor(new ClassTypeDescriptor(type));

    public static PointerTypeDescriptor PointerTo(TypeDescriptor type) => new PointerTypeDescriptor(type);

    public static ArrayTypeDescriptor ArrayOf(ClassType type, int elementCount) => new ArrayTypeDescriptor(new ClassTypeDescriptor(type), elementCount);

    public static ArrayTypeDescriptor ArrayOf(TypeDescriptor type, int elementCount) => new ArrayTypeDescriptor(type, elementCount);
}

public sealed class PrimitiveTypeDescriptor : TypeDescriptor {
    private Type primitiveType;

    public override string ReadableName => this.primitiveType.ToString();

    public override event EventHandler? ReadableNameChanged;

    public Type PrimitiveType {
        get => this.primitiveType;
        set => PropertyHelper.SetAndRaiseINE(ref this.primitiveType, value, this, static t => {
            t.PrimitiveTypeChanged?.Invoke(t, EventArgs.Empty);
            t.ReadableNameChanged?.Invoke(t, EventArgs.Empty);
        });
    }

    public event EventHandler? PrimitiveTypeChanged;

    public PrimitiveTypeDescriptor() {
    }

    public PrimitiveTypeDescriptor(Type primitiveType) {
        this.primitiveType = primitiveType;
    }

    public enum Type {
        Byte,
        Int16,
        Int32,
        Int64,
        Float,
        Double
    }
}

public sealed class ClassTypeDescriptor : TypeDescriptor {
    private EventHandler? myRnc;
    private readonly EventHandler cachedNameChangedHandler;

    public ClassType ClassType { get; }

    public override string ReadableName => this.ClassType.Name;

    public override event EventHandler? ReadableNameChanged {
        add => MulticastUtils.AddWithProxy(ref this.myRnc, value, this, t => t.ClassType.NameChanged += t.cachedNameChangedHandler);
        remove => MulticastUtils.RemoveWithProxy(ref this.myRnc, value, this, t => t.ClassType.NameChanged -= t.cachedNameChangedHandler);
    }

    public ClassTypeDescriptor(ClassType classType) {
        this.ClassType = classType;
        this.cachedNameChangedHandler = this.OnClassTypeNameChanged;
    }

    private void OnClassTypeNameChanged(object? o, EventArgs e) => this.myRnc?.Invoke(this, EventArgs.Empty);
}

public sealed class PointerTypeDescriptor : TypeDescriptor {
    private EventHandler? myRnc;
    private readonly EventHandler cachedReadableNameChangedHandler;

    public TypeDescriptor PointerType { get; }

    public override string ReadableName => this.PointerType.ReadableName + '*';

    public override event EventHandler? ReadableNameChanged {
        add => MulticastUtils.AddWithProxy(ref this.myRnc, value, this, static t => t.PointerType.ReadableNameChanged += t.cachedReadableNameChangedHandler);
        remove => MulticastUtils.RemoveWithProxy(ref this.myRnc, value, this, static t => t.PointerType.ReadableNameChanged -= t.cachedReadableNameChangedHandler);
    }

    public PointerTypeDescriptor(TypeDescriptor pointerType) {
        this.PointerType = pointerType;
        this.cachedReadableNameChangedHandler = this.OnPointerTypeReadableNameChanged;
    }

    private void OnPointerTypeReadableNameChanged(object? sender, EventArgs e) => this.myRnc?.Invoke(this, EventArgs.Empty);
}

public sealed class ArrayTypeDescriptor : TypeDescriptor {
    private int elementCount;
    private EventHandler? myRnc;
    private readonly EventHandler cachedReadableNameChangedHandler;

    public TypeDescriptor ElementType { get; }

    public int ElementCount {
        get => this.elementCount;
        set => PropertyHelper.SetAndRaiseINE(ref this.elementCount, value, this, static t => {
            t.ElementCountChanged?.Invoke(t, EventArgs.Empty);
            t.myRnc?.Invoke(t, EventArgs.Empty);
        });
    }

    public override string ReadableName => $"{this.ElementType.ReadableName}[{this.ElementCount}]";

    public override event EventHandler? ReadableNameChanged {
        add => MulticastUtils.AddWithProxy(ref this.myRnc, value, this, t => t.ElementType.ReadableNameChanged += t.cachedReadableNameChangedHandler);
        remove => MulticastUtils.RemoveWithProxy(ref this.myRnc, value, this, t => t.ElementType.ReadableNameChanged -= t.cachedReadableNameChangedHandler);
    }

    public event EventHandler? ElementCountChanged;

    public ArrayTypeDescriptor(TypeDescriptor elementType, int elementCount = 0) {
        this.ElementType = elementType;
        this.elementCount = elementCount;
        this.cachedReadableNameChangedHandler = this.OnElementTypeReadableNameChanged;
    }

    private void OnElementTypeReadableNameChanged(object? sender, EventArgs e) => this.myRnc?.Invoke(this, EventArgs.Empty);
}