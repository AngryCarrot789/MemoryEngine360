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

    public uint Address { get; }

    public string Value { get => this.value; set => this.SetField(ref this.value, value ?? ""); }
    public string Description { get => this.description; set => this.SetField(ref this.description, value ?? ""); }
    public DataType DataType { get => this.dataType; set => this.SetField(ref this.dataType, value); } 
    public StringScanOption StringScanOption { get => this.stringScanOption; set => this.SetField(ref this.stringScanOption, value); } 
    public int StringLength { get => this.stringLength; set => this.SetField(ref this.stringLength, value); }
    public bool DisplayAsHex { get => this.hex; set => this.SetField(ref this.hex, value); }
    public bool DisplayAsUnsigned { get => this.unsigned; set => this.SetField(ref this.unsigned, value); }
    
    public ScanningProcessor ScanningProcessor { get; set; }
    
    public event PropertyChangedEventHandler? PropertyChanged;

    public SavedAddressViewModel(ScanningProcessor scanningProcessor, uint address) {
        this.ScanningProcessor = scanningProcessor;
        this.Address = address;
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