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
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Engine;

public class SavedAddressViewModel : INotifyPropertyChanged {
    public static readonly DataKey<SavedAddressViewModel> DataKey = DataKey<SavedAddressViewModel>.Create("SavedAddressViewModel");

    private string value = "", description = "";
    private DataType dataType = DataType.Byte;
    private StringScanOption stringScanOption = StringScanOption.UTF8;
    private int stringLength = 0;
    private bool hex, unsigned;

    // This class needs a re-work. We shouldn't use a raw address like this,
    // since cheat engine doesn't appear to do that (since you have use base address + a list of offsets)
    public uint Address { get; }

    public string Value { get => this.value; set => this.SetField(ref this.value, value ?? ""); }
    public string Description { get => this.description; set => this.SetField(ref this.description, value ?? ""); }
    public DataType DataType { get => this.dataType; set => this.SetField(ref this.dataType, value); }
    public StringScanOption StringScanOption { get => this.stringScanOption; set => this.SetField(ref this.stringScanOption, value); }
    public int StringLength { get => this.stringLength; set => this.SetField(ref this.stringLength, value); }
    public bool DisplayAsHex { get => this.hex; set => this.SetField(ref this.hex, value); }
    public bool DisplayAsUnsigned { get => this.unsigned; set => this.SetField(ref this.unsigned, value); }

    public ScanningProcessor ScanningProcessor { get; set; }

    public MemoryEngine360.NumericDisplayType NumericDisplayType {
        get {
            if (this.DisplayAsHex)
                return MemoryEngine360.NumericDisplayType.Hexadecimal;
            if (this.DisplayAsUnsigned)
                return MemoryEngine360.NumericDisplayType.Unsigned;
            return MemoryEngine360.NumericDisplayType.Normal;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SavedAddressViewModel(ScanningProcessor scanningProcessor, uint address) {
        this.ScanningProcessor = scanningProcessor;
        this.Address = address;
    }

    public SavedAddressViewModel(ScanResultViewModel result) {
        this.ScanningProcessor = result.ScanningProcessor;
        this.Address = result.Address;
        this.dataType = result.DataType;
        this.value = result.CurrentValue;
        switch (result.NumericDisplayType) {
            case MemoryEngine360.NumericDisplayType.Normal:      break;
            case MemoryEngine360.NumericDisplayType.Unsigned:    this.unsigned = true; break;
            case MemoryEngine360.NumericDisplayType.Hexadecimal: this.hex = true; break;
            default:                                             throw new ArgumentOutOfRangeException();
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
        if (!EqualityComparer<T>.Default.Equals(field, value)) {
            field = value;
            this.OnPropertyChanged(propertyName);
        }
    }
}