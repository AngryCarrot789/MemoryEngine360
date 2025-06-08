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
    private readonly TextBoxToEventPropertyBinder<RandomNumericDataValueHandler> minimumBinder = new TextBoxToEventPropertyBinder<RandomNumericDataValueHandler>(nameof(ParsingMinimumChanged), (b) => b.Model.ParsingMinimum, async (b, text) => {
        (IDataValue, string)? result = await BinderParsingUtils.TryParseTextAsDataValueAndModify(b.Model, b.Model.DataType, text);
        if (!result.HasValue) {
            return false;
        }

        b.Model.Provider.Minimum = (BaseNumericDataValue?) result.Value.Item1;
        b.Model.ParsingMinimum = result.Value.Item2;
        return true;
    });

    private readonly TextBoxToEventPropertyBinder<RandomNumericDataValueHandler> maximumBinder = new TextBoxToEventPropertyBinder<RandomNumericDataValueHandler>(nameof(ParsingMinimumChanged), (b) => b.Model.ParsingMinimum, async (b, text) => {
        (IDataValue, string)? result = await BinderParsingUtils.TryParseTextAsDataValueAndModify(b.Model, b.Model.DataType, text);
        if (!result.HasValue) {
            return false;
        }

        b.Model.Provider.Maximum = (BaseNumericDataValue?) result.Value.Item1;
        b.Model.ParsingMaximum = result.Value.Item2;
        return true;
    });

    private string parsingMinimum, parsingMaximum;
    private bool isUpdatingProviderValues;

    public string ParsingMinimum {
        get => this.parsingMinimum;
        set => PropertyHelper.SetAndRaiseINE(ref this.parsingMinimum, value, this, static t => t.ParsingMinimumChanged?.Invoke(t));
    }

    public string ParsingMaximum {
        get => this.parsingMaximum;
        set => PropertyHelper.SetAndRaiseINE(ref this.parsingMaximum, value, this, static t => t.ParsingMaximumChanged?.Invoke(t));
    }

    public event RandomDataValueHandlerEventHandler? ParsingMinimumChanged, ParsingMaximumChanged;

    public TextBox PART_Minimum { get; }

    public TextBox PART_Maximum { get; }

    public RandomNumericDataValueHandler(TextBox partMinimum, TextBox partMaximum) {
        this.PART_Minimum = partMinimum;
        this.PART_Maximum = partMaximum;
    }

    public void UpdateTextFromMinimumValue() {
        this.ParsingMinimum = this.Provider.Minimum != null
            ? DataValueUtils.GetStringFromDataValue(this.Provider.Minimum,
                this.DataType.IsInteger() && this.ParseIntAsHex
                    ? NumericDisplayType.Hexadecimal
                    : NumericDisplayType.Normal)
            : "";
    }
    
    public void UpdateTextFromMaximumValue() {
        this.ParsingMaximum = this.Provider.Maximum != null
            ? DataValueUtils.GetStringFromDataValue(this.Provider.Maximum,
                this.DataType.IsInteger() && this.ParseIntAsHex
                    ? NumericDisplayType.Hexadecimal
                    : NumericDisplayType.Normal)
            : "";
    }

    protected override void OnConnected() {
        this.UpdateTextFromMinimumValue();
        this.UpdateTextFromMaximumValue();
        this.minimumBinder.Attach(this.PART_Minimum, this);
        this.maximumBinder.Attach(this.PART_Maximum, this);
        this.Provider.MinimumChanged += this.OnMinimumChanged;
        this.Provider.MaximumChanged += this.OnMaximumChanged;
        this.Provider.DataTypeChanged += this.OnProviderDataTypeChanged;
    }

    private void OnProviderDataTypeChanged(RandomNumberDataProvider sender) {
        BaseNumericDataValue? oldMin = sender.Minimum, oldMax = sender.Maximum;
        sender.Minimum = sender.Maximum = null;
        this.DataType = sender.DataType;
        this.isUpdatingProviderValues = true;
        sender.Minimum = (BaseNumericDataValue?) IDataValue.TryConvertDataValue(oldMin, this.DataType, default) ?? IDataValue.CreateDefaultNumeric(this.DataType);
        sender.Maximum = (BaseNumericDataValue?) IDataValue.TryConvertDataValue(oldMax, this.DataType, default) ?? IDataValue.CreateDefaultNumeric(this.DataType);
        this.isUpdatingProviderValues = false;
    }

    protected override void OnDisconnect() {
        this.minimumBinder.Detach();
        this.maximumBinder.Detach();
        this.Provider.MinimumChanged -= this.OnMinimumChanged;
        this.Provider.MaximumChanged -= this.OnMaximumChanged;
        this.Provider.DataTypeChanged -= this.OnProviderDataTypeChanged;
    }

    private void OnMinimumChanged(RandomNumberDataProvider sender) {
        this.UpdateTextFromMinimumValue();
    }

    private void OnMaximumChanged(RandomNumberDataProvider sender) {
        this.UpdateTextFromMaximumValue();
    }
}