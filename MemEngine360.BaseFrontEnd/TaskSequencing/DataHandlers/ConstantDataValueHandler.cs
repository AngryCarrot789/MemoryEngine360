using Avalonia.Controls;
using Avalonia.Data;
using MemEngine360.BaseFrontEnd.Utils;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.DataHandlers;

public delegate void ConstantDataValueHandlerParsingTextChangedEventHandler(ConstantDataValueHandler sender);

public class ConstantDataValueHandler : DataProviderHandler<ConstantDataProvider> {
    private readonly TextBoxToEventPropertyBinder<ConstantDataValueHandler> valueBinder = new TextBoxToEventPropertyBinder<ConstantDataValueHandler>(nameof(ParsingTextChanged), (b) => b.Model.ParsingText, async (b, text) => {
        (IDataValue, string)? result = await BinderParsingUtils.TryParseTextAsDataValueAndModify(b.Model, b.Model.DataType, text);
        if (!result.HasValue) {
            return false;
        }

        b.Model.Provider.DataValue = result.Value.Item1;
        b.Model.ParsingText = result.Value.Item2;
        return true;
    });

    private string parsingText;
    private bool isUpdatingProviderDataValue;

    public TextBox PART_Value { get; }

    public string ParsingText {
        get => this.parsingText;
        set {
            if (this.parsingText != value) {
                this.parsingText = value;
                this.ParsingTextChanged?.Invoke(this);
            }
        }
    }

    public event ConstantDataValueHandlerParsingTextChangedEventHandler? ParsingTextChanged;

    public ConstantDataValueHandler(TextBox partValue) {
        this.PART_Value = partValue;
    }

    public void UpdateTextFromProviderValue() {
        this.ParsingText = this.Provider.DataValue != null
            ? MemoryEngine360.GetStringFromDataValue(this.Provider.DataValue,
                this.DataType.IsInteger() && this.ParseIntAsHex
                    ? NumericDisplayType.Hexadecimal
                    : NumericDisplayType.Normal)
            : "";
    }


    protected override void OnConnected() {
        this.UpdateTextFromProviderValue();
        this.valueBinder.Attach(this.PART_Value, this);
        this.Provider.DataValueChanged += this.OnDataValueChanged;
        this.DataType = this.Provider.DataValue?.DataType ?? DataType.Int32;
    }

    protected override void OnDisconnect() {
        this.valueBinder.Detach();
        this.Provider.DataValueChanged -= this.OnDataValueChanged;
    }

    private void OnDataValueChanged(ConstantDataProvider sender) {
        if (!this.isUpdatingProviderDataValue) {
            this.DataType = sender.DataValue?.DataType ?? DataType.Int32;
        }
        
        this.UpdateTextFromProviderValue();
    }

    protected override void OnDataTypeChanged() {
        base.OnDataTypeChanged();
        this.TryUpdateProviderValueWithConvertedValue();
    }

    protected override void OnStringTypeChanged() {
        base.OnStringTypeChanged();
        this.TryUpdateProviderValueWithConvertedValue();
    }

    public void TryUpdateProviderValueWithConvertedValue() {
        if (this.IsConnected) {
            this.isUpdatingProviderDataValue = true;
            this.Provider.DataValue = IDataValue.TryConvertDataValue(this.Provider.DataValue, this.DataType, this.StringType) ?? IDataValue.CreateDefault(this.DataType, this.StringType);
            this.isUpdatingProviderDataValue = false;
        }
    }
}