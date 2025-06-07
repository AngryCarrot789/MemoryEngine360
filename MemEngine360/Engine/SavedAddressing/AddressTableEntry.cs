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
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.SavedAddressing;

public delegate void AddressTableEntryEventHandler(AddressTableEntry sender);

public class AddressTableEntry : BaseAddressTableEntry {
    private bool isAutoRefreshEnabled = true;
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
    /// Gets or sets this entry's address. May be relative to parent, so use <see cref="AbsoluteAddress"/>
    /// </summary>
    public uint Address { get; private set; }

    /// <summary>
    /// A helper property to resolve the absolute address based on <see cref="IsAddressAbsolute"/> and our parent's address
    /// </summary>
    public uint AbsoluteAddress => this.IsAddressAbsolute ? this.Address : ((this.Parent?.AbsoluteAddress ?? 0) + this.Address);

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
    /// Gets or sets if <see cref="AbsoluteAddress"/> should return <see cref="Address"/> or add our
    /// parent's <see cref="AddressTableGroupEntry.AbsoluteAddress"/> to it
    /// </summary>
    public bool IsAddressAbsolute { get; private set; } = true;

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

    public event AddressTableEntryEventHandler? IsAutoRefreshEnabledChanged;
    public event AddressTableEntryEventHandler? AddressChanged;
    public event AddressTableEntryEventHandler? ValueChanged;
    public event AddressTableEntryEventHandler? DataTypeChanged;
    public event AddressTableEntryEventHandler? StringTypeChanged;
    public event AddressTableEntryEventHandler? StringLengthChanged;
    public event AddressTableEntryEventHandler? ArrayLengthChanged;
    public event AddressTableEntryEventHandler? NumericDisplayTypeChanged;

    public AddressTableEntry(ScanningProcessor scanningProcessor, uint address, bool isAddressAbsolute = true) {
        this.ScanningProcessor = scanningProcessor;
        this.Address = address;
        this.IsAddressAbsolute = isAddressAbsolute;
    }

    public AddressTableEntry(ScanResultViewModel result) {
        this.ScanningProcessor = result.ScanningProcessor;
        this.Address = result.Address;
        this.dataType = result.DataType;
        this.value = result.CurrentValue;
        this.numericDisplayType = result.NumericDisplayType;
        if (this.dataType == DataType.String) {
            this.stringType = this.ScanningProcessor.StringScanOption;
            this.stringLength = ((DataValueString) this.value).Value.Length;
        }
        else if (this.dataType == DataType.ByteArray) {
            this.ArrayLength = ((DataValueByteArray) this.value).Value.Length;
        }
    }

    /// <summary>
    /// Sets the address of this entry
    /// </summary>
    /// <param name="newAddress">The new address</param>
    /// <param name="isAbsolute">Whether the address is absolute or relative to the parent entry</param>
    public void SetAddress(uint newAddress, bool isAbsolute) {
        if (this.Address != newAddress || this.IsAddressAbsolute != isAbsolute) {
            this.Address = newAddress;
            this.IsAddressAbsolute = isAbsolute;
            this.AddressChanged?.Invoke(this);
        }
    }
}