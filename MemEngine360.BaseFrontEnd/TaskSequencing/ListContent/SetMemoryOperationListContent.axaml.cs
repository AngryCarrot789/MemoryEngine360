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
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.Sequencing.Operations;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.ComboBoxes;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.ListContent;

public partial class SetMemoryOperationListContent : BaseOperationListContent {
    public delegate void SetMemoryOperationListContentEventHandler(SetMemoryOperationListContent sender);

    private DataType? myDataType;
    private string textValue = "";

    private readonly IBinder<SetMemoryOperation> addressBinder = new TextBoxToEventPropertyBinder<SetMemoryOperation>(nameof(SetMemoryOperation.AddressChanged), (b) => b.Model.Address.ToString("X8"), async (b, text) => {
        if (uint.TryParse(text, NumberStyles.HexNumber, null, out uint value)) {
            b.Model.Address = value;
            return true;
        }
        else if (ulong.TryParse(text, NumberStyles.HexNumber, null, out _)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Address is too long. It can only be 4 bytes", defaultButton: MessageBoxResult.OK);
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Start address is invalid", defaultButton: MessageBoxResult.OK);
        }
        
        return false;
    });

    private readonly TextBoxToEventPropertyBinder<SetMemoryOperationListContent> valueBinder = new TextBoxToEventPropertyBinder<SetMemoryOperationListContent>(nameof(TextValueChanged), (b) => b.Model.TextValue, async (b, text) => {
        bool hexPrefix = text.StartsWith("0x");
        ValidationArgs args = new ValidationArgs(hexPrefix ? text.Substring(2) : text, [], false);
        if (b.Model.myDataType is DataType dt) {
            NumericDisplayType intNdt = dt.IsInteger() && hexPrefix ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
            if (MemoryEngine360.TryParseTextAsDataValue(args, dt, intNdt, StringType.ASCII, out IDataValue? value)) {
                b.Model.Operation!.DataValueProvider = new ConstantDataProvider(value);
                b.Model.TextValue = (hexPrefix ? "0x" : "") + intNdt.AsString(dt, value.BoxedValue);
                return true;
            }
            else {
                await IMessageDialogService.Instance.ShowMessage("Invalid text", args.Errors.Count > 0 ? args.Errors[0] : "Could not parse value as " + dt, defaultButton: MessageBoxResult.OK);
                return false;
            }
        }
        else {
            b.Model.TextValue = text;
            return true;
        }
    });

    private readonly ComboBoxToEventPropertyEnumBinder<DataType> dataTypeBinder = new ComboBoxToEventPropertyEnumBinder<DataType>(typeof(SetMemoryOperationListContent), nameof(MyDataTypeChanged), (x) => ((SetMemoryOperationListContent) x).MyDataType, (x, y) => ((SetMemoryOperationListContent) x).MyDataType = y);

    private readonly IBinder<SetMemoryOperation> iterateBinder = new TextBoxToEventPropertyBinder<SetMemoryOperation>(nameof(SetMemoryOperation.IterateCountChanged), (b) => b.Model.IterateCount.ToString(), async (b, text) => {
        if (uint.TryParse(text, out uint value)) {
            b.Model.IterateCount = value;
            return true;
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", $"Iterate count is invalid. It must be between 0 and {uint.MaxValue}", defaultButton: MessageBoxResult.OK);
            return false;
        }
    });

    private DataType? MyDataType {
        get => this.myDataType;
        set {
            if (this.myDataType == value)
                return;

            this.myDataType = value;
            this.MyDataTypeChanged?.Invoke(this);

            if (this.PART_DataTypeComboBox.IsEffectivelyVisible) {
                // this.PART_ValueTextBox.SelectAll();
                ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    BugFix.TextBox_FocusSelectAll(this.PART_ValueTextBox);
                }, DispatchPriority.Background);
                // BugFix.TextBox_FocusSelectAll(this.PART_ValueTextBox);
                // this.valueBinder.OnHandleUpdateModel();
            }
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

    public new SetMemoryOperation? Operation => (SetMemoryOperation?) base.Operation;

    public event SetMemoryOperationListContentEventHandler? MyDataTypeChanged;
    public event SetMemoryOperationListContentEventHandler? TextValueChanged;

    public SetMemoryOperationListContent() {
        this.InitializeComponent();
        this.dataTypeBinder.Attach(this.PART_DataTypeComboBox, this);
        this.addressBinder.AttachControl(this.PART_AddressTextBox);
        this.valueBinder.Attach(this.PART_ValueTextBox, this);
        this.iterateBinder.AttachControl(this.PART_IterateCountTextBox);
    }

    protected override void OnOperationChanged(BaseSequenceOperation? oldOperation, BaseSequenceOperation? newOperation) {
        base.OnOperationChanged(oldOperation, newOperation);
        this.addressBinder.SwitchModel((SetMemoryOperation?) newOperation);
        this.iterateBinder.SwitchModel((SetMemoryOperation?) newOperation);

        if (oldOperation != null)
            ((SetMemoryOperation) oldOperation).DataValueProviderChanged -= this.OnDataValueProviderChanged;

        if (newOperation != null) {
            ((SetMemoryOperation) newOperation).DataValueProviderChanged += this.OnDataValueProviderChanged;
            this.OnDataValueProviderChanged((SetMemoryOperation) newOperation);
        }

        if (newOperation is SetMemoryOperation operation) {
            if (operation.DataValueProvider is ConstantDataProvider provider) {
                this.MyDataType = provider.DataValue?.DataType ?? DataType.Int32;
                this.TextValue = provider.DataValue?.BoxedValue.ToString() ?? "";
                this.PART_ToValueStackPanel.IsVisible = true;
            }
            else {
                this.PART_ToValueStackPanel.IsVisible = false;
            }
        }
    }

    private void OnDataValueProviderChanged(SetMemoryOperation sender) {
        this.PART_ToValueStackPanel.IsVisible = sender.DataValueProvider == null || sender.DataValueProvider is ConstantDataProvider;
        if (sender.DataValueProvider == null || !(sender.DataValueProvider is ConstantDataProvider provider) || provider.DataValue == null) {
            this.MyDataType = null;
            this.TextValue = "";
        }
        else {
            bool hexPrefix = this.TextValue.StartsWith("0x");
            NumericDisplayType intNdt = provider.DataValue.DataType.IsInteger() && hexPrefix ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
            this.MyDataType = provider.DataValue.DataType;
            this.TextValue = (hexPrefix ? "0x" : "") + intNdt.AsString(provider.DataValue.DataType, provider.DataValue.BoxedValue);
        }
    }
}