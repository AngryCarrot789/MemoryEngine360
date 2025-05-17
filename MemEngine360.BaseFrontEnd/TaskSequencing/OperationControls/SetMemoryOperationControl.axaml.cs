// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using System.Globalization;
using Avalonia.Interactivity;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Operations;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.ComboBoxes;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.OperationControls;

public delegate void SetMemoryOperationControlEventHandler(SetMemoryOperationControl sender);

public partial class SetMemoryOperationControl : BaseOperationControl {
    private DataType? myDataType;
    private string textValue = "";

    private readonly IBinder<SetMemoryOperation> addressBinder = new TextBoxToEventPropertyBinder<SetMemoryOperation>(nameof(SetMemoryOperation.AddressChanged), (b) => b.Model.Address.ToString("X8"), async (b, text) => {
        if (uint.TryParse(text, NumberStyles.HexNumber, null, out uint value)) {
            b.Model.Address = value;
        }
        else if (ulong.TryParse(text, NumberStyles.HexNumber, null, out _)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Address is too long. It can only be 4 bytes", defaultButton: MessageBoxResult.OK);
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Start address is invalid", defaultButton: MessageBoxResult.OK);
        }
    });

    private readonly IBinder<SetMemoryOperationControl> valueBinder = new TextBoxToEventPropertyBinder<SetMemoryOperationControl>(nameof(TextValueChanged), (b) => b.Model.TextValue, async (b, text) => {
        bool hexPrefix = text.StartsWith("0x");
        ValidationArgs args = new ValidationArgs(hexPrefix ? text.Substring(2) : text, [], false);
        if (b.Model.myDataType is DataType dt) {
            NumericDisplayType intNdt = dt.IsInteger() && hexPrefix ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
            if (MemoryEngine360.TryParseTextAsDataValue(args, dt, intNdt, StringType.UTF8, out IDataValue? value)) {
                ((SetMemoryOperation) b.Model.Operation!).DataValue = value;
                b.Model.TextValue = (hexPrefix ? "0x" : "") + intNdt.AsString(dt, value.BoxedValue);
            }
            else {
                await IMessageDialogService.Instance.ShowMessage("Invalid text", args.Errors.Count > 0 ? args.Errors[0] : "Could not parse value as " + dt, defaultButton: MessageBoxResult.OK);
            }
        }
        else {
            b.Model.TextValue = text;
        }
    });

    private readonly ComboBoxToEventPropertyEnumBinder<DataType> dataTypeBinder = new ComboBoxToEventPropertyEnumBinder<DataType>(typeof(SetMemoryOperationControl), nameof(MyDataTypeChanged), (x) => ((SetMemoryOperationControl) x).MyDataType, (x, y) => ((SetMemoryOperationControl) x).MyDataType = y);

    private readonly IBinder<SetMemoryOperation> repeatBinder = new TextBoxToEventPropertyBinder<SetMemoryOperation>(nameof(SetMemoryOperation.RepeatCountChanged), (b) => b.Model.RepeatCount.ToString(), async (b, text) => {
        if (uint.TryParse(text, out uint value)) {
            b.Model.RepeatCount = value;
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Repeat count is invalid", defaultButton: MessageBoxResult.OK);
        }
    });

    private DataType? MyDataType {
        get => this.myDataType;
        set {
            if (this.myDataType == value)
                return;

            this.myDataType = value;
            this.MyDataTypeChanged?.Invoke(this);
        }
    }

    private string TextValue {
        get => this.textValue;
        set {
            if (this.textValue == value)
                return;

            this.textValue = value;
            this.TextValueChanged?.Invoke(this);
        }
    }

    public event SetMemoryOperationControlEventHandler? MyDataTypeChanged;
    public event SetMemoryOperationControlEventHandler? TextValueChanged;

    public SetMemoryOperationControl() {
        this.InitializeComponent();
        this.dataTypeBinder.Attach(this.PART_DataTypeComboBox, this);
        this.addressBinder.AttachControl(this.PART_AddressTextBox);
        this.valueBinder.Attach(this.PART_ValueTextBox, this);
        this.repeatBinder.AttachControl(this.PART_RepeatTextBox);
    }

    protected override void OnOperationChanged(BaseSequenceOperation? oldOperation, BaseSequenceOperation? newOperation) {
        base.OnOperationChanged(oldOperation, newOperation);
        this.addressBinder.SwitchModel((SetMemoryOperation?) newOperation);
        this.repeatBinder.SwitchModel((SetMemoryOperation?) newOperation);

        if (oldOperation != null)
            ((SetMemoryOperation) oldOperation).DataValueChanged -= this.OnDataValueChanged;

        if (newOperation != null) {
            ((SetMemoryOperation) newOperation).DataValueChanged += this.OnDataValueChanged;
            this.OnDataValueChanged((SetMemoryOperation) newOperation);
        }

        if (newOperation is SetMemoryOperation operation) {
            this.MyDataType = operation.DataValue?.DataType ?? DataType.Int32;
            this.TextValue = operation.DataValue?.BoxedValue.ToString() ?? "";
        }
    }

    private void OnDataValueChanged(SetMemoryOperation sender) {
        if (sender.DataValue == null) {
            this.MyDataType = null;
            this.TextValue = "";
        }
        else {
            bool hexPrefix = this.TextValue.StartsWith("0x");
            NumericDisplayType intNdt = sender.DataValue.DataType.IsInteger() && hexPrefix ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
            this.MyDataType = sender.DataValue.DataType;
            this.TextValue = (hexPrefix ? "0x" : "") + intNdt.AsString(sender.DataValue.DataType, sender.DataValue.BoxedValue);
        }
    }

    private void Button_RemoveClick(object? sender, RoutedEventArgs e) {
        this.Operation?.Sequence!.RemoveOperation(this.Operation);
    }
}