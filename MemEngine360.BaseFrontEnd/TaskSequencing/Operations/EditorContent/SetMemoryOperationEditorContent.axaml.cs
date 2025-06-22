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
using MemEngine360.BaseFrontEnd.TaskSequencing.DataHandlers;
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.Sequencing.Operations;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.ComboBoxes;
using PFXToolKitUI.Avalonia.Bindings.Enums;
using PFXToolKitUI.PropertyEditing.DataTransfer.Enums;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Operations.EditorContent;

public partial class SetMemoryOperationEditorContent : BaseOperationEditorContent {
    private static readonly DataParameterEnumInfo<DataType> RandomDataTypeInfo = DataParameterEnumInfo<DataType>.FromAllowed([DataType.Byte, DataType.Int16, DataType.Int32, DataType.Int64, DataType.Float, DataType.Double]);
    
    private readonly IBinder<DataProviderHandler> parseIntAsHexBinder = new AvaloniaPropertyToEventPropertyBinder<DataProviderHandler>(CheckBox.IsCheckedProperty, nameof(DataProviderHandler.ParseIntAsHexChanged), (b) => ((CheckBox) b.Control).IsChecked = b.Model.ParseIntAsHex, (b) => b.Model.ParseIntAsHex = ((CheckBox) b.Control).IsChecked == true);
    private readonly EventPropertyEnumBinder<StringType> stringScanModeBinder = new EventPropertyEnumBinder<StringType>(typeof(ConstantDataValueHandler), nameof(ConstantDataValueHandler.StringTypeChanged), (x) => ((ConstantDataValueHandler) x).StringType, (x, v) => ((ConstantDataValueHandler) x).StringType = v);
    private readonly ComboBoxToEventPropertyEnumBinder<DataType> dataTypeBinder = new ComboBoxToEventPropertyEnumBinder<DataType>(typeof(DataProviderHandler), nameof(DataProviderHandler.DataTypeChanged), (x) => ((DataProviderHandler) x).DataType, (x, y) => ((DataProviderHandler) x).DataType = y);

    private readonly IBinder<SetMemoryOperation> selectedTabIndexBinder = new AvaloniaPropertyToEventPropertyBinder<SetMemoryOperation>(TabControl.SelectedIndexProperty, nameof(SetMemoryOperation.DataValueProviderChanged), (b) => {
        switch (b.Model.DataValueProvider) {
            case ConstantDataProvider:     ((TabControl) b.Control).SelectedIndex = 0; break;
            case RandomNumberDataProvider: ((TabControl) b.Control).SelectedIndex = 1; break;
            default:                       ((TabControl) b.Control).SelectedIndex = -1; break;
        }
    }, (b) => {
        switch (((TabControl) b.Control).SelectedIndex) {
            case 0:  b.Model.DataValueProvider = b.Model.InitialConstantDataProvider ??= new ConstantDataProvider(new DataValueInt32(0)); break;
            case 1:  b.Model.DataValueProvider = b.Model.InitialRandomNumberDataProvider ??= new RandomNumberDataProvider(DataType.Int32, new DataValueInt32(0), new DataValueInt32(10)); break;
            default: b.Model.DataValueProvider = null; break;
        }
    });

    private DataProviderHandler? myDataProviderEditorHandler;

    public override string Caption => "Set Memory";

    public SetMemoryOperationEditorContent() {
        this.InitializeComponent();

        this.parseIntAsHexBinder.AttachControl(this.PART_DisplayAndParseIntAsHex);
        this.stringScanModeBinder.Assign(this.PART_DTString_ASCII, StringType.ASCII);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF8, StringType.UTF8);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF16, StringType.UTF16);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF32, StringType.UTF32);
        this.selectedTabIndexBinder.AttachControl(this.PART_ModeTabControl);
    }

    protected override void OnOperationChanged(BaseSequenceOperation? oldOperation, BaseSequenceOperation? newOperation) {
        base.OnOperationChanged(oldOperation, newOperation);

        if (oldOperation is SetMemoryOperation oldOp) {
            oldOp.DataValueProviderChanged -= this.OnDataValueProviderChanged;
            if (oldOp.DataValueProvider != null)
                this.OnDataValueProviderChanged(oldOp, oldOp.DataValueProvider, null);
        }

        if (newOperation is SetMemoryOperation newOp) {
            newOp.DataValueProviderChanged += this.OnDataValueProviderChanged;
            if (newOp.DataValueProvider != null)
                this.OnDataValueProviderChanged(newOp, null, newOp.DataValueProvider);
        }

        this.selectedTabIndexBinder.SwitchModel(newOperation as SetMemoryOperation);
    }

    private void OnDataValueProviderChanged(SetMemoryOperation sender, DataValueProvider? oldProvider, DataValueProvider? newProvider) {
        if (this.myDataProviderEditorHandler != null) {
            this.dataTypeBinder.Detach();
            this.parseIntAsHexBinder.DetachModel();
            if (this.myDataProviderEditorHandler is ConstantDataValueHandler)
                this.stringScanModeBinder.Detach();
            
            this.myDataProviderEditorHandler.Disconnect();
            this.myDataProviderEditorHandler = null;
        }

        if (newProvider != null) {
            switch (newProvider) {
                case ConstantDataProvider:     this.myDataProviderEditorHandler = new ConstantDataValueHandler(this.PART_ConstantValueTextBox); break;
                case RandomNumberDataProvider: this.myDataProviderEditorHandler = new RandomNumericDataValueHandler(this.PART_RangedValueATextBox, this.PART_RangedValueBTextBox); break;
            }

            if (this.myDataProviderEditorHandler != null) {
                this.myDataProviderEditorHandler.Connect(newProvider);
                
                if (newProvider is ConstantDataProvider)
                    this.dataTypeBinder.Attach(this.PART_DataTypeCombo, this.myDataProviderEditorHandler);
                else if (newProvider is RandomNumberDataProvider)
                    this.dataTypeBinder.Attach(this.PART_DataTypeCombo, this.myDataProviderEditorHandler, RandomDataTypeInfo);
                
                this.parseIntAsHexBinder.AttachModel(this.myDataProviderEditorHandler);
                if (this.myDataProviderEditorHandler is ConstantDataValueHandler)
                    this.stringScanModeBinder.Attach(this.myDataProviderEditorHandler);
            }
        }
    }
}