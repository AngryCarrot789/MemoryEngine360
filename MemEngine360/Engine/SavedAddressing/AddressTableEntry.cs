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

using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.Modes;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.SavedAddressing;

public delegate void AddressTableEntryEventHandler(AddressTableEntry sender);

public delegate void AddressTableEntryMemoryAddressChangedEventHandler(AddressTableEntry sender, IMemoryAddress oldMemoryAddress, IMemoryAddress newMemoryAddress);

public class AddressTableEntry : BaseAddressTableEntry {
    private bool isAutoRefreshEnabled = true;
    private IMemoryAddress memoryAddress;
    private IDataValue? value;
    private DataType dataType = DataType.Byte;
    private StringType stringType = StringType.ASCII;
    private int stringLength = 0, arrayLength = 0;
    private NumericDisplayType numericDisplayType;

    /// <summary>
    /// Gets or sets if this saved address is active, as in, being refreshed every so often. False disables auto-refresh
    /// </summary>
    public bool IsAutoRefreshEnabled {
        get => this.isAutoRefreshEnabled;
        set => PropertyHelper.SetAndRaiseINE(ref this.isAutoRefreshEnabled, value, this, static t => t.IsAutoRefreshEnabledChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets this entry's memory address. This value is never null, but may be <see cref="StaticAddress.Zero"/>
    /// </summary>
    public IMemoryAddress MemoryAddress {
        get => this.memoryAddress;
        set {
            ArgumentNullException.ThrowIfNull(value);
            PropertyHelper.SetAndRaiseINE(ref this.memoryAddress, value, this, static (t, o, n) => t.MemoryAddressChanged?.Invoke(t, o, n));
        }
    }

    /// <summary>
    /// Gets or sets the current presented value
    /// </summary>
    public IDataValue? Value {
        get => this.value;
        set {
            if (!Equals(this.value, value)) {
                if (value != null && value.DataType != this.DataType)
                    throw new ArgumentException($"New value's data type does not match our data type: {value.DataType} != {this.DataType}");

                this.value = value;
                this.CurrentValueDisplayType = this.NumericDisplayType;
                this.ValueChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets the type of data this address points to
    /// </summary>
    public DataType DataType {
        get => this.dataType;
        set => PropertyHelper.SetAndRaiseINE(ref this.dataType, value, this, static t => t.DataTypeChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets how to interpolate bytes at the address as a string
    /// </summary>
    public StringType StringType {
        get => this.stringType;
        set => PropertyHelper.SetAndRaiseINE(ref this.stringType, value, this, static t => t.StringTypeChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the number of characters in the string
    /// </summary>
    public int StringLength {
        get => this.stringLength;
        set {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, nameof(value) + " cannot be negative");

            PropertyHelper.SetAndRaiseINE(ref this.stringLength, value, this, static t => t.StringLengthChanged?.Invoke(t));
        }
    }

    public int ArrayLength {
        get => this.arrayLength;
        set {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, nameof(value) + " cannot be negative");

            PropertyHelper.SetAndRaiseINE(ref this.arrayLength, value, this, static t => t.ArrayLengthChanged?.Invoke(t));
        }
    }

    /// <summary>
    /// Gets or sets how to present the value as a string
    /// </summary>
    public NumericDisplayType NumericDisplayType {
        get => this.numericDisplayType;
        set => PropertyHelper.SetAndRaiseINE(ref this.numericDisplayType, value, this, static t => t.NumericDisplayTypeChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the <see cref="Engine.NumericDisplayType"/> that was specified when <see cref="Value"/> changed
    /// </summary>
    public NumericDisplayType CurrentValueDisplayType { get; private set; }

    /// <summary>
    /// Gets or sets if this object is currently visible in the results
    /// list in the mem engine view. This is a filthy workaround 
    /// </summary>
    public bool IsVisibleInMainSavedResultList { get; set; } = true;

    public event AddressTableEntryEventHandler? IsAutoRefreshEnabledChanged;
    public event AddressTableEntryMemoryAddressChangedEventHandler? MemoryAddressChanged;
    public event AddressTableEntryEventHandler? ValueChanged;
    public event AddressTableEntryEventHandler? DataTypeChanged;
    public event AddressTableEntryEventHandler? StringTypeChanged;
    public event AddressTableEntryEventHandler? StringLengthChanged;
    public event AddressTableEntryEventHandler? ArrayLengthChanged;
    public event AddressTableEntryEventHandler? NumericDisplayTypeChanged;

    public AddressTableEntry() {
    }

    public AddressTableEntry(ScanResultViewModel result) {
        this.MemoryAddress = new StaticAddress(result.Address);
        this.dataType = result.DataType;
        this.value = result.CurrentValue;
        this.numericDisplayType = result.NumericDisplayType;
        if (this.dataType == DataType.String) {
            this.stringType = result.ScanningProcessor.StringScanOption;
            this.stringLength = ((DataValueString) this.value).Value.Length;
        }
        else if (this.dataType == DataType.ByteArray) {
            this.ArrayLength = ((DataValueByteArray) this.value).Value.Length;
        }
    }

    public override BaseAddressTableEntry CreateClone() {
        return new AddressTableEntry() {
            Description = this.Description,
            MemoryAddress = this.MemoryAddress,
            DataType = this.DataType,
            StringType = this.StringType,
            StringLength = this.StringLength,
            ArrayLength = this.ArrayLength,
            IsAutoRefreshEnabled = this.IsAutoRefreshEnabled,
            NumericDisplayType = this.NumericDisplayType,
            Value = this.Value,
        };
    }
}