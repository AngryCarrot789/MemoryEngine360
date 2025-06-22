﻿// 
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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using MemEngine360.Commands.ATM;
using MemEngine360.Engine.Modes;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.ComboBoxes;
using PFXToolKitUI.Avalonia.Bindings.Enums;
using PFXToolKitUI.Avalonia.Services.UserInputs;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.BaseFrontEnd.Services;

public partial class SavedResultDataTypeEditorUserInputControl : UserControl, IUserInputContent {
    public static readonly StyledProperty<int> StringLengthProperty = AvaloniaProperty.Register<SavedResultDataTypeEditorUserInputControl, int>(nameof(StringLength));
    public static readonly StyledProperty<int> ArrayLengthProperty = AvaloniaProperty.Register<SavedResultDataTypeEditorUserInputControl, int>(nameof(ArrayLength));

    private UserInputDialogView? myDialog;
    private SavedResultDataTypeUserInputInfo? myData;
    private DataType lastNumericDataType;
    private readonly ComboBoxToEventPropertyEnumBinder<DataType> dataTypeBinder = new ComboBoxToEventPropertyEnumBinder<DataType>(typeof(SavedResultDataTypeUserInputInfo), nameof(SavedResultDataTypeUserInputInfo.DataTypeChanged), (x) => ((SavedResultDataTypeUserInputInfo) x).DataType, (x, y) => ((SavedResultDataTypeUserInputInfo) x).DataType = y);

    private readonly IBinder<SavedResultDataTypeUserInputInfo> displayAsHexBinder = new AvaloniaPropertyToEventPropertyBinder<SavedResultDataTypeUserInputInfo>(CheckBox.IsCheckedProperty, nameof(SavedResultDataTypeUserInputInfo.DisplayAsHexChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.DisplayAsHex, (b) => b.Model.DisplayAsHex = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<SavedResultDataTypeUserInputInfo> displayAsUnsignedBinder = new AvaloniaPropertyToEventPropertyBinder<SavedResultDataTypeUserInputInfo>(CheckBox.IsCheckedProperty, nameof(SavedResultDataTypeUserInputInfo.DisplayAsUnsignedChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.DisplayAsUnsigned, (b) => b.Model.DisplayAsUnsigned = ((ToggleButton) b.Control).IsChecked == true);
    private readonly EventPropertyEnumBinder<StringType> stringScanModeBinder = new EventPropertyEnumBinder<StringType>(typeof(SavedResultDataTypeUserInputInfo), nameof(SavedResultDataTypeUserInputInfo.StringScanOptionChanged), (x) => ((SavedResultDataTypeUserInputInfo) x).StringScanOption, (x, v) => ((SavedResultDataTypeUserInputInfo) x).StringScanOption = v);
    private readonly IBinder<SavedResultDataTypeUserInputInfo> selectedTabIndexBinder;

    public int StringLength {
        get => this.GetValue(StringLengthProperty);
        set => this.SetValue(StringLengthProperty, value);
    }

    public int ArrayLength {
        get => this.GetValue(ArrayLengthProperty);
        set => this.SetValue(ArrayLengthProperty, value);
    }

    public SavedResultDataTypeEditorUserInputControl() {
        this.InitializeComponent();

        this.stringScanModeBinder.Assign(this.PART_String_ASCII, StringType.ASCII);
        this.stringScanModeBinder.Assign(this.PART_String_UTF8, StringType.UTF8);
        this.stringScanModeBinder.Assign(this.PART_String_UTF16, StringType.UTF16);
        this.stringScanModeBinder.Assign(this.PART_String_UTF32, StringType.UTF32);
        this.selectedTabIndexBinder = new AvaloniaPropertyToEventPropertyBinder<SavedResultDataTypeUserInputInfo>(TabControl.SelectedIndexProperty, nameof(SavedResultDataTypeUserInputInfo.DataTypeChanged), (b) => {
            switch (b.Model.DataType) {
                case DataType.Byte:
                case DataType.Int16:
                case DataType.Int32:
                case DataType.Int64:
                case DataType.Float:
                case DataType.Double:
                    ((TabControl) b.Control).SelectedIndex = 0;
                    break;
                case DataType.String:    ((TabControl) b.Control).SelectedIndex = 1; break;
                case DataType.ByteArray: ((TabControl) b.Control).SelectedIndex = 2; break;
                default:                 throw new ArgumentOutOfRangeException();
            }
        }, (b) => {
            switch (((TabControl) b.Control).SelectedIndex) {
                case 0: b.Model.DataType = this.lastNumericDataType; break;
                case 1: b.Model.DataType = DataType.String; break;
                case 2: b.Model.DataType = DataType.ByteArray; break;
            }
        });
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (this.myData != null) {
            if (change.Property == StringLengthProperty) {
                this.myData.StringLength = ((AvaloniaPropertyChangedEventArgs<int>) change).NewValue.GetValueOrDefault();
            }
            else if (change.Property == ArrayLengthProperty) {
                this.myData.ArrayLength = ((AvaloniaPropertyChangedEventArgs<int>) change).NewValue.GetValueOrDefault();
            }
        }
    }

    public void Connect(UserInputDialogView dialog, UserInputInfo info) {
        this.myDialog = dialog;
        this.myData = (SavedResultDataTypeUserInputInfo) info;
        this.lastNumericDataType = this.myData.DataType;
        this.myData.DataTypeChanged += this.MyDataOnDataTypeChanged;
        this.myData.StringLengthChanged += this.MyDataOnStringLengthChanged;
        this.myData.ArrayLengthChanged += this.MyDataOnArrayLengthChanged;
        this.selectedTabIndexBinder.Attach(this.PART_TabControl, this.myData);
        this.dataTypeBinder.Attach(this.PART_DataTypeComboBox, this.myData);
        this.displayAsHexBinder.Attach(this.PART_DisplayAsHex, this.myData);
        this.displayAsUnsignedBinder.Attach(this.PART_DisplayAsUnsigned, this.myData);
        this.stringScanModeBinder.Attach(this.myData);
        this.StringLength = this.myData.StringLength;
        this.ArrayLength = this.myData.ArrayLength;
    }

    private void MyDataOnDataTypeChanged(SavedResultDataTypeUserInputInfo sender) {
        if (sender.DataType.IsNumeric()) {
            this.lastNumericDataType = sender.DataType;
        }
    }

    private void MyDataOnStringLengthChanged(SavedResultDataTypeUserInputInfo sender) => this.StringLength = sender.StringLength;

    private void MyDataOnArrayLengthChanged(SavedResultDataTypeUserInputInfo sender) => this.ArrayLength = sender.ArrayLength;

    public void Disconnect() {
        this.myData!.StringLengthChanged -= this.MyDataOnStringLengthChanged;
        this.myData!.ArrayLengthChanged -= this.MyDataOnArrayLengthChanged;
        this.myData!.DataTypeChanged -= this.MyDataOnDataTypeChanged;
        this.selectedTabIndexBinder.Detach();
        this.dataTypeBinder.Detach();
        this.displayAsHexBinder.Detach();
        this.displayAsUnsignedBinder.Detach();
        this.stringScanModeBinder.Detach();

        this.myDialog = null;
        this.myData = null;
    }

    public bool FocusPrimaryInput() {
        this.PART_DataTypeComboBox.Focus();
        return true;
    }

    public void OnWindowOpened() {
    }

    public void OnWindowClosed() {
    }
}