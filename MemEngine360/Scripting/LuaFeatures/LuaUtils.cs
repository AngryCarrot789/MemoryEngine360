using System.Globalization;
using Lua;
using MemEngine360.Engine.Modes;

namespace MemEngine360.Scripting.LuaFeatures;

public static class LuaUtils {
    public static uint GetHexNumber(LuaFunctionExecutionContext ctx, int index, string functionName) {
        LuaValue arg = ctx.GetArgument(index);
        if (arg.Type == LuaValueType.String) {
            string text = arg.Read<string>() ?? "";
            ReadOnlySpan<char> span = text.StartsWith("0x") ? text.AsSpan(2) : text.AsSpan();
            return uint.Parse(span, NumberStyles.HexNumber);
        }

        if (arg.TryRead(out int value)) {
            return (uint) value;
        }

        throw BadArgument(in ctx, index, functionName, "Invalid hex value: " + arg);
    }

    public static uint GetUIntFromValue(in LuaFunctionExecutionContext context, int index, string functionName) {
        LuaValue addressArgument = context.GetArgument(index);
        
        uint address;
        if (addressArgument.Type == LuaValueType.Number) {
            addressArgument.TryRead(out double addr);
            if (addr < 0)
                throw BadArgument(in context, index, functionName, "UInt argument cannot be a negative number");
            address = (uint) addr;
        }
        else if (addressArgument.TryRead(out string str)) {
            if (str.StartsWith("0x")) {
                if (!uint.TryParse(str.AsSpan(2), NumberStyles.HexNumber, null, out address))
                    throw BadArgument(in context, index, functionName, "Invalid uint argument: " + str);
            }
            else if (!uint.TryParse(str, out address))
                throw BadArgument(in context, index, functionName, "Invalid uint argument: " + str);
        }
        else {
            throw BadArgument(in context, index, functionName, "Invalid uint argument: " + addressArgument);
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
            default: throw Exception(in context, "Unknown data type: " + type);
        }
    }

    public static LuaRuntimeException Exception(in LuaFunctionExecutionContext ctx, string message) {
        return new LuaRuntimeException(ctx.State.GetTraceback(), message);
    }

    public static void AssignFunction(LuaTable table, LuaFunction function) {
        table[function.Name] = function;
    }

    public static LuaRuntimeException AttemptInvalidOperation(in LuaFunctionExecutionContext ctx, string op, LuaValue a, LuaValue b) {
        return new LuaRuntimeException(ctx.State.GetTraceback(), $"attempt to {op} a '{a.Type}' with a '{b.Type}'");
    }

    public static LuaRuntimeException AttemptInvalidOperation(in LuaFunctionExecutionContext ctx, string op, LuaValue a) {
        return new LuaRuntimeException(ctx.State.GetTraceback(), $"attempt to {op} a '{a.Type}' value");
    }

    public static LuaRuntimeException BadArgument(in LuaFunctionExecutionContext ctx, int argumentId, string functionName) {
        return new LuaRuntimeException(ctx.State.GetTraceback(), $"bad argument #{argumentId} to '{functionName}' (value expected)");
    }

    public static LuaRuntimeException BadArgument(in LuaFunctionExecutionContext ctx, int argumentId, string functionName, LuaValueType[] expected) {
        return new LuaRuntimeException(ctx.State.GetTraceback(), $"bad argument #{argumentId} to '{functionName}' ({string.Join(" or ", expected)} expected)");
    }

    public static LuaRuntimeException BadArgument(in LuaFunctionExecutionContext ctx, int argumentId, string functionName, string expected, string actual) {
        return new LuaRuntimeException(ctx.State.GetTraceback(), $"bad argument #{argumentId} to '{functionName}' ({expected} expected, got {actual})");
    }

    public static LuaRuntimeException BadArgument(in LuaFunctionExecutionContext ctx, int argumentId, string functionName, string message) {
        return new LuaRuntimeException(ctx.State.GetTraceback(), $"bad argument #{argumentId} to '{functionName}' ({message})");
    }

    public static LuaRuntimeException BadArgumentNumberIsNotInteger(in LuaFunctionExecutionContext ctx, int argumentId, string functionName) {
        return new LuaRuntimeException(ctx.State.GetTraceback(), $"bad argument #{argumentId} to '{functionName}' (number has no integer representation)");
    }
    
    public static LuaRuntimeException NotEnoughArgs(in LuaFunctionExecutionContext ctx, string functionName, int expectedCount) {
        return new LuaRuntimeException(ctx.State.GetTraceback(), $"not enough args to '{functionName}' ({expectedCount} expected, got {ctx.ArgumentCount})");
    }
    
    public static LuaRuntimeException NotEnoughArgs(in LuaFunctionExecutionContext ctx, string functionName, string message) {
        return new LuaRuntimeException(ctx.State.GetTraceback(), $"not enough args to '{functionName}' ({message})");
    }
}