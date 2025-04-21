namespace MemEngine360.Engine.Modes;

/// <summary>
/// A searchable data type
/// </summary>
public enum DataType {
    /// <summary>
    /// 8 bits
    /// </summary>
    Byte,

    /// <summary>
    /// 16 bits
    /// </summary>
    Int16,

    /// <summary>
    /// 32 bits
    /// </summary>
    Int32,

    /// <summary>
    /// 64 bits
    /// </summary>
    Int64,

    /// <summary>
    /// 32 bit floating point number
    /// </summary>
    Float,

    /// <summary>
    /// 64 bit floating point number
    /// </summary>
    Double,

    /// <summary>
    /// A searchable string
    /// </summary>
    String
}

public static class DataTypeExtensions {
    public static bool IsNumeric(this DataType dataType) {
        switch (dataType) {
            case DataType.Byte:
            case DataType.Int16:
            case DataType.Int32:
            case DataType.Int64:
            case DataType.Float:
            case DataType.Double:
                return true;
            default: return false;
        }
    }

    public static bool IsInteger(this DataType dataType) {
        switch (dataType) {
            case DataType.Byte:
            case DataType.Int16:
            case DataType.Int32:
            case DataType.Int64:
                return true;
            default: return false;
        }
    }
    
    public static bool IsFloat(this DataType dataType) {
        switch (dataType) {
            case DataType.Float:
            case DataType.Double:
                return true;
            default: return false;
        }
    }
}