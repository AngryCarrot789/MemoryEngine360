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

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.ValueAbstraction;

namespace MemEngine360.Sequencing.Operations;

public delegate void SetVariableOperationEventHandler(SetMemoryOperation sender);
public delegate void SetVariableOperationDataValueProviderChangedEventHandler(SetMemoryOperation sender, DataValueProvider? oldProvider, DataValueProvider? newProvider);

/// <summary>
/// An operation that sets a value on the console
/// </summary>
public class SetMemoryOperation : BaseSequenceOperation {
    private uint address;
    private DataValueProvider? dataValueProvider;
    private uint iterateCount = 1;

    /// <summary>
    /// Gets or sets the address we write at
    /// </summary>
    public uint Address {
        get => this.address;
        set {
            if (this.address != value) {
                this.address = value;
                this.AddressChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets the value we write to the console
    /// </summary>
    public DataValueProvider? DataValueProvider {
        get => this.dataValueProvider;
        set {
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
        set {
            if (this.iterateCount == value) {
                return;
            }

            this.iterateCount = value;
            this.IterateCountChanged?.Invoke(this);
        }
    }

    public override string DisplayName => "Set Memory";

    public event SetVariableOperationEventHandler? AddressChanged;
    public event SetVariableOperationDataValueProviderChangedEventHandler? DataValueProviderChanged;
    public event SetVariableOperationEventHandler? IterateCountChanged;

    public SetMemoryOperation() {
    }

    protected override async Task RunOperation(SequenceExecutionContext ctx, CancellationToken token) {
        if (this.iterateCount < 1) {
            return; // no point in doing anything when we won't write anything
        }
        
        DataValueProvider? provider = this.dataValueProvider;
        IDataValue? value;
        if (provider != null && (value = provider.Provide()) != null) {
            IDisposable? busyToken = ctx.BusyToken;
            if (busyToken == null && !ctx.IsConnectionDedicated) {
                ctx.Progress.Text = "Waiting for busy operations...";
                if ((busyToken = await ctx.Sequence.Manager!.MemoryEngine.BeginBusyOperationAsync(token)) == null) {
                    return;
                }
            }

            try {
                ctx.Progress.Text = "Setting memory";
                byte[]? buffer = GetDataBuffer(ctx, value, provider, this.iterateCount);
                if (buffer != null)
                    await ctx.Connection.WriteBytes(this.address, buffer);
            }
            finally {
                // Do not dispose of ctx.BusyToken. That's the priority token!!
                if (!ctx.IsConnectionDedicated && !busyToken!.Equals(ctx.BusyToken)) {
                    busyToken.Dispose();
                }
            }
        }
    }

    private static byte[]? GetDataBuffer(SequenceExecutionContext ctx, IDataValue value, DataValueProvider provider, uint iterate) {
        uint byteCount = value.ByteCount;
        if (byteCount == 0 || (iterate > (int.MaxValue / byteCount)) /* overflow check */) {
            return null;
        }
        
        Span<byte> bytes = stackalloc byte[(int) byteCount];
        value.GetBytes(bytes);
        if (value.DataType.IsEndiannessSensitive() && BitConverter.IsLittleEndian != ctx.Connection.IsLittleEndian) {
            bytes.Reverse();
        }
                
        bool appendNullChar = value.DataType == DataType.String && provider.AppendNullCharToString;
        byte[] buffer = new byte[bytes.Length * iterate + (appendNullChar ? 1 : 0)];
        ref byte bufferAddress = ref MemoryMarshal.GetArrayDataReference(buffer);
        for (int i = 0, j = 0; i < iterate; i++, j += bytes.Length) {
            bytes.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref bufferAddress, j), bytes.Length));
        }

        return buffer;
    }
}