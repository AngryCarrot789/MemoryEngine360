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

public class ScanResultViewModel : INotifyPropertyChanged {
    public static readonly DataKey<ScanResultViewModel> DataKey = DataKey<ScanResultViewModel>.Create("ScanResultViewModel");
    
    private string currentValue, previousValue;

    public uint Address { get; }

    public DataType DataType { get; }
    
    public MemoryEngine360.NumericDisplayType NumericDisplayType { get; }
    
    public string FirstValue { get; }
    
    public string CurrentValue {
        get => this.currentValue; 
        set => this.SetField(ref this.currentValue, value ?? "");
    }
    
    public string PreviousValue {
        get => this.previousValue; 
        set => this.SetField(ref this.previousValue, value ?? "");
    }
    
    public ScanningProcessor ScanningProcessor { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ScanResultViewModel(ScanningProcessor scanningProcessor, uint address, DataType dataType, MemoryEngine360.NumericDisplayType numericDisplayType, string firstValue) {
        this.ScanningProcessor = scanningProcessor;
        this.Address = address;
        this.DataType = dataType;
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