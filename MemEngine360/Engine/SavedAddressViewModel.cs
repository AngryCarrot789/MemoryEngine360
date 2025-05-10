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

using MemEngine360.Engine.Modes;
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils.Accessing;

namespace MemEngine360.Engine;

public sealed class SavedAddressViewModel : BaseTransferableViewModel {
    // This class needs a re-work. We shouldn't use a raw address like this,
    // since cheat engine doesn't appear to do that (since you have use base address + a list of offsets)

    public static readonly DataKey<SavedAddressViewModel> DataKey = DataKey<SavedAddressViewModel>.Create("SavedAddressViewModel");

    public static readonly DataParameterBool IsAutoRefreshEnabledParameter = DataParameter.Register(new DataParameterBool(typeof(SavedAddressViewModel), nameof(IsAutoRefreshEnabled), true, ValueAccessors.Reflective<bool>(typeof(SavedAddressViewModel), nameof(isAutoRefreshEnabled))));
    public static readonly DataParameter<uint> AddressParameter = DataParameter.Register(new DataParameter<uint>(typeof(SavedAddressViewModel), nameof(Address), 0, ValueAccessors.Reflective<uint>(typeof(SavedAddressViewModel), nameof(address))));
    public static readonly DataParameter<string> ValueParameter = DataParameter.Register(new DataParameter<string>(typeof(SavedAddressViewModel), nameof(Value), default, ValueAccessors.Reflective<string>(typeof(SavedAddressViewModel), nameof(value))));
    public static readonly DataParameter<string> DescriptionParameter = DataParameter.Register(new DataParameter<string>(typeof(SavedAddressViewModel), nameof(Description), default, ValueAccessors.Reflective<string>(typeof(SavedAddressViewModel), nameof(description))));
    public static readonly DataParameter<DataType> DataTypeParameter = DataParameter.Register(new DataParameter<DataType>(typeof(SavedAddressViewModel), nameof(DataType), default, ValueAccessors.Reflective<DataType>(typeof(SavedAddressViewModel), nameof(dataType))));
    public static readonly DataParameter<StringType> StringTypeParameter = DataParameter.Register(new DataParameter<StringType>(typeof(SavedAddressViewModel), nameof(StringType), default, ValueAccessors.Reflective<StringType>(typeof(SavedAddressViewModel), nameof(stringType))));
    public static readonly DataParameter<uint> StringLengthParameter = DataParameter.Register(new DataParameter<uint>(typeof(SavedAddressViewModel), nameof(StringLength), default, ValueAccessors.Reflective<uint>(typeof(SavedAddressViewModel), nameof(stringLength))));
    public static readonly DataParameter<NumericDisplayType> NumericDisplayTypeParameter = DataParameter.Register(new DataParameter<NumericDisplayType>(typeof(SavedAddressViewModel), nameof(NumericDisplayType), default, ValueAccessors.Reflective<NumericDisplayType>(typeof(SavedAddressViewModel), nameof(numericDisplayType))));

    private bool isAutoRefreshEnabled;
    private uint address;
    private string value = "", description = "";
    private DataType dataType = DataType.Byte;
    private StringType stringType = StringType.UTF8;
    private uint stringLength = 0;
    private NumericDisplayType numericDisplayType;

    /// <summary>
    /// Gets or sets if this saved address is active, as in, being refreshed every so often. False disables auto-refresh
    /// </summary>
    public bool IsAutoRefreshEnabled {
        get => this.isAutoRefreshEnabled;
        set => DataParameter.SetValueHelper(this, IsAutoRefreshEnabledParameter, ref this.isAutoRefreshEnabled, value);
    }
    
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
    /// Gets or sets how to present the value as a string
    /// </summary>
    public NumericDisplayType NumericDisplayType {
        get => this.numericDisplayType;
        set => DataParameter.SetValueHelper(this, NumericDisplayTypeParameter, ref this.numericDisplayType, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="Engine.NumericDisplayType"/> that was specified when <see cref="Value"/> changed
    /// </summary>
    public NumericDisplayType CurrentValueDisplayType { get; private set; }

    /// <summary>
    /// Gets the scanning processor that owns this saved address
    /// </summary>
    public ScanningProcessor ScanningProcessor { get; set; }

    /// <summary>
    /// Gets or sets if this object is currently visible in the results
    /// list in the mem engine view. This is a filthy workaround 
    /// </summary>
    public bool IsVisibleInMainSavedResultList { get; set; } = true;

    public SavedAddressViewModel(ScanningProcessor scanningProcessor, uint address) {
        this.isAutoRefreshEnabled = IsAutoRefreshEnabledParameter.GetDefaultValue(this);
        this.ScanningProcessor = scanningProcessor;
        this.address = address;
    }

    public SavedAddressViewModel(ScanResultViewModel result) {
        this.isAutoRefreshEnabled = IsAutoRefreshEnabledParameter.GetDefaultValue(this);
        this.ScanningProcessor = result.ScanningProcessor;
        this.address = result.Address;
        this.dataType = result.DataType;
        this.value = result.CurrentValue;
        this.numericDisplayType = result.NumericDisplayType;
        if (this.dataType == DataType.String) {
            this.stringType = this.ScanningProcessor.StringScanOption;
            this.stringLength = (uint) this.value.Length;
        }
    }

    static SavedAddressViewModel() {
        ValueParameter.PriorityValueChanged += (parameter, owner) => {
            SavedAddressViewModel obj = (SavedAddressViewModel) owner;
            obj.CurrentValueDisplayType = obj.NumericDisplayType;
        };
        
        RegisterParametersAsObservable(AddressParameter, ValueParameter, DescriptionParameter, DataTypeParameter, StringTypeParameter, StringLengthParameter, NumericDisplayTypeParameter);
    }
}