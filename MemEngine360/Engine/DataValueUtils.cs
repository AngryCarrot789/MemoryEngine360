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
    /// <returns>True when parsed successfully, false when the input couldn't be parsed</returns>
    public static bool TryParseTextAsDataValue(ValidationArgs args, DataType dataType, NumericDisplayType ndt, StringType stringType, [NotNullWhen(true)] out IDataValue? value) {
        return (value = TryParseTextAsDataValue(args, dataType, ndt, stringType)) != null;
    }

    /// <summary>
    /// Same as <see cref="TryParseTextAsDataValue"/> but throws when the input couldn't be parsed
    /// </summary>
    public static IDataValue ParseTextAsDataValue(string input, DataType dataType, NumericDisplayType ndt, StringType stringType) {
        ValidationArgs args = new ValidationArgs(input, [], false);
        IDataValue? value = TryParseTextAsDataValue(args, dataType, ndt, stringType);
        if (value != null) {
            return value;
        }
        
        throw new Exception("Invalid input. " + (args.Errors.Count > 0 ? args.Errors[0] : ""));
    }

    private static IDataValue? TryParseTextAsDataValue(ValidationArgs args, DataType dataType, NumericDisplayType ndt, StringType stringType) {
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
                if (!MemoryPattern.TryCompile(args.Input, out var pattern, false, out string? errorMessage)) {
                    args.Errors.Add(errorMessage);
                    break;
                }

                return new DataValueByteArray(pattern.pattern.Select(x => x ?? 0).ToArray());
            }
            default: throw new ArgumentOutOfRangeException();
        }

        return null;
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
            case DataType.Byte:      return ndt.AsString(value.DataType, ((DataValueByte) value).Value);
            case DataType.Int16:     return ndt.AsString(value.DataType, ((DataValueInt16) value).Value);
            case DataType.Int32:     return ndt.AsString(value.DataType, ((DataValueInt32) value).Value);
            case DataType.Int64:     return ndt.AsString(value.DataType, ((DataValueInt64) value).Value);
            case DataType.Float:     return ndt.AsString(value.DataType, ((DataValueFloat) value).Value);
            case DataType.Double:    return ndt.AsString(value.DataType, ((DataValueDouble) value).Value);
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