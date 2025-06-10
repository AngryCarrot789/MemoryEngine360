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
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.DataProviders;

public delegate void ConstantDataProviderEventHandler(ConstantDataProvider sender);

public sealed class ConstantDataProvider : DataValueProvider {
    private DataType dataType = DataType.Int32;
    private IDataValue? dataValue;
    private StringType stringType;
    private bool parseIntAsHex;

    public DataType DataType {
        get => this.dataType;
        set => PropertyHelper.SetAndRaiseINE(ref this.dataType, value, this, static t => t.DataTypeChanged?.Invoke(t));
    }

    public IDataValue? DataValue {
        get => this.dataValue;
        set => PropertyHelper.SetAndRaiseINE(ref this.dataValue, value, this, static t => t.DataValueChanged?.Invoke(t));
    }

    public StringType StringType {
        get => this.stringType;
        set => PropertyHelper.SetAndRaiseINE(ref this.stringType, value, this, static t => t.StringTypeChanged?.Invoke(t));
    }

    public bool ParseIntAsHex {
        get => this.parseIntAsHex;
        set => PropertyHelper.SetAndRaiseINE(ref this.parseIntAsHex, value, this, static t => t.ParseIntAsHexChanged?.Invoke(t));
    }

    public event ConstantDataProviderEventHandler? DataTypeChanged;
    public event ConstantDataProviderEventHandler? DataValueChanged;
    public event ConstantDataProviderEventHandler? StringTypeChanged;
    public event ConstantDataProviderEventHandler? ParseIntAsHexChanged;

    public ConstantDataProvider() {
    }

    public ConstantDataProvider(IDataValue dataValue) {
        this.dataType = dataValue.DataType;
        this.dataValue = dataValue;
    }

    public override IDataValue? Provide() => this.dataValue;
}