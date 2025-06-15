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

using Avalonia.Controls;
using MemEngine360.BaseFrontEnd.Utils;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.DataHandlers;

public delegate void RandomDataValueHandlerEventHandler(RandomNumericDataValueHandler sender);

public class RandomNumericDataValueHandler : DataProviderHandler<RandomNumberDataProvider> {
    private readonly TextBoxToEventPropertyBinder<RandomNumericDataValueHandler> minimumBinder = new TextBoxToEventPropertyBinder<RandomNumericDataValueHandler>(nameof(ParsingMinimumTextChanged), (b) => b.Model.ParsingMinimumText, async (b, text) => {
        b.Model.isUpdatingProviderValues = true;
        IDataValue? result = await BinderParsingUtils.TryParseTextAsDataValueAndModify(text, b.Model.DataType, b.Model);
        if (result != null) {
            BaseNumericDataValue? max;
            BaseNumericDataValue? newValue = (BaseNumericDataValue?) result;
            if (newValue != null && (max = b.Model.Provider.Maximum) != null && newValue.CompareTo(max) > 0) {
                newValue = max;
            }

            b.Model.Provider.Minimum = newValue;
        }

        b.Model.isUpdatingProviderValues = false;
        b.Model.UpdateTextFromMinimumValue();
        return result != null;
    });

    private readonly TextBoxToEventPropertyBinder<RandomNumericDataValueHandler> maximumBinder = new TextBoxToEventPropertyBinder<RandomNumericDataValueHandler>(nameof(ParsingMaximumTextChanged), (b) => b.Model.ParsingMaximumText, async (b, text) => {
        b.Model.isUpdatingProviderValues = true;
        IDataValue? result = await BinderParsingUtils.TryParseTextAsDataValueAndModify(text, b.Model.DataType, b.Model);
        if (result != null) {
            BaseNumericDataValue? min;
            BaseNumericDataValue? newValue = (BaseNumericDataValue?) result;
            if (newValue != null && (min = b.Model.Provider.Minimum) != null && newValue.CompareTo(min) < 0) {
                newValue = min;
            }

            b.Model.Provider.Maximum = newValue;
        }

        b.Model.isUpdatingProviderValues = false;
        b.Model.UpdateTextFromMaximumValue();
        return result != null;
    });

    private string parsingMinimumText, parsingMaximumText;
    private bool isUpdatingProviderValues;

    public string ParsingMinimumText {
        get => this.parsingMinimumText;
        set => PropertyHelper.SetAndRaiseINE(ref this.parsingMinimumText, value, this, static t => t.ParsingMinimumTextChanged?.Invoke(t));
    }

    public string ParsingMaximumText {
        get => this.parsingMaximumText;
        set => PropertyHelper.SetAndRaiseINE(ref this.parsingMaximumText, value, this, static t => t.ParsingMaximumTextChanged?.Invoke(t));
    }

    public event RandomDataValueHandlerEventHandler? ParsingMinimumTextChanged, ParsingMaximumTextChanged;

    public TextBox PART_Minimum { get; }

    public TextBox PART_Maximum { get; }

    public RandomNumericDataValueHandler(TextBox partMinimum, TextBox partMaximum) {
        this.PART_Minimum = partMinimum;
        this.PART_Maximum = partMaximum;
    }
    
    protected override void OnParseIntAsHexChanged() {
        base.OnParseIntAsHexChanged();
        if (this.IsConnected) {
            this.Provider.ParseIntAsHex = this.ParseIntAsHex;
        }
    }

    public void UpdateTextFromMinimumValue() {
        BaseNumericDataValue? value = this.Provider.Minimum;
        this.ParsingMinimumText = value != null
            ? DataValueUtils.GetStringFromDataValue(value,
                this.DataType.IsInteger() && this.ParseIntAsHex
                    ? NumericDisplayType.Hexadecimal
                    : NumericDisplayType.Normal)
            : "";
    }

    public void UpdateTextFromMaximumValue() {
        BaseNumericDataValue? value = this.Provider.Maximum;
        this.ParsingMaximumText = value != null
            ? DataValueUtils.GetStringFromDataValue(value,
                this.DataType.IsInteger() && this.ParseIntAsHex
                    ? NumericDisplayType.Hexadecimal
                    : NumericDisplayType.Normal)
            : "";
    }

    protected override void OnConnected() {
        this.ParseIntAsHex = this.Provider.ParseIntAsHex;
        this.DataType = this.Provider.DataType;
        this.UpdateTextFromMinimumValue();
        this.UpdateTextFromMaximumValue();
        this.minimumBinder.Attach(this.PART_Minimum, this);
        this.maximumBinder.Attach(this.PART_Maximum, this);
        this.Provider.MinimumChanged += this.OnProviderMinimumChanged;
        this.Provider.MaximumChanged += this.OnProviderMaximumChanged;
        this.Provider.DataTypeChanged += this.OnProviderDataTypeChanged;
        this.Provider.ParseIntAsHexChanged += this.OnProviderParseIntAsHexChanged;
    }

    protected override void OnDisconnect() {
        this.minimumBinder.Detach();
        this.maximumBinder.Detach();
        this.Provider.MinimumChanged -= this.OnProviderMinimumChanged;
        this.Provider.MaximumChanged -= this.OnProviderMaximumChanged;
        this.Provider.DataTypeChanged -= this.OnProviderDataTypeChanged;
        this.Provider.ParseIntAsHexChanged -= this.OnProviderParseIntAsHexChanged;
    }

    private void OnProviderMinimumChanged(RandomNumberDataProvider sender) {
        if (!this.isUpdatingProviderValues)
            this.UpdateTextFromMinimumValue();
    }

    private void OnProviderMaximumChanged(RandomNumberDataProvider sender) {
        if (!this.isUpdatingProviderValues)
            this.UpdateTextFromMaximumValue();
    }

    private void OnProviderDataTypeChanged(RandomNumberDataProvider sender) {
        lock (sender.Lock) {
            BaseNumericDataValue? oldMin = sender.Minimum, oldMax = sender.Maximum;
            sender.Minimum = sender.Maximum = null;
            this.DataType = sender.DataType;
            this.isUpdatingProviderValues = true;
            sender.Minimum = (BaseNumericDataValue?) IDataValue.TryConvertDataValue(oldMin, this.DataType, default) ?? IDataValue.CreateDefaultNumeric(this.DataType);
            sender.Maximum = (BaseNumericDataValue?) IDataValue.TryConvertDataValue(oldMax, this.DataType, default) ?? IDataValue.CreateDefaultNumeric(this.DataType);
            this.isUpdatingProviderValues = false;

            this.UpdateTextFromMinimumValue();
            this.UpdateTextFromMaximumValue();
        }
    }

    private void OnProviderParseIntAsHexChanged(RandomNumberDataProvider sender) {
        this.ParseIntAsHex = sender.ParseIntAsHex;
        if (!this.isUpdatingProviderValues) {
            this.UpdateTextFromMinimumValue();
            this.UpdateTextFromMaximumValue();
        }
    }
    
    protected override void OnDataTypeChanged() {
        base.OnDataTypeChanged();
        this.Provider.DataType = this.DataType;
    }
}