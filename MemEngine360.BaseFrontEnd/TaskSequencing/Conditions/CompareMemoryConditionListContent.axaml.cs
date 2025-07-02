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

using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using MemEngine360.BaseFrontEnd.Utils;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Conditions;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.ComboBoxes;
using PFXToolKitUI.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Conditions;

public delegate void CompareMemoryConditionListContentEventHandler(CompareMemoryConditionListContent sender);

public partial class CompareMemoryConditionListContent : BaseConditionListContent {
    private DataType dataType;
    private string parsingText;

    private readonly TextBoxToEventPropertyBinder<CompareMemoryConditionListContent> valueBinder = new TextBoxToEventPropertyBinder<CompareMemoryConditionListContent>(nameof(ParsingTextChanged), (b) => b.Model.ParsingText, async (b, text) => {
        DataValueState? newState = await BinderParsingUtils.TryParseTextAsDataValueAndModify(text, b.Model.DataType, b.Model.Condition!.ParseIntAsHex);
        if (newState is DataValueState result) {
            b.Model.isUpdatingProviderDataValue = true;
            b.Model.Condition!.ParseIntAsHex = result.ParseIntAsHex;
            b.Model.Condition!.CompareTo = result.Value;
            b.Model.isUpdatingProviderDataValue = false;
            b.Model.UpdateTextFromProviderValue();
            return true;
        }

        return false;
    });

    private readonly IBinder<CompareMemoryCondition> addressBinder = new TextBoxToEventPropertyBinder<CompareMemoryCondition>(nameof(CompareMemoryCondition.AddressChanged), (b) => b.Model.Address.ToString(), (b, text) => BinderParsingUtils.TryParseAddressEx(text, b, (a, v) => a.Model.Address = v));
    private readonly ComboBoxToEventPropertyEnumBinder<DataType> dataTypeBinder = new ComboBoxToEventPropertyEnumBinder<DataType>(typeof(CompareMemoryConditionListContent), nameof(DataTypeChanged), (x) => ((CompareMemoryConditionListContent) x).DataType, (x, y) => ((CompareMemoryConditionListContent) x).DataType = y);

    public DataType DataType {
        get => this.dataType;
        set {
            PropertyHelper.SetAndRaiseINE(ref this.dataType, value, this, static t => t.DataTypeChanged?.Invoke(t));
            CompareMemoryCondition? condition = this.Condition;
            if (condition?.CompareTo != null && condition.CompareTo.DataType != value) {
                condition.CompareTo = IDataValue.TryConvertDataValue(condition.CompareTo, value, (condition.CompareTo as DataValueString)?.StringType ?? StringType.ASCII);
            }

            if (condition != null && !value.IsNumeric()) {
                if (condition.CompareType != CompareType.Equals && condition.CompareType != CompareType.NotEquals) {
                    condition.CompareType = CompareType.Equals;
                }
            }
        }
    }
    
    public string ParsingText {
        get => this.parsingText;
        set => PropertyHelper.SetAndRaiseINE(ref this.parsingText, value, this, static t => t.ParsingTextChanged?.Invoke(t));
    }

    public event CompareMemoryConditionListContentEventHandler? DataTypeChanged;
    public event CompareMemoryConditionListContentEventHandler? ParsingTextChanged;

    public new CompareMemoryCondition? Condition => (CompareMemoryCondition?) base.Condition;

    private bool isUpdatingToggleButtons;
    private bool isUpdatingProviderDataValue;

    public CompareMemoryConditionListContent() {
        this.InitializeComponent();
        this.addressBinder.AttachControl(this.PART_AddressTextBox);
        this.valueBinder.AttachControl(this.PART_ValueTextBox);
        this.SetConditionMetIndicator(this.PART_IsConditionMetEllipse);
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        this.dataTypeBinder.Attach(this.PART_DataTypeComboBox, this);
        base.OnLoaded(e);
    }

    protected override void OnUnloaded(RoutedEventArgs e) {
        this.dataTypeBinder.Detach();
        base.OnUnloaded(e);
    }

    public void UpdateTextFromProviderValue() {
        CompareMemoryCondition? cond = this.Condition;
        this.ParsingText = cond?.CompareTo != null
            ? DataValueUtils.GetStringFromDataValue(cond.CompareTo,
                this.DataType.IsInteger() && cond.ParseIntAsHex
                    ? NumericDisplayType.Hexadecimal
                    : NumericDisplayType.Normal)
            : "";
    }

    protected override void OnConditionChanged(BaseSequenceCondition? oldCondition, BaseSequenceCondition? newCondition) {
        base.OnConditionChanged(oldCondition, newCondition);

        if (oldCondition is CompareMemoryCondition oldCond) {
            oldCond.CompareToChanged -= this.OnCompareToChanged;
            oldCond.CompareTypeChanged -= this.OnCompareTypeChangedChanged;
            oldCond.ParseIntAsHexChanged -= this.OnParseIntAsHexChanged;
            this.addressBinder.DetachModel();
            this.valueBinder.DetachModel();
        }

        if (newCondition is CompareMemoryCondition newCond) {
            newCond.CompareToChanged += this.OnCompareToChanged;
            newCond.CompareTypeChanged += this.OnCompareTypeChangedChanged;
            newCond.ParseIntAsHexChanged += this.OnParseIntAsHexChanged;
            if (newCond.CompareTo != null) {
                PropertyHelper.SetAndRaiseINE(ref this.dataType, newCond.CompareTo.DataType, this, static t => t.DataTypeChanged?.Invoke(t));
            }
            else {
                this.DataType = DataType.Int32;
            }

            this.addressBinder.AttachModel(newCond);
            this.valueBinder.AttachModel(this);
            this.OnCompareTypeChangedChanged(newCond);
            this.UpdateTextFromProviderValue();
        }
    }

    private void OnCompareToChanged(CompareMemoryCondition sender) {
        if (sender.CompareTo != null) {
            PropertyHelper.SetAndRaiseINE(ref this.dataType, sender.CompareTo.DataType, this, static t => t.DataTypeChanged?.Invoke(t));
        }
        else {
            this.DataType = DataType.Int32;
        }

        this.UpdateTextFromProviderValue();
        
        this.PART_CMP_EQ.IsEnabled = true;
        this.PART_CMP_NEQ.IsEnabled = true;
        this.PART_CMP_LT.IsEnabled = true;
        this.PART_CMP_LTEQ.IsEnabled = true;
        this.PART_CMP_GT.IsEnabled = true;
        this.PART_CMP_GTEQ.IsEnabled = true;
        if (!this.DataType.IsNumeric()) {
            this.PART_CMP_LT.IsEnabled = false;
            this.PART_CMP_LTEQ.IsEnabled = false;
            this.PART_CMP_GT.IsEnabled = false;
            this.PART_CMP_GTEQ.IsEnabled = false;
        }
    }

    private void OnCompareTypeChangedChanged(CompareMemoryCondition sender) {
        this.isUpdatingToggleButtons = true;
        this.PART_CMP_EQ.IsChecked = false;
        this.PART_CMP_NEQ.IsChecked = false;
        this.PART_CMP_LT.IsChecked = false;
        this.PART_CMP_LTEQ.IsChecked = false;
        this.PART_CMP_GT.IsChecked = false;
        this.PART_CMP_GTEQ.IsChecked = false;
        switch (sender.CompareType) {
            case CompareType.Equals:              this.PART_CMP_EQ.IsChecked = true; break;
            case CompareType.NotEquals:           this.PART_CMP_NEQ.IsChecked = true; break;
            case CompareType.LessThan:            this.PART_CMP_LT.IsChecked = true; break;
            case CompareType.LessThanOrEquals:    this.PART_CMP_LTEQ.IsChecked = true; break;
            case CompareType.GreaterThan:         this.PART_CMP_GT.IsChecked = true; break;
            case CompareType.GreaterThanOrEquals: this.PART_CMP_GTEQ.IsChecked = true; break;
            default:                              throw new ArgumentOutOfRangeException();
        }

        this.isUpdatingToggleButtons = false;
    }
    
    private void OnParseIntAsHexChanged(CompareMemoryCondition sender) {
        this.UpdateTextFromProviderValue();
    }

    private void PART_CompareModeClicked(object? sender, RoutedEventArgs e) {
        if (this.isUpdatingToggleButtons || base.Condition == null) {
            return;
        }

        ((ToggleButton) sender!).IsChecked = true;
        if (ReferenceEquals(sender, this.PART_CMP_EQ))
            this.Condition!.CompareType = CompareType.Equals;
        else if (ReferenceEquals(sender, this.PART_CMP_NEQ))
            this.Condition!.CompareType = CompareType.NotEquals;
        else if (ReferenceEquals(sender, this.PART_CMP_LT))
            this.Condition!.CompareType = CompareType.LessThan;
        else if (ReferenceEquals(sender, this.PART_CMP_LTEQ))
            this.Condition!.CompareType = CompareType.LessThanOrEquals;
        else if (ReferenceEquals(sender, this.PART_CMP_GT))
            this.Condition!.CompareType = CompareType.GreaterThan;
        else if (ReferenceEquals(sender, this.PART_CMP_GTEQ))
            this.Condition!.CompareType = CompareType.GreaterThanOrEquals;
    }
}