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

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MemEngine360.Configs;
using MemEngine360.Engine;
using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Operations;

public delegate void SetVariableOperationEventHandler(SetMemoryOperation sender);

public delegate void SetVariableOperationDataValueProviderChangedEventHandler(SetMemoryOperation sender, DataValueProvider? oldProvider, DataValueProvider? newProvider);

/// <summary>
/// An operation that sets a value on the console
/// </summary>
public class SetMemoryOperation : BaseSequenceOperation {
    private IMemoryAddress address = StaticAddress.Zero;
    private DataValueProvider? dataValueProvider;
    private uint iterateCount = 1;
    private SetMemoryWriteMode writeMode;

    // Used to save the values when the user switches provider type in the UI.
    // Yeah I know UI stuff in models yucky etc etc, may fix later
    public ConstantDataProvider? InitialConstantDataProvider;
    public RandomNumberDataProvider? InitialRandomNumberDataProvider;

    /// <summary>
    /// Gets or sets the address we write at
    /// </summary>
    public IMemoryAddress Address {
        get => this.address;
        set {
            ArgumentNullException.ThrowIfNull(value);
            PropertyHelper.SetAndRaiseINE(ref this.address, value, this, static t => t.AddressChanged?.Invoke(t));
        }
    }

    /// <summary>
    /// Gets or sets the value we write to the console
    /// </summary>
    public DataValueProvider? DataValueProvider {
        get => this.dataValueProvider;
        set {
            if (this.InitialConstantDataProvider == null && value is ConstantDataProvider)
                this.InitialConstantDataProvider = (ConstantDataProvider?) value;
            else if (this.InitialRandomNumberDataProvider == null && value is RandomNumberDataProvider)
                this.InitialRandomNumberDataProvider = (RandomNumberDataProvider?) value;

            DataValueProvider? oldProvider = this.dataValueProvider;
            if (!Equals(oldProvider, value)) {
                this.dataValueProvider = value;
                this.DataValueProviderChanged?.Invoke(this, oldProvider, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the number of times to write the value. Default is 1, meaning write once. Setting this to 0 basically disables this operation
    /// </summary>
    public uint IterateCount {
        get => this.iterateCount;
        set => PropertyHelper.SetAndRaiseINE(ref this.iterateCount, value, this, static t => t.IterateCountChanged?.Invoke(t));
    }

    public SetMemoryWriteMode WriteMode {
        get => this.writeMode;
        set => PropertyHelper.SetAndRaiseINE(ref this.writeMode, value, this, static t => t.WriteModeChanged?.Invoke(t));
    }

    public override string DisplayName => "Set Memory";

    public event SetVariableOperationEventHandler? AddressChanged;
    public event SetVariableOperationDataValueProviderChangedEventHandler? DataValueProviderChanged;
    public event SetVariableOperationEventHandler? IterateCountChanged;
    public event SetVariableOperationEventHandler? WriteModeChanged;

    public SetMemoryOperation() {
    }

    protected override async Task RunOperation(SequenceExecutionContext ctx, CancellationToken token) {
        if (this.iterateCount < 1) {
            return; // no point in doing anything when we won't write anything
        }

        DataValueProvider? provider = this.dataValueProvider;
        IDataValue? value;
        if (provider == null) {
            return;
        }

        lock (provider.Lock) {
            if ((value = provider.Provide()) == null) {
                return;
            }
        }

        IBusyToken? busyToken = ctx.BusyToken;
        if (busyToken == null && !ctx.IsConnectionDedicated) {
            ctx.Progress.Text = BusyLock.WaitingMessage;
            if ((busyToken = await ctx.Sequence.Manager!.MemoryEngine.BeginBusyOperationAsync(token)) == null) {
                return;
            }
        }

        try {
            ctx.Progress.Text = "Resolving address";
            uint resolvedAddress = await this.address.TryResolveAddress(ctx.Connection) ?? 0;
            if (resolvedAddress == 0) {
                return;
            }

            if (this.writeMode != SetMemoryWriteMode.Set && value is DataValueNumeric numVal) {
                DataValueNumeric consoleVal = (DataValueNumeric) await MemoryEngine.ReadDataValue(ctx.Connection, resolvedAddress, numVal);
                if (numVal.DataType.IsFloatingPoint()) {
                    double dval;
                    switch (this.writeMode) {
                        case SetMemoryWriteMode.Add:      dval = consoleVal.ToDouble() + numVal.ToDouble(); break;
                        case SetMemoryWriteMode.Multiply: dval = consoleVal.ToDouble() * numVal.ToDouble(); break;
                        case SetMemoryWriteMode.Divide:
                            double d = numVal.ToDouble();
                            dval = d != 0.0 ? consoleVal.ToDouble() / d : consoleVal.ToDouble();
                            break;
                        default:
                            Debug.Fail("Error");
                            return;
                    }

                    // protect against NaN or Inf, since it can cause some games to completely freak out and crash the console 
                    if ((double.IsNaN(dval) || double.IsInfinity(dval)) && BasicApplicationConfiguration.Instance.UseNaNInfProtection) {
                        dval = 0.0;
                    }

                    value = numVal.DataType == DataType.Float ? new DataValueFloat((float) Math.Clamp(dval, float.MinValue, float.MaxValue)) : new DataValueDouble(dval);
                }
                else {
                    long lval;
                    switch (this.writeMode) {
                        case SetMemoryWriteMode.Add:      lval = consoleVal.ToLong() + numVal.ToLong(); break;
                        case SetMemoryWriteMode.Multiply: lval = consoleVal.ToLong() * numVal.ToLong(); break;
                        case SetMemoryWriteMode.Divide:
                            long l = numVal.ToLong();
                            lval = l != 0 ? consoleVal.ToLong() / l : consoleVal.ToLong();
                            break;
                        default:
                            Debug.Fail("Error");
                            return;
                    }

                    switch (numVal.DataType) {
                        case DataType.Byte:  value = new DataValueByte((byte) Math.Clamp(lval, byte.MinValue, byte.MaxValue)); break;
                        case DataType.Int16: value = new DataValueInt16((short) Math.Clamp(lval, short.MinValue, short.MaxValue)); break;
                        case DataType.Int32: value = new DataValueInt32((int) Math.Clamp(lval, int.MinValue, int.MaxValue)); break;
                        case DataType.Int64: value = new DataValueInt64(lval); break;
                        default:
                            Debug.Fail("Error");
                            return;
                    }
                }
            }

            ctx.Progress.Text = "Setting memory";
            byte[]? buffer = GetDataBuffer(ctx.Connection.IsLittleEndian, value, provider.AppendNullCharToString, this.iterateCount);
            if (buffer != null) {
                // Note for test connection, when a sequence repeats forever and has no delay, only sets memory,
                // this method is extremely laggy when a debugger is attached, probably because of the
                // timeout exceptions being thrown, and the IDE tries to intercept them
                await ctx.Connection.WriteBytes(resolvedAddress, buffer);
            }
        }
        finally {
            // Do not dispose of ctx.BusyToken. That's the priority token!!
            if (!ctx.IsConnectionDedicated && !busyToken!.Equals(ctx.BusyToken)) {
                busyToken.Dispose();
            }
        }
    }

    protected override BaseSequenceOperation CreateCloneCore() {
        return new SetMemoryOperation() {
            Address = this.Address,
            DataValueProvider = this.DataValueProvider?.CreateClone(),
            IterateCount = this.IterateCount
        };
    }

    private static byte[]? GetDataBuffer(bool littleEndian, IDataValue value, bool shouldAppendNullChar, uint iterate) {
        int byteCount = value.ByteCount;
        if (byteCount == 0 || (iterate > (int.MaxValue / byteCount)) /* overflow check */) {
            return null;
        }

        byte[]? heapArray = null;
        try {
            Span<byte> tmpBytes = byteCount <= 1024 ? stackalloc byte[byteCount] : (Span<byte>) (heapArray = ArrayPool<byte>.Shared.Rent(byteCount));
            Span<byte> srcBuffer = tmpBytes.Slice(0, value.GetBytes(tmpBytes, littleEndian));

            bool appendNullChar = value.DataType == DataType.String && shouldAppendNullChar;
            byte[] dstBuffer = new byte[srcBuffer.Length * iterate + (appendNullChar ? 1 : 0)];
            ref byte bufferAddress = ref MemoryMarshal.GetArrayDataReference(dstBuffer);
            for (int i = 0, j = 0; i < iterate; i++, j += srcBuffer.Length) {
                srcBuffer.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref bufferAddress, j), srcBuffer.Length));
            }

            return dstBuffer;
        }
        finally {
            if (heapArray != null) {
                ArrayPool<byte>.Shared.Return(heapArray);
            }
        }
    }
}