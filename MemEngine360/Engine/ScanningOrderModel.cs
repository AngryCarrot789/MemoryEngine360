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

using MemEngine360.Engine.Modes;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Engine;

public sealed class ScanningOrderModel {
    private bool isEnabled;
    private DataType dataType;

    public bool IsEnabled {
        get => this.isEnabled;
        set => PropertyHelper.SetAndRaiseINE(ref this.isEnabled, value, this, this.IsEnabledChanged);
    }

    public DataType DataType {
        get => this.dataType;
        set => PropertyHelper.SetAndRaiseINE(ref this.dataType, value, this, this.DataTypeChanged);
    }

    public event EventHandler? IsEnabledChanged;
    public event EventHandler? DataTypeChanged;

    public ScanningOrderModel() {
        this.isEnabled = true;
    }

    public ScanningOrderModel(DataType dataType) : this() {
        this.dataType = dataType;
    }
}