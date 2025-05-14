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

namespace MemEngine360.Engine.SavedAddressing;

public delegate void AddressTableEntryEventHandler(AddressTableEntry sender);

public class AddressTableEntry : BaseAddressTableEntry {
    private bool isAutoRefreshEnabled = true;
    private uint address;
    private string value = "";
    private DataType dataType = DataType.Byte;
    private StringType stringType = StringType.UTF8;
    private uint stringLength = 0;
    private NumericDisplayType numericDisplayType;
    private bool isAddressAbsolute = true;

    /// <summary>
    /// Gets or sets if this saved address is active, as in, being refreshed every so often. False disables auto-refresh
    /// </summary>
    public bool IsAutoRefreshEnabled {
        get => this.isAutoRefreshEnabled;
        set {
            if (this.isAutoRefreshEnabled != value) {
                this.isAutoRefreshEnabled = value;
                this.IsAutoRefreshEnabledChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets this entry's address. May be relative to parent, so use <see cref="AbsoluteAddress"/>
    /// </summary>
    public uint Address {
        get => this.address;
        set {
            if (this.address != value) {
                this.address = value;
                this.AddressChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// A helper property to resolve the absolute address based on <see cref="IsAddressAbsolute"/> and our parent's address
    /// </summary>
    public uint AbsoluteAddress => this.isAddressAbsolute ? this.Address : ((this.Parent?.AbsoluteAddress ?? 0) + this.address);

    /// <summary>
    /// Gets or sets the current presented value
    /// </summary>
    public string Value {
        get => this.value;
        set {
            if (this.value != value) {
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
        set {
            if (this.dataType != value) {
                this.dataType = value;
                this.DataTypeChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets how to interpolate bytes at the address as a string
    /// </summary>
    public StringType StringType {
        get => this.stringType;
        set {
            if (this.stringType != value) {
                this.stringType = value;
                this.StringTypeChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets the number of characters in the string
    /// </summary>
    public uint StringLength {
        get => this.stringLength;
        set {
            if (this.stringLength != value) {
                this.stringLength = value;
                this.StringLengthChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets how to present the value as a string
    /// </summary>
    public NumericDisplayType NumericDisplayType {
        get => this.numericDisplayType;
        set {
            if (this.numericDisplayType != value) {
                this.numericDisplayType = value;
                this.NumericDisplayTypeChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets if <see cref="AbsoluteAddress"/> should return <see cref="Address"/> or add our
    /// parent's <see cref="AddressTableGroupEntry.AbsoluteAddress"/> to it
    /// </summary>
    public bool IsAddressAbsolute {
        get => this.isAddressAbsolute;
        set {
            if (this.isAddressAbsolute == value)
                return;

            this.isAddressAbsolute = value;
            this.IsAddressAbsoluteChanged?.Invoke(this);
        }
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

    public event AddressTableEntryEventHandler? IsAutoRefreshEnabledChanged;
    public event AddressTableEntryEventHandler? AddressChanged;
    public event AddressTableEntryEventHandler? ValueChanged;
    public event AddressTableEntryEventHandler? DataTypeChanged;
    public event AddressTableEntryEventHandler? StringTypeChanged;
    public event AddressTableEntryEventHandler? StringLengthChanged;
    public event AddressTableEntryEventHandler? NumericDisplayTypeChanged;
    public event AddressTableEntryEventHandler? IsAddressAbsoluteChanged;

    public AddressTableEntry(ScanningProcessor scanningProcessor, uint address) {
        this.ScanningProcessor = scanningProcessor;
        this.address = address;
    }

    public AddressTableEntry(ScanResultViewModel result) {
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
}