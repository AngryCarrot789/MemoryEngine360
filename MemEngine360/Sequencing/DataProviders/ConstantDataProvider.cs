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

using MemEngine360.ValueAbstraction;

namespace MemEngine360.Sequencing.DataProviders;

public delegate void ConstantDataProviderEventHandler(ConstantDataProvider sender);

public sealed class ConstantDataProvider : DataValueProvider {
    private IDataValue? dataValue;

    public IDataValue? DataValue {
        get => this.dataValue;
        set {
            if (!Equals(this.dataValue, value)) {
                this.dataValue = value;
                this.DataValueChanged?.Invoke(this);
            }
        }
    }

    public event ConstantDataProviderEventHandler? DataValueChanged;

    public ConstantDataProvider() {
    }

    public ConstantDataProvider(IDataValue dataValue) {
        this.DataValue = dataValue;
    }

    public override IDataValue? Provide() {
        return this.DataValue;
    }
}