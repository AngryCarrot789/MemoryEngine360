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

using Avalonia.Controls;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.Sequencing.Operations;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.ComboBoxes;
using PFXToolKitUI.Avalonia.Bindings.Enums;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.EditorContent;

public delegate void SetMemoryOperationEditorContentEventHandler(SetMemoryOperationEditorContent sender);

public partial class SetMemoryOperationEditorContent : BaseOperationEditorContent {
    private readonly AvaloniaPropertyToEventPropertyBinder<SetMemoryOperationEditorContent> parseIntAsHexBinder = new AvaloniaPropertyToEventPropertyBinder<SetMemoryOperationEditorContent>(CheckBox.IsCheckedProperty, nameof(ParseIntAsHexChanged), (b) => ((CheckBox) b.Control).IsChecked = b.Model.ParseIntAsHex, (b) => b.Model.ParseIntAsHex = ((CheckBox) b.Control).IsChecked == true);
    private readonly EventPropertyEnumBinder<StringType> stringScanModeBinder = new EventPropertyEnumBinder<StringType>(typeof(SetMemoryOperationEditorContent), nameof(StringTypeChanged), (x) => ((SetMemoryOperationEditorContent) x).StringType, (x, v) => ((SetMemoryOperationEditorContent) x).StringType = v);
    private readonly ComboBoxToEventPropertyEnumBinder<DataType> dataTypeBinder = new ComboBoxToEventPropertyEnumBinder<DataType>(typeof(SetMemoryOperationEditorContent), nameof(DataTypeChanged), (x) => ((SetMemoryOperationEditorContent) x).DataType, (x, y) => ((SetMemoryOperationEditorContent) x).DataType = y);
    private readonly TextBoxToEventPropertyBinder<SetMemoryOperationEditorContent> constantValueBinder = new TextBoxToEventPropertyBinder<SetMemoryOperationEditorContent>(nameof(ConstantDataValueChanged), (b) => GetTextFromDataValue(b.Model.ConstantDataValue, b.Model), async (b, s) => await UpdateModel(b, s, (c, v) => c.ConstantDataValue = v));
    private readonly TextBoxToEventPropertyBinder<SetMemoryOperationEditorContent> rangedDvABinder = new TextBoxToEventPropertyBinder<SetMemoryOperationEditorContent>(nameof(RangedDataValueAChanged), (b) => GetTextFromDataValue(b.Model.RangedDataValueA, b.Model), async (b, s) => await UpdateModel(b, s, (c, v) => c.RangedDataValueA = v));
    private readonly TextBoxToEventPropertyBinder<SetMemoryOperationEditorContent> rangedDvBBinder = new TextBoxToEventPropertyBinder<SetMemoryOperationEditorContent>(nameof(RangedDataValueBChanged), (b) => GetTextFromDataValue(b.Model.RangedDataValueB, b.Model), async (b, s) => await UpdateModel(b, s, (c, v) => c.RangedDataValueB = v));
    private readonly AvaloniaPropertyToEventPropertyBinder<SetMemoryOperationEditorContent> selectedTabIndexBinder;

    private StringType stringType;
    private IDataValue? constantDataValue;
    private IDataValue? rangedDvA, rangedDvB;
    private DataType dataType;
    private bool parseIntAsHex;
    private bool isRandomMode;

    public StringType StringType {
        get => this.stringType;
        set {
            if (this.stringType != value) {
                this.stringType = value;
                this.StringTypeChanged?.Invoke(this);

                this.ConstantDataValue = this.TryConvertDataValueToCurrentState(this.ConstantDataValue);
                this.UpdateOperationValues();
            }
        }
    }

    public IDataValue? ConstantDataValue {
        get => this.constantDataValue;
        set {
            if (!Equals(this.constantDataValue, value)) {
                if (value != null && value.DataType != this.DataType)
                    throw new InvalidOperationException("Attempt to set " + nameof(this.ConstantDataValue) + " with wrong data type");
                this.constantDataValue = value;
                this.ConstantDataValueChanged?.Invoke(this);
            }
        }
    }

    public IDataValue? RangedDataValueA {
        get => this.rangedDvA;
        set {
            if (!Equals(this.rangedDvA, value)) {
                if (value != null && value.DataType != this.DataType)
                    throw new InvalidOperationException("Attempt to set " + nameof(this.RangedDataValueA) + " with wrong data type");
                this.rangedDvA = value;
                this.RangedDataValueAChanged?.Invoke(this);
            }
        }
    }

    public IDataValue? RangedDataValueB {
        get => this.rangedDvB;
        set {
            if (!Equals(this.rangedDvB, value)) {
                if (value != null && value.DataType != this.DataType)
                    throw new InvalidOperationException("Attempt to set " + nameof(this.RangedDataValueB) + " with wrong data type");
                this.rangedDvB = value;
                this.RangedDataValueBChanged?.Invoke(this);
            }
        }
    }

    public DataType DataType {
        get => this.dataType;
        set {
            if (this.dataType != value) {
                this.dataType = value;
                this.DataTypeChanged?.Invoke(this);

                this.ConstantDataValue = this.TryConvertDataValueToCurrentState(this.ConstantDataValue);
                this.RangedDataValueA = this.TryConvertDataValueToCurrentState(this.RangedDataValueA);
                this.RangedDataValueB = this.TryConvertDataValueToCurrentState(this.RangedDataValueB);
                this.UpdateOperationValues();
            }
        }
    }

    public bool ParseIntAsHex {
        get => this.parseIntAsHex;
        set {
            if (this.parseIntAsHex != value) {
                this.parseIntAsHex = value;
                this.ParseIntAsHexChanged?.Invoke(this);
            }
        }
    }

    public bool IsRandomMode {
        get => this.isRandomMode;
        set {
            if (this.isRandomMode != value) {
                this.isRandomMode = value;
                this.IsRandomModeChanged?.Invoke(this);
                this.UpdateOperationValues();
            }
        }
    }

    public override string Caption => "Set Memory";

    public event SetMemoryOperationEditorContentEventHandler? StringTypeChanged;
    public event SetMemoryOperationEditorContentEventHandler? ConstantDataValueChanged;
    public event SetMemoryOperationEditorContentEventHandler? RangedDataValueAChanged;
    public event SetMemoryOperationEditorContentEventHandler? RangedDataValueBChanged;
    public event SetMemoryOperationEditorContentEventHandler? DataTypeChanged;
    public event SetMemoryOperationEditorContentEventHandler? ParseIntAsHexChanged;
    public event SetMemoryOperationEditorContentEventHandler? IsRandomModeChanged;

    public SetMemoryOperationEditorContent() {
        this.InitializeComponent();

        this.parseIntAsHexBinder.Attach(this.PART_ParseIntAsHexCheckBox, this);
        this.constantValueBinder.Attach(this.PART_ConstantValueTextBox, this);
        this.rangedDvABinder.Attach(this.PART_RangedValueATextBox, this);
        this.rangedDvBBinder.Attach(this.PART_RangedValueBTextBox, this);
        this.stringScanModeBinder.Assign(this.PART_DTString_ASCII, StringType.ASCII);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF8, StringType.UTF8);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF16, StringType.UTF16);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF32, StringType.UTF32);
        this.stringScanModeBinder.Attach(this);
        this.dataTypeBinder.Attach(this.PART_DataTypeCombo, this);

        this.selectedTabIndexBinder = new AvaloniaPropertyToEventPropertyBinder<SetMemoryOperationEditorContent>(TabControl.SelectedIndexProperty, nameof(this.IsRandomModeChanged), (b) => {
            ((TabControl) b.Control).SelectedIndex = b.Model.IsRandomMode ? 1 : 0;
        }, (b) => {
            b.Model.IsRandomMode = ((TabControl) b.Control).SelectedIndex == 1;
        });

        this.selectedTabIndexBinder.Attach(this.PART_ModeTabControl, this);
    }

    protected override void OnOperationChanged(BaseSequenceOperation? oldOperation, BaseSequenceOperation? newOperation) {
        base.OnOperationChanged(oldOperation, newOperation);

        if (oldOperation is SetMemoryOperation oldOp) {
            oldOp.DataValueProviderChanged -= this.OnDataValueProviderChanged;
        }
        
        if (newOperation is SetMemoryOperation newOp) {
            newOp.DataValueProviderChanged += this.OnDataValueProviderChanged;
            this.OnDataValueProviderChanged(newOp);
        }
    }

    private void OnDataValueProviderChanged(SetMemoryOperation sender) {
        if (sender.DataValueProvider == null) {
            this.IsRandomMode = false;
            this.DataType = DataType.Int32;
        }
        else if (sender.DataValueProvider is ConstantDataProvider constProvider) {
            if (constProvider.DataValue.DataType != this.dataType) {
                this.dataType = constProvider.DataValue.DataType;
                this.DataTypeChanged?.Invoke(this);
            }

            this.ConstantDataValue = constProvider.DataValue;
        }
        else if (sender.DataValueProvider is RandomNumberDataProvider randomProvider) {
            if (randomProvider.DataType != this.dataType) {
                this.dataType = randomProvider.DataType;
                this.DataTypeChanged?.Invoke(this);
            }
            
            this.RangedDataValueA = randomProvider.Minimum;
            this.RangedDataValueB = randomProvider.Maximum;
        }
    }

    private void UpdateOperationValues() {
        if (this.Operation == null) {
            return;
        }

        SetMemoryOperation operation = (SetMemoryOperation) this.Operation!;
        if (this.isRandomMode) {
            if (this.dataType.IsNumeric()) {
                if (this.rangedDvA is BaseNumericDataValue dvA && this.rangedDvB is BaseNumericDataValue dvB && 
                    dvA.DataType == this.dataType && dvB.DataType == this.dataType) {
                    operation.DataValueProvider = new RandomNumberDataProvider(this.dataType, dvA, dvB);
                }
                else {
                    operation.DataValueProvider = null;
                }
            }
            else {
                operation.DataValueProvider = null;
            }
        }
        else if (this.constantDataValue != null && this.constantDataValue.DataType == this.dataType) {
            operation.DataValueProvider = new ConstantDataProvider(this.constantDataValue);
        }
        else {
            operation.DataValueProvider = null;
        }
    }

    private IDataValue? TryConvertDataValueToCurrentState(IDataValue? oldValue) {
        if (oldValue == null) {
            return null;
        }

        // Same types?
        if (oldValue.DataType == this.DataType) {
            // String type differs? Convert to new string type
            if (oldValue.DataType == DataType.String && ((DataValueString) oldValue).StringType != this.StringType) {
                return new DataValueString(((DataValueString) oldValue).Value, this.StringType);
            }

            return oldValue;
        }

        // Old value is numeric?
        if (oldValue.DataType.IsNumeric()) {
            // Try convert into string
            if (this.DataType == DataType.String) {
                return new DataValueString(oldValue.BoxedValue.ToString() ?? "", this.stringType);
            }

            // Try convert into another numeric type
            return ((BaseNumericDataValue) oldValue).TryConvertTo(this.dataType, out BaseNumericDataValue? newValue) ? newValue : null;
        }

        // Old value is string or pattern... or another if more were added since this comment.
        return null;
    }

    private static string GetTextFromDataValue(IDataValue? value, SetMemoryOperationEditorContent control) {
        return value == null
            ? ""
            : MemoryEngine360.GetStringFromDataValue(value, control.ParseIntAsHex && control.dataType.IsInteger()
                ? NumericDisplayType.Hexadecimal
                : NumericDisplayType.Normal);
    }

    private static async Task<bool> UpdateModel(IBinder<SetMemoryOperationEditorContent> b, string text, Action<SetMemoryOperationEditorContent, IDataValue?> setValue) {
        ValidationArgs args = new ValidationArgs(text, [], false);
        DataType dt = b.Model.DataType;
        NumericDisplayType intNdt = dt.IsInteger() && b.Model.ParseIntAsHex ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
        if (MemoryEngine360.TryParseTextAsDataValue(args, dt, intNdt, b.Model.StringType, out IDataValue? value)) {
            setValue(b.Model, value);
            b.Model.UpdateOperationValues();
            return true;
        }
        else {
            setValue(b.Model, null);
            b.Model.UpdateOperationValues();
            return false;
        }
    }
}