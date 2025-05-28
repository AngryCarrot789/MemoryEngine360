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

using MemEngine360.Engine.Modes;
using MemEngine360.ValueAbstraction;

namespace MemEngine360.Sequencing.DataProviders;

public delegate void RandomNumberDataProviderEventHandler(RandomNumberDataProvider sender);

public sealed class RandomNumberDataProvider : DataValueProvider {
    private readonly Random random;
    public DataType DataType { get; }

    public BaseNumericDataValue Minimum { get; }

    public BaseNumericDataValue Maximum { get; }

    public RandomNumberDataProvider(DataType dataType, BaseNumericDataValue minimum, BaseNumericDataValue maximum) {
        if (!dataType.IsFloatingPoint() && !dataType.IsInteger()) {
            throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Only floats or integers are allowed");
        }

        this.DataType = dataType;
        this.Minimum = minimum;
        this.Maximum = maximum;
        this.random = new Random();
    }

    public override IDataValue? Provide() {
        long maxLong;
        switch (this.DataType) {
            case DataType.Byte:  return new DataValueByte((byte) this.random.NextInt64(this.Minimum.ToByte(), (long) this.Maximum.ToByte() + 1));
            case DataType.Int16: return new DataValueInt16((short) this.random.NextInt64(this.Minimum.ToShort(), (long) this.Maximum.ToShort() + 1));
            case DataType.Int32: return new DataValueInt32((int) this.random.NextInt64(this.Minimum.ToInt(), (long) this.Maximum.ToInt() + 1));
            case DataType.Int64: return new DataValueInt64(this.random.NextInt64(this.Minimum.ToLong(), (maxLong = this.Maximum.ToLong()) != long.MaxValue ? (maxLong + 1) : long.MaxValue));
            case DataType.Float:
            case DataType.Double: {
                double min = this.Minimum!.ToDouble(), max = this.Maximum!.ToDouble();
                double rnd = this.random.NextDouble() * (max - min) + min;
                return this.DataType == DataType.Float ? new DataValueFloat((float) rnd) : new DataValueDouble(rnd);
            }
            default: throw new ArgumentOutOfRangeException();
        }
    }
}