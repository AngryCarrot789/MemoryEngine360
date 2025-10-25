using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.StructViewing.ClassBuilding;

public delegate void TypeDescriptorEventHandler(TypeDescriptor descriptor);

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
    public abstract event TypeDescriptorEventHandler? ReadableNameChanged;

    protected TypeDescriptor() {
    }

    public static PrimitiveTypeDescriptor Primitive(PrimitiveTypeDescriptor.Type type) => new PrimitiveTypeDescriptor(type);
    
    public static ClassTypeDescriptor OfType(ClassType type) => new ClassTypeDescriptor(type);
    
    public static PointerTypeDescriptor PointerTo(ClassType type) => new PointerTypeDescriptor(new ClassTypeDescriptor(type));
    
    public static PointerTypeDescriptor PointerTo(TypeDescriptor type) => new PointerTypeDescriptor(type);
    
    public static ArrayTypeDescriptor ArrayOf(ClassType type, int elementCount) => new ArrayTypeDescriptor(new ClassTypeDescriptor(type), elementCount);
    
    public static ArrayTypeDescriptor ArrayOf(TypeDescriptor type, int elementCount) => new ArrayTypeDescriptor(type, elementCount);
}

public delegate void PrimitiveTypeDescriptorPrimitiveTypeChangedEventHandler(PrimitiveTypeDescriptor sender);

public sealed class PrimitiveTypeDescriptor : TypeDescriptor {
    private Type primitiveType;

    public override string ReadableName => this.primitiveType.ToString();
    
    public override event TypeDescriptorEventHandler? ReadableNameChanged;

    public Type PrimitiveType {
        get => this.primitiveType;
        set => PropertyHelper.SetAndRaiseINE(ref this.primitiveType, value, this, static t => {
            t.PrimitiveTypeChanged?.Invoke(t);
            t.ReadableNameChanged?.Invoke(t);
        });
    }

    public event PrimitiveTypeDescriptorPrimitiveTypeChangedEventHandler? PrimitiveTypeChanged;
    
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
    private int rncHandlers;
    private TypeDescriptorEventHandler? myRnc;
    private readonly ClassTypeEventHandler cachedNameChangedHandler;

    public ClassType ClassType { get; }

    public override string ReadableName => this.ClassType.Name;

    public override event TypeDescriptorEventHandler? ReadableNameChanged {
        add => MulticastUtils.AddProxy(ref this.rncHandlers, ref this.myRnc, value, this, t => t.ClassType.NameChanged += t.cachedNameChangedHandler);
        remove => MulticastUtils.RemoveProxy(ref this.rncHandlers, ref this.myRnc, value, this, t => t.ClassType.NameChanged -= t.cachedNameChangedHandler);
    }

    public ClassTypeDescriptor(ClassType classType) {
        this.ClassType = classType;
        this.cachedNameChangedHandler = this.OnClassTypeNameChanged;
    }

    private void OnClassTypeNameChanged(ClassType sender) => this.myRnc?.Invoke(this);
}

public sealed class PointerTypeDescriptor : TypeDescriptor {
    private int rncHandlers;
    private TypeDescriptorEventHandler? myRnc;
    private readonly TypeDescriptorEventHandler cachedReadableNameChangedHandler;

    public TypeDescriptor PointerType { get; }

    public override string ReadableName => this.PointerType.ReadableName + '*';

    public override event TypeDescriptorEventHandler? ReadableNameChanged {
        add => MulticastUtils.AddProxy(ref this.rncHandlers, ref this.myRnc, value, this, t => t.PointerType.ReadableNameChanged += t.cachedReadableNameChangedHandler);
        remove => MulticastUtils.RemoveProxy(ref this.rncHandlers, ref this.myRnc, value, this, t => t.PointerType.ReadableNameChanged -= t.cachedReadableNameChangedHandler);
    }

    public PointerTypeDescriptor(TypeDescriptor pointerType) {
        this.PointerType = pointerType;
        this.cachedReadableNameChangedHandler = this.OnPointerTypeReadableNameChanged;
    }

    private void OnPointerTypeReadableNameChanged(TypeDescriptor descriptor) => this.myRnc?.Invoke(this);
}

public delegate void ArrayTypeDescriptorEventHandler(ArrayTypeDescriptor sender);

public sealed class ArrayTypeDescriptor : TypeDescriptor {
    private int elementCount;

    private int rncHandlers;
    private TypeDescriptorEventHandler? myRnc;
    private readonly TypeDescriptorEventHandler cachedReadableNameChangedHandler;

    public TypeDescriptor ElementType { get; }

    public int ElementCount {
        get => this.elementCount;
        set => PropertyHelper.SetAndRaiseINE(ref this.elementCount, value, this, static t => {
            t.ElementCountChanged?.Invoke(t);
            t.myRnc?.Invoke(t);
        });
    }

    public override string ReadableName => $"{this.ElementType.ReadableName}[{this.ElementCount}]";

    public override event TypeDescriptorEventHandler? ReadableNameChanged {
        add => MulticastUtils.AddProxy(ref this.rncHandlers, ref this.myRnc, value, this, t => t.ElementType.ReadableNameChanged += t.cachedReadableNameChangedHandler);
        remove => MulticastUtils.RemoveProxy(ref this.rncHandlers, ref this.myRnc, value, this, t => t.ElementType.ReadableNameChanged -= t.cachedReadableNameChangedHandler);
    }

    public event ArrayTypeDescriptorEventHandler? ElementCountChanged;

    public ArrayTypeDescriptor(TypeDescriptor elementType, int elementCount = 0) {
        this.ElementType = elementType;
        this.elementCount = elementCount;
        this.cachedReadableNameChangedHandler = this.OnElementTypeReadableNameChanged;
    }

    private void OnElementTypeReadableNameChanged(TypeDescriptor descriptor) => this.myRnc?.Invoke(this);
}