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

    public static async Task<(IDataValue, string)?> TryParseTextAsDataValueAndModify(DataProviderHandler h, DataType dataType, string text) {
        bool hasHexPrefix = text.StartsWith("0x");
        if (hasHexPrefix && !h.ParseIntAsHex && dataType.IsInteger()) {
            h.ParseIntAsHex = true;
        }
        
        ValidationArgs args = new ValidationArgs(h.ParseIntAsHex && hasHexPrefix ? text.Substring(2) : text, [], false);
        NumericDisplayType intNdt = h.ParseIntAsHex ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
        if (MemoryEngine360.TryParseTextAsDataValue(args, dataType, intNdt, StringType.ASCII, out IDataValue? value)) {
            return (value, intNdt.AsString(dataType, value.BoxedValue));
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid text", args.Errors.Count > 0 ? args.Errors[0] : "Could not parse value as " + dataType, defaultButton: MessageBoxResult.OK);
            return null;
        }
    }
}