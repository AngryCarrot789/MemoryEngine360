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

using System.Globalization;
using MemEngine360.BaseFrontEnd.TaskSequencing.DataHandlers;
using MemEngine360.Engine;
using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.Modes;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.BaseFrontEnd.Utils;

public record struct DataValueState(IDataValue Value, bool ParseIntAsHex);

/// <summary>
/// A helper class for binders. This provides reusable parsing logic, typically for <see cref="TextBoxToEventPropertyBinder{TModel}"/>
/// </summary>
public static class BinderParsingUtils {
    public static async Task<bool> TryParseAddressEx<T>(string text, IBinder<T> binder, Action<IBinder<T>, IMemoryAddress> update) where T : class {
        if (MemoryAddressUtils.TryParse(text, out IMemoryAddress? address, out string? errorMessage)) {
            update(binder, address);
            return true;
        }

        await IMessageDialogService.Instance.ShowMessage("Invalid memory address", errorMessage!, defaultButton: MessageBoxResult.OK);
        return false;
    }

    public static async Task<bool> TryParseAddress<T>(string text, IBinder<T> binder, Action<IBinder<T>, uint> update, string caption = "Invalid value") where T : class {
        if (AddressParsing.TryParse32(text, out uint value, out string? error)) {
            update(binder, value);
            return true;
        }
        else {
            await IMessageDialogService.Instance.ShowMessage(caption, error, defaultButton: MessageBoxResult.OK);
            return false;
        }
    }

    public static async Task<IDataValue?> TryParseTextAsDataValueAndModify(string text, DataType initialDataType, DataProviderHandler h, bool canTryParseAsFloatOrDouble = true) {
        DataValueState? state = await TryParseTextAsDataValueAndModify(text, initialDataType, h.ParseIntAsHex, canTryParseAsFloatOrDouble);
        if (!state.HasValue) {
            return null;
        }

        h.ParseIntAsHex = state.Value.ParseIntAsHex;
        return state.Value.Value;
    }

    public static async Task<DataValueState?> TryParseTextAsDataValueAndModify(string input, DataType initialDataType, bool parseIntAsHex, bool canTryParseAsFloatOrDouble = true) {
        bool bHasHexPrefix = input.StartsWith("0x");
        if (!parseIntAsHex && bHasHexPrefix && initialDataType.IsInteger())
            parseIntAsHex = true;

        bool bIsNumeric = initialDataType.IsNumeric();
        bool bTryParseAsFloat = canTryParseAsFloatOrDouble && bIsNumeric && !bHasHexPrefix && input.EndsWith("f", StringComparison.OrdinalIgnoreCase);
        bool bInputEndsWithD = input.EndsWith("d", StringComparison.OrdinalIgnoreCase);
        bool bTryParseAsDouble = canTryParseAsFloatOrDouble && bIsNumeric && !bHasHexPrefix && (bInputEndsWithD || input.Contains('.'));
        ValidationArgs mainArgs = new ValidationArgs(parseIntAsHex && bHasHexPrefix ? input.Substring(2) : input, [], false);
        if (DataValueUtils.TryParseTextAsDataValue(mainArgs, initialDataType, parseIntAsHex ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal, StringType.ASCII, out IDataValue? value)) {
            return new DataValueState(value, parseIntAsHex);
        }
        else if (bTryParseAsFloat && float.TryParse(input.AsSpan(0, input.Length - 1), out float floatValue)) {
            return new DataValueState(new DataValueFloat(floatValue), false);
        }
        else if (bTryParseAsDouble && double.TryParse(input.AsSpan(0, input.Length - (bInputEndsWithD ? 1 : 0)), out double doubleValue)) {
            return new DataValueState(new DataValueDouble(doubleValue), false);
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid text", mainArgs.Errors.Count > 0 ? mainArgs.Errors[0] : "Could not parse value as " + initialDataType, defaultButton: MessageBoxResult.OK);
            return null;
        }
    }
}