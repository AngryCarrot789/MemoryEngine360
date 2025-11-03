using System.Globalization;
using Lua;
using MemEngine360.Engine.Modes;

namespace MemEngine360.Scripting.LuaFeatures;

public static class LuaUtils {
    public static uint GetHexNumber(LuaFunctionExecutionContext ctx, int index) {
        LuaValue arg = ctx.GetArgument(index);
        if (arg.Type == LuaValueType.String) {
            string text = arg.Read<string>() ?? "";
            ReadOnlySpan<char> span = text.StartsWith("0x") ? text.AsSpan(2) : text.AsSpan();
            return uint.Parse(span, NumberStyles.HexNumber);
        }

        if (arg.TryRead(out int value)) {
            return (uint) value;
        }

        throw InvalidOperation(in ctx, "Invalid hex value: " + arg);
    }

    public static uint GetUIntFromValue(in LuaFunctionExecutionContext context, LuaValue addressArgument) {
        uint address;
        if (addressArgument.Type == LuaValueType.Number) {
            addressArgument.TryRead(out double addr);
            if (addr < 0)
                throw InvalidOperation(in context, "UInt argument cannot be a negative number");
            address = (uint) addr;
        }
        else if (addressArgument.TryRead(out string str)) {
            if (str.StartsWith("0x")) {
                if (!uint.TryParse(str.AsSpan(2), NumberStyles.HexNumber, null, out address))
                    throw InvalidOperation(in context, "Invalid uint argument: " + str);
            }
            else if (!uint.TryParse(str, out address))
                throw InvalidOperation(in context, "Invalid uint argument: " + str);
        }
        else {
            throw InvalidOperation(in context, "Invalid uint argument: " + addressArgument);
        }

        return address;
    }

    public static DataType GetDataTypeFromString(in LuaFunctionExecutionContext context, string type) {
        switch (type.ToUpperInvariant()) {
            case "BYTE":
            case "INT8":
                return DataType.Byte;
            case "SHORT":
            case "INT16":
                return DataType.Int16;
            case "INT":
            case "INT32":
                return DataType.Int32;
            case "LONG":
            case "INT64":
                return DataType.Int64;
            case "FLOAT":
            case "SINGLE":
            case "FLOAT32":
                return DataType.Float;
            case "DOUBLE":
            case "DOUBLE64":
            case "FLOAT64":
                return DataType.Double;
            default: throw InvalidOperation(in context, "Unknown data type: " + type);
        }
    }

    public static LuaRuntimeException InvalidOperation(in LuaFunctionExecutionContext ctx, string message) {
        return new LuaRuntimeException(ctx.State.GetTraceback(), message);
    }

    public static void AssignFunction(LuaTable table, LuaFunction function) {
        table[function.Name] = function;
    }
}