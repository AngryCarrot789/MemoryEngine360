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

using System.Diagnostics;
using MemEngine360.Engine;
using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.Modes;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Conditions;

public delegate void CompareMemoryConditionEventHandler(CompareMemoryCondition sender);

/// <summary>
/// A sequencer condition that reads a value from the console and compares it to a constant value.
/// </summary>
public class CompareMemoryCondition : BaseSequenceCondition {
    private IMemoryAddress address = StaticAddress.Zero;
    private CompareType compareType;
    private IDataValue? compareTo;
    private bool parseIntAsHex;

    /// <summary>
    /// Gets or sets the address we read the value from
    /// </summary>
    public IMemoryAddress Address {
        get => this.address;
        set => PropertyHelper.SetAndRaiseINE(ref this.address, value, this, static t => t.AddressChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the compare type for this condition. Note, only less/greater comparisons are numeric only, so when the
    /// data value is set to a string or byte array or some other type of data value, this value is automatically set to equal
    /// </summary>
    public CompareType CompareType {
        get => this.compareType;
        set => PropertyHelper.SetAndRaiseINE(ref this.compareType, value, this, static t => t.CompareTypeChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the value to compare to. Note, the console value is compared to this value. So when
    /// <see cref="CompareType"/> is <see cref="Conditions.CompareType.GreaterThan"/> and the console read
    /// value is 10 and this value is 5, the comparison is 10 > 5, which is true.
    /// </summary>
    public IDataValue? CompareTo {
        get => this.compareTo;
        set {
            if (value != null && !value.DataType.IsNumeric()) {
                if (this.CompareType != CompareType.Equals && this.CompareType != CompareType.NotEquals) {
                    this.CompareType = CompareType.Equals;
                }
            }

            PropertyHelper.SetAndRaiseINE(ref this.compareTo, value, this, static t => t.CompareToChanged?.Invoke(t));
        }
    }

    /// <summary>
    /// Gets or sets if the UI should parse integers as hex rather than normal integers
    /// </summary>
    public bool ParseIntAsHex {
        get => this.parseIntAsHex;
        set => PropertyHelper.SetAndRaiseINE(ref this.parseIntAsHex, value, this, static t => t.ParseIntAsHexChanged?.Invoke(t));
    }

    public event CompareMemoryConditionEventHandler? AddressChanged;
    public event CompareMemoryConditionEventHandler? CompareTypeChanged;
    public event CompareMemoryConditionEventHandler? CompareToChanged;
    public event CompareMemoryConditionEventHandler? ParseIntAsHexChanged;

    public CompareMemoryCondition() {
    }

    protected override async Task<bool> IsConditionMetCore(SequenceExecutionContext ctx, CachedConditionData cache, CancellationToken cancellationToken) {
        // store in local variable since IsConditonMet runs in a BGT, not main thread
        IDataValue? cmpVal = this.CompareTo;
        if (cmpVal == null) {
            // when we have no value, just say condition met because why not
            return true;
        }

        IDataValue? consoleValue;
        IDisposable? busyToken = ctx.BusyToken;
        try {
            if (busyToken == null && !ctx.IsConnectionDedicated) {
                BusyLock busyLock = ctx.Sequence.Manager!.MemoryEngine.BusyLocker;
                // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                if ((busyToken = busyLock.TryBeginBusyOperation()) == null) {
                    ctx.Progress.Text = BusyLock.WaitingMessage;
                    if ((busyToken = await busyLock.BeginBusyOperationAsync(cancellationToken)) == null) {
                        // only reached if token is cancelled
                        Debug.Assert(cancellationToken.IsCancellationRequested);
                        cancellationToken.ThrowIfCancellationRequested();
                        return false;
                    }
                }
            }

            IMemoryAddress addr = this.Address;
            if (!cache.TryGetAddress(addr, out uint actualAddress)) {
                uint? resolved = await addr.TryResolveAddress(ctx.Connection);
                if (!resolved.HasValue)
                    return false; // Address is unresolvable, maybe pointer chain is invalid or wrong game or whatever.

                actualAddress = resolved.Value;
            }

            if (!cache.TryGetDataValue(new TypedAddress(cmpVal.DataType, actualAddress), out consoleValue)) {
                consoleValue = await MemoryEngine.ReadDataValue(ctx.Connection, actualAddress, cmpVal);
                cache[new TypedAddress(consoleValue.DataType, actualAddress)] = consoleValue;
            }
        }
        // Do not catch Timeout/IO exceptions, instead, let task sequence handle it
        finally {
            // Do not dispose of ctx.BusyToken. That's the priority token!!
            if (!ctx.IsConnectionDedicated && !busyToken!.Equals(ctx.BusyToken)) {
                busyToken.Dispose();
            }
        }

        switch (cmpVal.DataType) {
            case DataType.Byte:
            case DataType.Int16:
            case DataType.Int32:
            case DataType.Int64:
            case DataType.Float:
            case DataType.Double: {
                BaseNumericDataValue cmpNumber = (BaseNumericDataValue) cmpVal;
                BaseNumericDataValue consoleNumber = (BaseNumericDataValue) consoleValue;
                int cmp = consoleNumber.CompareTo(cmpNumber);
                switch (this.CompareType) {
                    case CompareType.Equals:              return cmp == 0;
                    case CompareType.NotEquals:           return cmp != 0;
                    case CompareType.LessThan:            return cmp < 0;
                    case CompareType.LessThanOrEquals:    return cmp <= 0;
                    case CompareType.GreaterThan:         return cmp > 0;
                    case CompareType.GreaterThanOrEquals: return cmp >= 0;
                    default:                              throw new ArgumentOutOfRangeException();
                }
            }
            case DataType.String: {
                bool equals = consoleValue.BoxedValue.Equals(cmpVal.BoxedValue);
                return this.CompareType == CompareType.Equals ? equals : !equals;
            }
            case DataType.ByteArray: {
                bool equals = ((DataValueByteArray) consoleValue).Value.SequenceEqual(((DataValueByteArray) cmpVal).Value);
                return this.CompareType == CompareType.Equals ? equals : !equals;
            }
            default: throw new ArgumentOutOfRangeException();
        }
    }

    public override BaseSequenceCondition CreateClone() {
        return new CompareMemoryCondition() {
            IsEnabled = this.IsEnabled,
            Address = this.Address,
            CompareType = this.CompareType,
            CompareTo = this.CompareTo,
            ParseIntAsHex = this.ParseIntAsHex
        };
    }
}

public enum CompareType {
    Equals, // ==
    NotEquals, // !=
    LessThan, // <
    LessThanOrEquals, // <=
    GreaterThan, // >
    GreaterThanOrEquals, // >=
}