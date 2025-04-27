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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using MemEngine360.Engine.Modes;
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils.Accessing;

namespace MemEngine360.Engine;

public sealed class SavedAddressViewModel : ITransferableData, INotifyPropertyChanged {
    // This class needs a re-work. We shouldn't use a raw address like this,
    // since cheat engine doesn't appear to do that (since you have use base address + a list of offsets)

    public static readonly DataKey<SavedAddressViewModel> DataKey = DataKey<SavedAddressViewModel>.Create("SavedAddressViewModel");

    public static readonly DataParameter<uint> AddressParameter = DataParameter.Register(new DataParameter<uint>(typeof(SavedAddressViewModel), nameof(Address), 0, ValueAccessors.Reflective<uint>(typeof(SavedAddressViewModel), nameof(address))));
    public static readonly DataParameter<string> ValueParameter = DataParameter.Register(new DataParameter<string>(typeof(SavedAddressViewModel), nameof(Value), default, ValueAccessors.Reflective<string>(typeof(SavedAddressViewModel), nameof(value))));
    public static readonly DataParameter<string> DescriptionParameter = DataParameter.Register(new DataParameter<string>(typeof(SavedAddressViewModel), nameof(Description), default, ValueAccessors.Reflective<string>(typeof(SavedAddressViewModel), nameof(description))));
    public static readonly DataParameter<DataType> DataTypeParameter = DataParameter.Register(new DataParameter<DataType>(typeof(SavedAddressViewModel), nameof(DataType), default, ValueAccessors.Reflective<DataType>(typeof(SavedAddressViewModel), nameof(dataType))));
    public static readonly DataParameter<StringType> StringTypeParameter = DataParameter.Register(new DataParameter<StringType>(typeof(SavedAddressViewModel), nameof(StringType), default, ValueAccessors.Reflective<StringType>(typeof(SavedAddressViewModel), nameof(stringType))));
    public static readonly DataParameter<uint> StringLengthParameter = DataParameter.Register(new DataParameter<uint>(typeof(SavedAddressViewModel), nameof(StringLength), default, ValueAccessors.Reflective<uint>(typeof(SavedAddressViewModel), nameof(stringLength))));
    public static readonly DataParameter<bool> DisplayAsHexParameter = DataParameter.Register(new DataParameter<bool>(typeof(SavedAddressViewModel), nameof(DisplayAsHex), default, ValueAccessors.Reflective<bool>(typeof(SavedAddressViewModel), nameof(hex))));
    public static readonly DataParameter<bool> DisplayAsUnsignedParameter = DataParameter.Register(new DataParameter<bool>(typeof(SavedAddressViewModel), nameof(DisplayAsUnsigned), default, ValueAccessors.Reflective<bool>(typeof(SavedAddressViewModel), nameof(unsigned))));

    private uint address;
    private string value = "", description = "";
    private DataType dataType = DataType.Byte;
    private StringType stringType = StringType.UTF8;
    private uint stringLength = 0;
    private bool hex, unsigned;

    public uint Address {
        get => this.address;
        set => DataParameter.SetValueHelper(this, AddressParameter, ref this.address, value);
    }

    /// <summary>
    /// Gets or sets the current presented value
    /// </summary>
    public string Value {
        get => this.value;
        set => DataParameter.SetValueHelper(this, ValueParameter, ref this.value, value);
    }

    /// <summary>
    /// Gets or sets the description for this saved address
    /// </summary>
    public string Description {
        get => this.description;
        set => DataParameter.SetValueHelper(this, DescriptionParameter, ref this.description, value);
    }

    /// <summary>
    /// Gets or sets the type of data this address points to
    /// </summary>
    public DataType DataType {
        get => this.dataType;
        set => DataParameter.SetValueHelper(this, DataTypeParameter, ref this.dataType, value);
    }

    /// <summary>
    /// Gets or sets how to interpolate bytes at the address as a string
    /// </summary>
    public StringType StringType {
        get => this.stringType;
        set => DataParameter.SetValueHelper(this, StringTypeParameter, ref this.stringType, value);
    }

    /// <summary>
    /// Gets or sets the number of characters in the string
    /// </summary>
    public uint StringLength {
        get => this.stringLength;
        set => DataParameter.SetValueHelper(this, StringLengthParameter, ref this.stringLength, value);
    }

    /// <summary>
    /// Gets or sets if we should display the value as hexidecimal. Takes priority over <see cref="DisplayAsUnsigned"/>
    /// </summary>
    public bool DisplayAsHex {
        get => this.hex;
        set => DataParameter.SetValueHelper(this, DisplayAsHexParameter, ref this.hex, value);
    }

    /// <summary>
    /// Gets or sets if the integer value is displayed as unsigned. Does nothing for <see cref="Modes.DataType.Byte"/>
    /// </summary>
    public bool DisplayAsUnsigned {
        get => this.unsigned;
        set => DataParameter.SetValueHelper(this, DisplayAsUnsignedParameter, ref this.unsigned, value);
    }

    /// <summary>
    /// Gets the scanning processor that owns this saved address
    /// </summary>
    public ScanningProcessor ScanningProcessor { get; set; }

    /// <summary>
    /// Gets the numeric display type based on <see cref="DisplayAsHex"/> and <see cref="DisplayAsUnsigned"/>
    /// </summary>
    public NumericDisplayType NumericDisplayType {
        get {
            if (this.DisplayAsHex)
                return NumericDisplayType.Hexadecimal;
            if (this.DisplayAsUnsigned)
                return NumericDisplayType.Unsigned;
            return NumericDisplayType.Normal;
        }
    }

    /// <summary>
    /// Gets or sets if this object is currently visible in the results
    /// list in the mem engine view. This is a filthy workaround 
    /// </summary>
    public bool IsVisibleInMainSavedResultList { get; set; } = true;

    public TransferableData TransferableData { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SavedAddressViewModel(ScanningProcessor scanningProcessor, uint address) {
        this.TransferableData = new TransferableData(this);
        this.ScanningProcessor = scanningProcessor;
        this.address = address;
    }

    public SavedAddressViewModel(ScanResultViewModel result) {
        this.TransferableData = new TransferableData(this);
        this.ScanningProcessor = result.ScanningProcessor;
        this.address = result.Address;
        this.dataType = result.DataType;
        this.value = result.CurrentValue;
        if (this.dataType == DataType.String) {
            this.stringType = this.ScanningProcessor.StringScanOption;
            this.stringLength = (uint) this.value.Length;
        }

        switch (result.NumericDisplayType) {
            case NumericDisplayType.Normal:      break;
            case NumericDisplayType.Unsigned:    this.unsigned = true; break;
            case NumericDisplayType.Hexadecimal: this.hex = true; break;
            default:                                             throw new ArgumentOutOfRangeException();
        }
    }

    static SavedAddressViewModel() {
        // Translate DataParmeter value changes into INPC
        DataParameter.AddMultipleHandlers(OnObservablePropertyChanged, AddressParameter, ValueParameter, DescriptionParameter, DataTypeParameter, StringTypeParameter, StringLengthParameter, DisplayAsHexParameter, DisplayAsUnsignedParameter);
    }

    private static void OnObservablePropertyChanged(DataParameter parameter, ITransferableData owner) {
        ((SavedAddressViewModel) owner).RaisePropertyChanged(parameter.Name);
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null) {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}