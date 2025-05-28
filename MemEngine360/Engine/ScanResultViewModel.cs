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
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Engine;

public class ScanResultViewModel : INotifyPropertyChanged {
    public static readonly DataKey<ScanResultViewModel> DataKey = DataKey<ScanResultViewModel>.Create("ScanResultViewModel");

    private IDataValue currentValue, previousValue;

    public uint Address { get; }

    public DataType DataType { get; }

    public NumericDisplayType NumericDisplayType { get; }

    public StringType StringType { get; }

    public IDataValue FirstValue { get; }

    public IDataValue CurrentValue {
        get => this.currentValue;
        set {
            if (value.DataType != this.DataType)
                throw new ArgumentException($"New value's data type does not match our data type: {value.DataType} != {this.DataType}");

            if (!this.currentValue.Equals(value))
                this.SetField(ref this.currentValue, value ?? throw new ArgumentNullException(nameof(value), nameof(this.CurrentValue) + " cannot be null"));
        }
    }

    public IDataValue PreviousValue {
        get => this.previousValue;
        set {
            if (value.DataType != this.DataType)
                throw new ArgumentException($"New value's data type does not match our data type: {value.DataType} != {this.DataType}");

            if (!this.previousValue.Equals(value))
                this.SetField(ref this.previousValue, value ?? throw new ArgumentNullException(nameof(value), nameof(this.PreviousValue) + " cannot be null"));
        }
    }

    /// <summary>
    /// Gets the string length of <see cref="CurrentValue"/>, or zero if not a string. This is just a helper property used for auto refresh
    /// </summary>
    public int CurrentStringLength => (this.currentValue as DataValueString)?.Value.Length ?? 0;

    /// <summary>
    /// Gets the length of the array in <see cref="CurrentValue"/>, or zero if not an array. This is just a helper property used for auto refresh
    /// </summary>
    public int CurrentArrayLength => (this.currentValue as DataValueByteArray)?.Value.Length ?? 0;

    public ScanningProcessor ScanningProcessor { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ScanResultViewModel(ScanningProcessor scanningProcessor, uint address, DataType dataType, NumericDisplayType numericDisplayType, StringType stringType, IDataValue firstValue) {
        this.ScanningProcessor = scanningProcessor;
        this.Address = address;
        this.DataType = dataType;
        this.StringType = stringType;
        this.NumericDisplayType = numericDisplayType;
        this.FirstValue = this.currentValue = this.previousValue = firstValue;
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