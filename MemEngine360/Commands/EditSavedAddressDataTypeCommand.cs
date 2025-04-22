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

using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Commands;

public class EditSavedAddressDataTypeCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!SavedAddressViewModel.DataKey.TryGetContext(e.ContextData, out SavedAddressViewModel? result)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!SavedAddressViewModel.DataKey.TryGetContext(e.ContextData, out SavedAddressViewModel? result)) {
            return;
        }

        SavedResultDataTypeUserInputInfo info = new SavedResultDataTypeUserInputInfo(result) {
            Caption = "Modify data type"
        };
        
        if (ApplicationPFX.Instance.ServiceManager.TryGetService(out IEditSavedAddressService? service)) {
            if (await service.ShowDialog(info) == true) {
                result.DisplayAsHex = info.DisplayAsHex;
                result.DisplayAsUnsigned = info.DisplayAsUnsigned;
                result.DataType = info.DataType;
                result.StringScanOption = info.StringScanOption;
                result.StringLength = info.StringLength;
                result.ScanningProcessor.RefreshSavedAddresses();
            }
        }
    }
}

public delegate void SavedResultDataTypeUserInputInfoEventHandler(SavedResultDataTypeUserInputInfo sender);

public class SavedResultDataTypeUserInputInfo : UserInputInfo {
    private DataType dataType;
    private StringScanOption stringScanOption;
    private int stringLength;
    private bool displayAsHex, displayAsSigned;

    public DataType DataType {
        get => this.dataType;
        set {
            if (this.dataType == value)
                return;

            this.dataType = value;
            this.DataTypeChanged?.Invoke(this);
        }
    }

    public StringScanOption StringScanOption {
        get => this.stringScanOption;
        set {
            if (this.stringScanOption == value)
                return;

            this.stringScanOption = value;
            this.StringScanOptionChanged?.Invoke(this);
        }
    }

    public int StringLength {
        get => this.stringLength;
        set {
            if (this.stringLength == value)
                return;

            this.stringLength = value;
            this.StringLengthChanged?.Invoke(this);
        }
    }

    public bool DisplayAsHex {
        get => this.displayAsHex;
        set {
            if (this.displayAsHex == value)
                return;

            this.displayAsHex = value;
            this.DisplayAsHexChanged?.Invoke(this);
        }
    }

    public bool DisplayAsUnsigned {
        get => this.displayAsSigned;
        set {
            if (this.displayAsSigned == value)
                return;

            this.displayAsSigned = value;
            this.DisplayAsUnsignedChanged?.Invoke(this);
        }
    }

    public event SavedResultDataTypeUserInputInfoEventHandler? DataTypeChanged;
    public event SavedResultDataTypeUserInputInfoEventHandler? StringScanOptionChanged;
    public event SavedResultDataTypeUserInputInfoEventHandler? StringLengthChanged;
    public event SavedResultDataTypeUserInputInfoEventHandler? DisplayAsHexChanged;
    public event SavedResultDataTypeUserInputInfoEventHandler? DisplayAsUnsignedChanged;

    public SavedResultDataTypeUserInputInfo() {
    }

    public SavedResultDataTypeUserInputInfo(SavedAddressViewModel result) {
        this.stringLength = result.StringLength;
        this.displayAsHex = result.DisplayAsHex;
        this.displayAsSigned = result.DisplayAsUnsigned;
        this.dataType = result.DataType;
        this.stringScanOption = result.StringScanOption;
    }

    public override bool HasErrors() => false;

    public override void UpdateAllErrors() {
    }
}

public interface IEditSavedAddressService {
    Task<bool?> ShowDialog(SavedResultDataTypeUserInputInfo info);
}