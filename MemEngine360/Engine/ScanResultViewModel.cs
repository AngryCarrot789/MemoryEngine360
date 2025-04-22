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