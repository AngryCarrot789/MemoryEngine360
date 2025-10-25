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
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using ILMath;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine;

/// <summary>
/// A helper class for serializing and deserializing data values, reading/writing them through connections, etc.
/// </summary>
public static class DataValueUtils {
    /// <summary>
    /// Attempts to parse a string input as a <see cref="IDataValue"/> using the given information (the type of data expected, numeric display type and string type)
    /// </summary>
    /// <param name="args">Validation args containing the input and a list which, when an error is encountered, the error is added to the list</param>
    /// <param name="dataType">The type of data we want to parse the text from</param>
    /// <param name="ndt">
    /// The way integers and floats are parsed.
    /// <br/>
    /// When this is <see cref="NumericDisplayType.Hexadecimal"/>, we attempt to parse integer as hex and floats as their raw float bits.
    /// <br/>
    /// When this is <see cref="NumericDisplayType.Unsigned"/>, we attempt to parse integers as unsigned so no negative signs (except byte which is always unsigned)
    /// <br/>
    /// When this is <see cref="NumericDisplayType.Normal"/>, all integers except byte are parsed as signed, and floats are parsed as regular floats
    /// </param>
    /// <param name="stringType">The string type, e.g. ASCII and unicode</param>
    /// <param name="value">The parsed value</param>
    /// <param name="canParseAsExpression">
    /// Whether to use expression parsing to parse the value. When true, and the data type is integer-based,
    /// it will be parsed as hex by default only if <see cref="ndt"/> is <see cref="NumericDisplayType.Hexadecimal"/>
    /// </param>
    /// <returns>True when parsed successfully, false when the input couldn't be parsed</returns>
    public static bool TryParseTextAsDataValue(ValidationArgs args, DataType dataType, NumericDisplayType ndt, StringType stringType, [NotNullWhen(true)] out IDataValue? value, bool canParseAsExpression = false) {
        return (value = TryParseTextAsDataValue(args, dataType, ndt, stringType, canParseAsExpression)) != null;
    }

    /// <summary>
    /// Same as <see cref="TryParseTextAsDataValue"/> but throws when the input couldn't be parsed
    /// </summary>
    public static IDataValue ParseTextAsDataValue(string input, DataType dataType, NumericDisplayType ndt, StringType stringType, bool canParseAsExpression = false) {
        ValidationArgs args = new ValidationArgs(input, [], false);
        IDataValue? value = TryParseTextAsDataValue(args, dataType, ndt, stringType, canParseAsExpression);
        if (value != null) {
            return value;
        }

        throw new Exception("Invalid input" + (args.Errors.Count > 0 ? (": " + args.Errors[0]) : ""));
    }

    private static IDataValue? TryParseTextAsDataValue(ValidationArgs args, DataType dataType, NumericDisplayType ndt, StringType stringType, bool canParseAsExpression = false) {
        if (canParseAsExpression && dataType.IsNumeric()) {
            return TryParseIntegerExpressionAsDataValue(args, dataType, ndt);
        }

        NumberStyles nsInt = ndt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        switch (dataType) {
            case DataType.Byte: {
                if (byte.TryParse(args.Input, nsInt, null, out byte val))
                    return new DataValueByte(val);
                AddErrorForInteger<byte>(args, dataType, ndt);
                break;
            }
            case DataType.Int16:
            case DataType.Int32:
            case DataType.Int64: {
                if (ndt == NumericDisplayType.Unsigned) {
                    switch (dataType) {
                        case DataType.Int16: {
                            if (ushort.TryParse(args.Input, nsInt, null, out ushort val))
                                return new DataValueInt16((short) val);
                            AddErrorForInteger<ushort>(args, dataType, ndt);
                            break;
                        }
                        case DataType.Int32: {
                            if (uint.TryParse(args.Input, nsInt, null, out uint val))
                                return new DataValueInt32((int) val);
                            AddErrorForInteger<uint>(args, dataType, ndt);
                            break;
                        }
                        case DataType.Int64: {
                            if (ulong.TryParse(args.Input, nsInt, null, out ulong val))
                                return new DataValueInt64((long) val);
                            AddErrorForInteger<ulong>(args, dataType, ndt);
                            break;
                        }
                        default: throw new Exception("Memory corruption");
                    }
                }
                else {
                    switch (dataType) {
                        case DataType.Int16: {
                            if (short.TryParse(args.Input, nsInt, null, out short val))
                                return new DataValueInt16(val);
                            AddErrorForInteger<short>(args, dataType, ndt);
                            break;
                        }
                        case DataType.Int32: {
                            if (int.TryParse(args.Input, nsInt, null, out int val))
                                return new DataValueInt32(val);
                            AddErrorForInteger<int>(args, dataType, ndt);
                            break;
                        }
                        case DataType.Int64: {
                            if (long.TryParse(args.Input, nsInt, null, out long val))
                                return new DataValueInt64(val);
                            AddErrorForInteger<long>(args, dataType, ndt);
                            break;
                        }
                        default: throw new Exception("Memory corruption");
                    }
                }

                break;
            }
            case DataType.Float: {
                if (ndt == NumericDisplayType.Hexadecimal) {
                    if (uint.TryParse(args.Input, NumberStyles.HexNumber, null, out uint val)) {
                        return new DataValueFloat(Unsafe.As<uint, float>(ref val));
                    }

                    args.Errors.Add("Invalid unsigned integer (as the float bits)");
                }
                else if (float.TryParse(args.Input, out float val)) {
                    return new DataValueFloat(val);
                }
                else {
                    args.Errors.Add("Invalid float/single");
                }

                break;
            }
            case DataType.Double: {
                if (ndt == NumericDisplayType.Hexadecimal) {
                    if (ulong.TryParse(args.Input, NumberStyles.HexNumber, null, out ulong val)) {
                        return new DataValueDouble(Unsafe.As<ulong, double>(ref val));
                    }

                    args.Errors.Add("Invalid unsigned long (as the double bits)");
                }
                else if (double.TryParse(args.Input, out double val)) {
                    return new DataValueDouble(val);
                }
                else {
                    args.Errors.Add("Invalid double");
                }

                break;
            }
            case DataType.String: return new DataValueString(args.Input, stringType);
            case DataType.ByteArray: {
                if (!MemoryPattern.TryCompile(args.Input, out MemoryPattern pattern, false, out string? errorMessage)) {
                    args.Errors.Add(errorMessage);
                    break;
                }

                return new DataValueByteArray(pattern.pattern.Select(x => x ?? 0).ToArray());
            }
            default: throw new ArgumentOutOfRangeException();
        }

        return null;
    }
    
    private static readonly IEvaluationContext<uint> DefaultU32EvalCtx = EvaluationContexts.CreateForInteger<uint>();
    private static readonly IEvaluationContext<int> DefaultI32EvalCtx = EvaluationContexts.CreateForInteger<int>();
    private static readonly IEvaluationContext<ulong> DefaultU64EvalCtx = EvaluationContexts.CreateForInteger<ulong>();
    private static readonly IEvaluationContext<long> DefaultI64EvalCtx = EvaluationContexts.CreateForInteger<long>();
    private static readonly IEvaluationContext<float> DefaultFloatCtx = EvaluationContexts.CreateForFloat();
    private static readonly IEvaluationContext<double> DefaultDoubleCtx = EvaluationContexts.CreateForDouble();

    private static IDataValue? TryParseIntegerExpressionAsDataValue(ValidationArgs args, DataType dataType, NumericDisplayType ndt) {
        ParsingContext ctx = new ParsingContext() {
            DefaultIntegerParseMode = ndt == NumericDisplayType.Hexadecimal ? IntegerParseMode.Hexadecimal : IntegerParseMode.Integer
        };

        const CompilationMethod CompilationMethod = CompilationMethod.Functional;

        try {
            switch (dataType) {
                case DataType.Byte:
                case DataType.Int16:
                case DataType.Int32: {
                    if (ndt != NumericDisplayType.Normal) {
                        uint value = MathEvaluation.CompileExpression<uint>("", args.Input, ctx, CompilationMethod)(DefaultU32EvalCtx);
                        return dataType switch {
                            DataType.Byte => IDataValue.CreateNumeric((byte) value),
                            DataType.Int16 => IDataValue.CreateNumeric((short) value),
                            DataType.Int32 => IDataValue.CreateNumeric(value),
                            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
                        };
                    }
                    else {
                        int value = MathEvaluation.CompileExpression<int>("", args.Input, ctx, CompilationMethod)(DefaultI32EvalCtx);
                        return dataType switch {
                            DataType.Byte => IDataValue.CreateNumeric((byte) value),
                            DataType.Int16 => IDataValue.CreateNumeric((short) value),
                            DataType.Int32 => IDataValue.CreateNumeric(value),
                            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
                        };
                    }
                }
                case DataType.Int64: {
                    long value;
                    if (ndt != NumericDisplayType.Normal) {
                        value = (long) MathEvaluation.CompileExpression<ulong>("", args.Input, ctx, CompilationMethod)(DefaultU64EvalCtx);
                    }
                    else {
                        value = MathEvaluation.CompileExpression<long>("", args.Input, ctx, CompilationMethod)(DefaultI64EvalCtx);
                    }

                    return new DataValueInt64(value);
                }
                case DataType.Float: {
                    return new DataValueFloat(MathEvaluation.CompileExpression<float>("", args.Input, ctx, CompilationMethod)(DefaultFloatCtx));
                }
                case DataType.Double: {
                    return new DataValueDouble(MathEvaluation.CompileExpression<double>("", args.Input, ctx, CompilationMethod)(DefaultDoubleCtx));
                }
                default: throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
            }
        }
        catch (Exception e) {
            args.Errors.Add("Invalid value or expression: " + e.Message);
            return null;
        }
    }

    /// <summary>
    /// Converts a data value into a general string representation, typically used when editing a
    /// saved address entry to put the current value into the text box
    /// </summary>
    /// <param name="value">The data value</param>
    /// <param name="ndt">The method of formatting numbers. See <see cref="NumericDisplayTypeExtensions.AsString"/> for more info</param>
    /// <param name="arrayJoinChar">An optional character inserted between each byte of a <see cref="DataValueByteArray"/></param>
    /// <param name="putStringInQuotes">When true, encapsulates the value of <see cref="DataValueString"/> in quotes (convenience parameter)</param>
    /// <returns>The string representation of the data value</returns>
    /// <exception cref="ArgumentOutOfRangeException">Invalid data type</exception>
    public static string GetStringFromDataValue(IDataValue value, NumericDisplayType ndt, char? arrayJoinChar = ' ', bool putStringInQuotes = false) {
        switch (value.DataType) {
            case DataType.Byte:
            case DataType.Int16:
            case DataType.Int32:
            case DataType.Int64:
            case DataType.Float:
            case DataType.Double:
                return ndt.AsString(value.DataType, value.BoxedValue);
            case DataType.String:    return putStringInQuotes ? $"\"{value.BoxedValue}\"" : value.BoxedValue.ToString()!;
            case DataType.ByteArray: return NumberUtils.BytesToHexAscii(((DataValueByteArray) value).Value, arrayJoinChar);
            default:                 throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Helper method for getting string from data value from a <see cref="ScanResultViewModel"/>
    /// </summary>
    /// <param name="result">The result</param>
    /// <param name="value">The value</param>
    public static string GetStringFromDataValue(ScanResultViewModel result, IDataValue value, char? arrayJoinChar = ' ', bool putStringInQuotes = false) => GetStringFromDataValue(value, result.NumericDisplayType, arrayJoinChar, putStringInQuotes);

    /// <summary>
    /// Helper method for getting string from data value from an <see cref="AddressTableEntry"/>
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <param name="value">The value</param>
    public static string GetStringFromDataValue(AddressTableEntry entry, IDataValue value, char? arrayJoinChar = ' ', bool putStringInQuotes = false) => GetStringFromDataValue(value, entry.NumericDisplayType, arrayJoinChar, putStringInQuotes);

    private static void AddErrorForInteger<T>(ValidationArgs args, DataType dataType, NumericDisplayType ndt) where T : IBinaryInteger<T>, IMinMaxValue<T> {
        Debug.Assert(dataType.IsInteger(), "Expected data type to be numeric");
        NumberStyles nsInt = ndt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        if ((dataType == DataType.Byte || ndt != NumericDisplayType.Normal) && args.Input.TrimStart().StartsWith('-')) {
            args.Errors.Add($"{dataType} cannot be negative. Range is {T.MinValue}-{T.MaxValue}");
        }
        else if (typeof(T) != typeof(ulong) && ulong.TryParse(args.Input, nsInt, null, out _)) {
            args.Errors.Add($"Value is out of range for {dataType}. Range is {T.MinValue}-{T.MaxValue}");
        }
        else {
            args.Errors.Add("Text is not numeric");
        }
    }
}