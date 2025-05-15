# Basic Dialogs (input, message, etc.)
PFXToolKitUI contains some basic dialog systems for accepting user inputs.

Some include:
- Single input
- Double input
- Showing messages (with yes/ok, no, cancel, none feedback)
- Keyboard and Mouse strokes
- File open/save pickers
- Colour picking

MemEngine360 also contains some custom variants such as:
- Memory region selector
- Module selector

These all use the user input info system. The base class is `UserInputInfo`.

For example, if you want to accept two integer inputs:

```
// Validate integer value -- display "Value too big" just for the fucks of it
Action<ValidationArgs> validateInteger = (a) => {
    if (!uint.TryParse(a.Input, NumberStyles.HexNumber, null, out _)) {
        if (ulong.TryParse(a.Input, NumberStyles.HexNumber, null, out _)) {
            a.Errors.Add("Value is too big. Maximum is 0xFFFFFFFF");
        }
        else {
            a.Errors.Add("Invalid UInt32.");
        }
    }
};

ScanningProcessor p = engine.ScanningProcessor;
DoubleUserInputInfo info = new DoubleUserInputInfo() {
    Caption = "Start and Length",
    Message = "Input the start and length values",
    ConfirmText = "Start", DefaultButton = true,
    LabelA = "Start address (hex)", 
    LabelB = "Length (hex)",
    TextA = "82600000",
    TextB = "4000"
    ValidateA = validateInteger, ValidateB = validateInteger,
    Footer = "Some custom footer text just above the buttons panel",
};

// Show dialog
if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
    // This code below should never fail what so ever, since the validation
    // is ALWAYS run before ShowInputDialogAsync returns true
    uint start = uint.Parse(info.TextA, NumberStyles.HexNumber);
    uint length = uint.Parse(info.TextB, NumberStyles.HexNumber);
}
```

## Creating custom user input info dialogs
This is some example code for registering/mapping your model type to a control:

```
UserInputDialogView.Registry.RegisterType<DoubleUserInputInfo>(() => new DoubleUserInputControl());
```

`DoubleUserInputControl` implements `IUserInputContent`. Either this or the single input version are 
fairly basic to help you get to grips on how it all works. For example, knowing what to do in `UserInputInfo.UpdateAllErrors()`