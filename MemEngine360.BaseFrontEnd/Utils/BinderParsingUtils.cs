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
using MemEngine360.Engine.Modes;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.BaseFrontEnd.Utils;

/// <summary>
/// A helper class for binders. This provides reusable parsing logic, typically for <see cref="TextBoxToEventPropertyBinder{TModel}"/>
/// </summary>
public static class BinderParsingUtils {
    public static async Task<bool> TryParseAddress<T>(string text, IBinder<T> binder, Action<IBinder<T>, uint> update, string tooLongMessage = "Address is too long. It can only be 4 bytes", string invalidValueMessage = "Start address is invalid") where T : class {
        if (uint.TryParse(text, NumberStyles.HexNumber, null, out uint value)) {
            update(binder, value);
            return true;
        }
        else if (ulong.TryParse(text, NumberStyles.HexNumber, null, out _)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", tooLongMessage, defaultButton: MessageBoxResult.OK);
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", invalidValueMessage, defaultButton: MessageBoxResult.OK);
        }

        return false;
    }

    public static async Task<IDataValue?> TryParseTextAsDataValueAndModify(DataProviderHandler h, DataType dataType, string text) {
        bool hasHexPrefix = text.StartsWith("0x");
        if (hasHexPrefix && !h.ParseIntAsHex && dataType.IsInteger()) {
            h.ParseIntAsHex = true;
        }
        else {
            h.ParseIntAsHex = false;
        }
        
        ValidationArgs args = new ValidationArgs(h.ParseIntAsHex && hasHexPrefix ? text.Substring(2) : text, [], false);
        NumericDisplayType intNdt = h.ParseIntAsHex ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
        if (DataValueUtils.TryParseTextAsDataValue(args, dataType, intNdt, StringType.ASCII, out IDataValue? value)) {
            return value;
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid text", args.Errors.Count > 0 ? args.Errors[0] : "Could not parse value as " + dataType, defaultButton: MessageBoxResult.OK);
            return null;
        }
    }
    
    public static async Task<(IDataValue?, bool parseIntAsHex)> TryParseTextAsDataValueAndModify(bool parseIntAsHex, DataType dataType, string text) {
        bool hasHexPrefix = text.StartsWith("0x");
        if (hasHexPrefix && !parseIntAsHex && dataType.IsInteger()) {
            parseIntAsHex = true;
        }
        else {
            parseIntAsHex = false;
        }
        
        ValidationArgs args = new ValidationArgs(parseIntAsHex && hasHexPrefix ? text.Substring(2) : text, [], false);
        NumericDisplayType intNdt = parseIntAsHex ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
        if (DataValueUtils.TryParseTextAsDataValue(args, dataType, intNdt, StringType.ASCII, out IDataValue? value)) {
            return (value, parseIntAsHex);
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid text", args.Errors.Count > 0 ? args.Errors[0] : "Could not parse value as " + dataType, defaultButton: MessageBoxResult.OK);
            return (null, parseIntAsHex);
        }
    }
}