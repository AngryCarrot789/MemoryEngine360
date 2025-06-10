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

public delegate void RandomNumberDataProviderEventHandler(RandomNumberDataProvider sender);

public sealed class RandomNumberDataProvider : DataValueProvider {
    private readonly Random random;
    private BaseNumericDataValue? minimum, maximum;

    private DataType dataType = DataType.Int32;
    private bool parseIntAsHex;

    public DataType DataType {
        get => this.dataType;
        set {
            if (!value.IsFloatingPoint() && !value.IsInteger()) {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Only floats or integers are allowed");
            }

            PropertyHelper.SetAndRaiseINE(ref this.dataType, value, this, static t => t.DataTypeChanged?.Invoke(t));
        }
    }

    public bool ParseIntAsHex {
        get => this.parseIntAsHex;
        set => PropertyHelper.SetAndRaiseINE(ref this.parseIntAsHex, value, this, static t => t.ParseIntAsHexChanged?.Invoke(t));
    }

    public BaseNumericDataValue? Minimum {
        get => this.minimum;
        set {
            if (value != null && value.DataType != this.DataType)
                throw new ArgumentException("New value's data type does not match our data type: " + value.DataType + " != " + this.DataType);

            PropertyHelper.SetAndRaiseINE(ref this.minimum, value, this, static t => t.MinimumChanged?.Invoke(t));
        }
    }

    public BaseNumericDataValue? Maximum {
        get => this.maximum;
        set {
            if (value != null && value.DataType != this.DataType)
                throw new ArgumentException("New value's data type does not match our data type: " + value.DataType + " != " + this.DataType);

            PropertyHelper.SetAndRaiseINE(ref this.maximum, value, this, static t => t.MaximumChanged?.Invoke(t));
        }
    }

    public event RandomNumberDataProviderEventHandler? DataTypeChanged;
    public event RandomNumberDataProviderEventHandler? ParseIntAsHexChanged;
    public event RandomNumberDataProviderEventHandler? MinimumChanged, MaximumChanged;

    public RandomNumberDataProvider() {
        this.random = new Random();
    }

    public RandomNumberDataProvider(DataType dataType, BaseNumericDataValue minimum, BaseNumericDataValue maximum) : this() {
        this.DataType = dataType;
        this.Minimum = minimum;
        this.Maximum = maximum;
    }

    public override IDataValue? Provide() {
        BaseNumericDataValue? theMin, theMax;

        lock (this.Lock) {
            if ((theMin = this.minimum) == null || (theMax = this.maximum) == null) {
                return null;
            }   
        }

        long maxLong;
        switch (this.DataType) {
            case DataType.Byte:  return new DataValueByte((byte) this.random.NextInt64(theMin.ToByte(), (long) theMax.ToByte() + 1));
            case DataType.Int16: return new DataValueInt16((short) this.random.NextInt64(theMin.ToShort(), (long) theMax.ToShort() + 1));
            case DataType.Int32: return new DataValueInt32((int) this.random.NextInt64(theMin.ToInt(), (long) theMax.ToInt() + 1));
            case DataType.Int64: return new DataValueInt64(this.random.NextInt64(theMin.ToLong(), (maxLong = theMax.ToLong()) != long.MaxValue ? (maxLong + 1) : long.MaxValue));
            case DataType.Float:
            case DataType.Double: {
                double min = theMin!.ToDouble(), max = theMax!.ToDouble();
                double rnd = this.random.NextDouble() * (max - min) + min;
                return this.DataType == DataType.Float ? new DataValueFloat((float) rnd) : new DataValueDouble(rnd);
            }
            default: throw new ArgumentOutOfRangeException();
        }
    }
}