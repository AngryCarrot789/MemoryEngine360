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
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Sequencing.DataProviders;

public sealed class ConstantDataProvider : DataValueProvider {
    public DataType DataType {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.DataTypeChanged);
    } = DataType.Int32;

    public IDataValue? DataValue {
        get => field;
        set {
            PropertyHelper.SetAndRaiseINE(ref field, null, this, this.DataValueChanged);
            if (value != null) {
                this.DataType = value.DataType;
            }
        }
    }

    public StringType StringType {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.StringTypeChanged);
    }

    public bool ParseIntAsHex {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.ParseIntAsHexChanged);
    }

    public event EventHandler? DataTypeChanged;
    public event EventHandler? DataValueChanged;
    public event EventHandler? StringTypeChanged;
    public event EventHandler? ParseIntAsHexChanged;

    public ConstantDataProvider() {
    }

    public ConstantDataProvider(IDataValue dataValue) {
        this.DataType = dataValue.DataType;
        this.DataValue = dataValue;
    }

    public override IDataValue? Provide() => this.DataValue;

    public override DataValueProvider CreateClone() {
        return new ConstantDataProvider() {
            DataType = this.DataType,
            DataValue = this.DataValue,
            StringType = this.StringType,
            ParseIntAsHex = this.ParseIntAsHex,
        };
    }
}