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
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Engine.SavedAddressing;

public class AddressTableEntry : BaseAddressTableEntry {
    /// <summary>
    /// Gets or sets if this saved address is active, as in, being refreshed every so often. False disables auto-refresh
    /// </summary>
    public bool IsAutoRefreshEnabled {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsAutoRefreshEnabledChanged);
    } = true;

    /// <summary>
    /// Gets this entry's memory address. This value is never null, but may be <see cref="StaticAddress.Zero"/>
    /// </summary>
    public IMemoryAddress MemoryAddress {
        get => field;
        set {
            ArgumentNullException.ThrowIfNull(value);
            PropertyHelper.SetAndRaiseINE(ref field, value, this, this.MemoryAddressChanged);
        }
    } = StaticAddress.Zero;

    /// <summary>
    /// Gets or sets the current presented value
    /// </summary>
    public IDataValue? Value {
        get => field;
        set {
            if (!Equals(field, value)) {
                if (value != null && value.DataType != this.DataType)
                    throw new ArgumentException($"New value's data type does not match our data type: {value.DataType} != {this.DataType}");

                field = value;
                this.CurrentValueDisplayType = this.NumericDisplayType;
                this.ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets or sets the type of data this address points to
    /// </summary>
    public DataType DataType {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.DataTypeChanged);
    } = DataType.Byte;

    /// <summary>
    /// Gets or sets how to interpolate bytes at the address as a string
    /// </summary>
    public StringType StringType {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.StringTypeChanged);
    } = StringType.ASCII;

    /// <summary>
    /// Gets or sets the number of characters in the string
    /// </summary>
    public int StringLength {
        get => field;
        set {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, nameof(value) + " cannot be negative");

            PropertyHelper.SetAndRaiseINE(ref field, value, this, this.StringLengthChanged);
        }
    } = 0;

    public int ArrayLength {
        get => field;
        set {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, nameof(value) + " cannot be negative");

            PropertyHelper.SetAndRaiseINE(ref field, value, this, this.ArrayLengthChanged);
        }
    } = 0;

    /// <summary>
    /// Gets or sets how to present the value as a string
    /// </summary>
    public NumericDisplayType NumericDisplayType {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.NumericDisplayTypeChanged);
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

    public event EventHandler? IsAutoRefreshEnabledChanged;
    public event EventHandler? MemoryAddressChanged;
    public event EventHandler? ValueChanged;
    public event EventHandler? DataTypeChanged;
    public event EventHandler? StringTypeChanged;
    public event EventHandler? StringLengthChanged;
    public event EventHandler? ArrayLengthChanged;
    public event EventHandler? NumericDisplayTypeChanged;

    public AddressTableEntry() {
    }

    public AddressTableEntry(ScanResultViewModel result) {
        this.MemoryAddress = new StaticAddress(result.Address);
        this.DataType = result.DataType;
        this.Value = result.CurrentValue;
        this.NumericDisplayType = result.NumericDisplayType;
        if (this.DataType == DataType.String) {
            this.StringType = result.ScanningProcessor.StringScanOption;
            this.StringLength = ((DataValueString) this.Value).Value.Length;
        }
        else if (this.DataType == DataType.ByteArray) {
            this.ArrayLength = ((DataValueByteArray) this.Value).Value.Length;
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