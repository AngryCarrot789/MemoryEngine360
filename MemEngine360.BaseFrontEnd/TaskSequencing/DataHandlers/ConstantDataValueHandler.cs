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
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.DataHandlers;

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

    private bool isUpdatingProviderDataValue;

    public TextBox ValueTextBox { get; }

    public string ParsingText {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.ParsingTextChanged);
    } = "";

    /// <summary>
    /// Gets or sets the encoding used to encode/decode strings/bytes
    /// </summary>
    public StringType StringType {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.OnStringTypeChanged();
            }
        }
    }

    public event EventHandler? ParsingTextChanged;
    public event EventHandler? StringTypeChanged;

    public ConstantDataValueHandler(TextBox valueTextBox) {
        this.ValueTextBox = valueTextBox;
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
        this.valueBinder.Attach(this.ValueTextBox, this);
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

    private void OnProviderDataValueChanged(object? o, EventArgs eventArgs) {
        if (!this.isUpdatingProviderDataValue) {
            this.DataType = ((ConstantDataProvider) o!).DataValue?.DataType ?? DataType.Int32;
            this.UpdateTextFromProviderValue();
        }
    }

    private void OnProviderParseIntAsHexChanged(object? o, EventArgs eventArgs) {
        this.ParseIntAsHex = ((ConstantDataProvider) o!).ParseIntAsHex;
        if (!this.isUpdatingProviderDataValue)
            this.UpdateTextFromProviderValue();
    }

    private void OnProviderStringTypeChanged(object? o, EventArgs eventArgs) {
        this.StringType = ((ConstantDataProvider) o!).StringType;
        if (!this.isUpdatingProviderDataValue)
            this.UpdateTextFromProviderValue();
    }
    
    private void OnProviderDataTypeChanged(object? o, EventArgs eventArgs) {
        this.DataType = this.Provider.DataType;
        this.TryUpdateProviderValueWithConvertedValue();
    }

    protected override void OnDataTypeChanged() {
        base.OnDataTypeChanged();
        this.Provider.DataType = this.DataType;
    }

    protected void OnStringTypeChanged() {
        this.StringTypeChanged?.Invoke(this, EventArgs.Empty);
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