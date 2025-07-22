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
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;
using PFXToolKitUI.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.DataHandlers;

public delegate void ConstantDataValueHandlerParsingTextChangedEventHandler(ConstantDataValueHandler sender);

public class ConstantDataValueHandler : DataProviderHandler<ConstantDataProvider> {
    private readonly TextBoxToEventPropertyBinder<ConstantDataValueHandler> valueBinder = new TextBoxToEventPropertyBinder<ConstantDataValueHandler>(nameof(ParsingTextChanged), (b) => b.Model.ParsingText, async (b, text) => {
        b.Model.isUpdatingProviderDataValue = true;
        IDataValue? result = await BinderParsingUtils.TryParseTextAsDataValueAndModify(text, b.Model.DataType, b.Model);
        if (result != null) {
            b.Model.Provider.DataValue = result;
        }

        b.Model.isUpdatingProviderDataValue = false;
        b.Model.UpdateTextFromProviderValue();
        return result != null;
    });

    private string parsingText;
    private bool isUpdatingProviderDataValue;
    private StringType stringType;

    public TextBox PART_Value { get; }

    public string ParsingText {
        get => this.parsingText;
        set => PropertyHelper.SetAndRaiseINE(ref this.parsingText, value, this, static t => t.ParsingTextChanged?.Invoke(t));
    }
    
    /// <summary>
    /// Gets or sets the encoding used to encode/decode strings/bytes
    /// </summary>
    public StringType StringType {
        get => this.stringType;
        set {
            if (this.stringType != value) {
                this.stringType = value;
                this.OnStringTypeChanged();
            }
        }
    }

    public event ConstantDataValueHandlerParsingTextChangedEventHandler? ParsingTextChanged;
    public event ConstantDataValueHandlerParsingTextChangedEventHandler? StringTypeChanged;

    public ConstantDataValueHandler(TextBox partValue) {
        this.PART_Value = partValue;
    }
    
    public void UpdateTextFromProviderValue() {
        this.ParsingText = this.Provider.DataValue != null
            ? DataValueUtils.GetStringFromDataValue(this.Provider.DataValue,
                this.DataType.IsInteger() && this.ParseIntAsHex
                    ? NumericDisplayType.Hexadecimal
                    : NumericDisplayType.Normal)
            : "";
    }


    protected override void OnConnected() {
        this.ParseIntAsHex = this.Provider.ParseIntAsHex;
        this.StringType = this.Provider.StringType;
        this.DataType = this.Provider.DataValue?.DataType ?? DataType.Int32;
        this.UpdateTextFromProviderValue();
        this.valueBinder.Attach(this.PART_Value, this);
        this.Provider.DataValueChanged += this.OnProviderDataValueChanged;
        this.Provider.DataTypeChanged += this.OnProviderDataTypeChanged;
        this.Provider.ParseIntAsHexChanged += this.OnProviderParseIntAsHexChanged;
        this.Provider.StringTypeChanged += this.OnProviderStringTypeChanged;
    }

    protected override void OnDisconnect() {
        this.valueBinder.Detach();
        this.Provider.DataValueChanged -= this.OnProviderDataValueChanged;
        this.Provider.DataTypeChanged -= this.OnProviderDataTypeChanged;
        this.Provider.ParseIntAsHexChanged -= this.OnProviderParseIntAsHexChanged;
        this.Provider.StringTypeChanged -= this.OnProviderStringTypeChanged;
    }

    private void OnProviderDataValueChanged(ConstantDataProvider sender) {
        if (!this.isUpdatingProviderDataValue) {
            this.DataType = sender.DataValue?.DataType ?? DataType.Int32;
            this.UpdateTextFromProviderValue();
        }
    }

    private void OnProviderParseIntAsHexChanged(ConstantDataProvider sender) {
        this.ParseIntAsHex = sender.ParseIntAsHex;
        if (!this.isUpdatingProviderDataValue)
            this.UpdateTextFromProviderValue();
    }

    private void OnProviderStringTypeChanged(ConstantDataProvider sender) {
        this.StringType = sender.StringType;
        if (!this.isUpdatingProviderDataValue)
            this.UpdateTextFromProviderValue();
    }
    
    private void OnProviderDataTypeChanged(ConstantDataProvider sender) {
        this.DataType = this.Provider.DataType;
        this.TryUpdateProviderValueWithConvertedValue();
    }

    protected override void OnDataTypeChanged() {
        base.OnDataTypeChanged();
        this.Provider.DataType = this.DataType;
    }

    protected void OnStringTypeChanged() {
        this.StringTypeChanged?.Invoke(this);
        this.TryUpdateProviderValueWithConvertedValue();
    }
    
    protected override void OnParseIntAsHexChanged() {
        base.OnParseIntAsHexChanged();
        if (this.IsConnected) {
            this.Provider.ParseIntAsHex = this.ParseIntAsHex;
        }
    }

    public void TryUpdateProviderValueWithConvertedValue() {
        if (this.isUpdatingProviderDataValue) {
            return;
        }
        
        if (this.IsConnected) {
            lock (this.Provider.Lock) {
                this.isUpdatingProviderDataValue = true;
                this.Provider.DataValue = IDataValue.TryConvertDataValue(this.Provider.DataValue, this.DataType, this.StringType) ?? IDataValue.CreateDefault(this.DataType, this.StringType);
                this.isUpdatingProviderDataValue = false;

                this.UpdateTextFromProviderValue();
            }
        }
    }
}