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

using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands.ATM;

public class EditSavedAddressDataTypeCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!BaseAddressTableEntry.DataKey.TryGetContext(e.ContextData, out BaseAddressTableEntry? result) || !(result is AddressTableEntry)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!BaseAddressTableEntry.DataKey.TryGetContext(e.ContextData, out BaseAddressTableEntry? result) || !(result is AddressTableEntry saved)) {
            return;
        }

        SavedResultDataTypeUserInputInfo info = new SavedResultDataTypeUserInputInfo(saved) {
            Caption = "Modify data type"
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
            if (info.DataType.IsNumeric()) {
                if (info.DisplayAsHex) {
                    saved.NumericDisplayType = NumericDisplayType.Hexadecimal;
                }
                else if (info.DisplayAsUnsigned) {
                    saved.NumericDisplayType = NumericDisplayType.Unsigned;
                }
                else {
                    saved.NumericDisplayType = NumericDisplayType.Normal;
                }
            }
            else {
                saved.NumericDisplayType = NumericDisplayType.Normal;
            }
            
            saved.StringType = info.StringScanOption;
            saved.StringLength = info.StringLength;
            saved.ArrayLength = info.ArrayLength;
            saved.DataType = info.DataType;
            saved.ScanningProcessor.RefreshSavedAddressesLater();
        }
    }
}

public delegate void SavedResultDataTypeUserInputInfoEventHandler(SavedResultDataTypeUserInputInfo sender);

public class SavedResultDataTypeUserInputInfo : UserInputInfo {
    private DataType dataType;
    private StringType stringScanOption;
    private int stringLength, arrayLength;
    private bool displayAsHex, displayAsSigned;

    public DataType DataType {
        get => this.dataType;
        set => PropertyHelper.SetAndRaiseINE(ref this.dataType, value, this, static t => t.DataTypeChanged?.Invoke(t));
    }

    public StringType StringScanOption {
        get => this.stringScanOption;
        set => PropertyHelper.SetAndRaiseINE(ref this.stringScanOption, value, this, static t => t.StringScanOptionChanged?.Invoke(t));
    }

    public int StringLength {
        get => this.stringLength;
        set => PropertyHelper.SetAndRaiseINE(ref this.stringLength, value, this, static t => t.StringLengthChanged?.Invoke(t));
    }
    
    public int ArrayLength {
        get => this.arrayLength;
        set => PropertyHelper.SetAndRaiseINE(ref this.arrayLength, value, this, static t => t.ArrayLengthChanged?.Invoke(t));
    }

    public bool DisplayAsHex {
        get => this.displayAsHex;
        set {
            if (this.displayAsHex != value) {
                if (value && this.DisplayAsUnsigned) {
                    this.DisplayAsUnsigned = false;
                }

                this.displayAsHex = value;
                this.DisplayAsHexChanged?.Invoke(this);
            }
        }
    }

    public bool DisplayAsUnsigned {
        get => this.displayAsSigned;
        set {
            if (this.displayAsSigned != value) {
                if (value && this.DisplayAsHex) {
                    this.DisplayAsHex = false;
                }

                this.displayAsSigned = value;
                this.DisplayAsUnsignedChanged?.Invoke(this);
            }
        }
    }

    public event SavedResultDataTypeUserInputInfoEventHandler? DataTypeChanged;
    public event SavedResultDataTypeUserInputInfoEventHandler? StringScanOptionChanged;
    public event SavedResultDataTypeUserInputInfoEventHandler? StringLengthChanged;
    public event SavedResultDataTypeUserInputInfoEventHandler? ArrayLengthChanged;
    public event SavedResultDataTypeUserInputInfoEventHandler? DisplayAsHexChanged;
    public event SavedResultDataTypeUserInputInfoEventHandler? DisplayAsUnsignedChanged;

    public SavedResultDataTypeUserInputInfo() {
    }

    public SavedResultDataTypeUserInputInfo(AddressTableEntry result) {
        this.stringLength = result.StringLength;
        this.arrayLength = result.ArrayLength;
        this.displayAsHex = result.NumericDisplayType == NumericDisplayType.Hexadecimal;
        this.displayAsSigned = result.NumericDisplayType == NumericDisplayType.Unsigned;
        this.dataType = result.DataType;
        this.stringScanOption = result.StringType;
    }

    public override bool HasErrors() => false;

    public override void UpdateAllErrors() {
    }
}