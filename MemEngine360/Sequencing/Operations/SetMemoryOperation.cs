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
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.ValueAbstraction;

namespace MemEngine360.Sequencing.Operations;

public delegate void SetVariableOperationEventHandler(SetMemoryOperation sender);

/// <summary>
/// An operation that sets a value on the console
/// </summary>
public class SetMemoryOperation : BaseSequenceOperation {
    private uint address;
    private DataValueProvider? dataValueProvider;
    private uint iterateCount;

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
            if (!Equals(this.dataValueProvider, value)) {
                this.dataValueProvider = value;
                this.DataValueProviderChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets the number of times to repeat writing the value. Default is 0, meaning do not repeat, just write once
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
    public event SetVariableOperationEventHandler? DataValueProviderChanged;
    public event SetVariableOperationEventHandler? IterateCountChanged;

    public SetMemoryOperation() {
    }

    protected override async Task RunOperation(SequenceExecutionContext ctx, CancellationToken token) {
        DataValueProvider? provider = this.dataValueProvider;
        IDataValue? value;
        if (provider != null && (value = provider.Provide()) != null) {
            IDisposable? busyToken = ctx.BusyToken;
            if (busyToken == null) {
                ctx.Progress.Text = "Waiting for busy operations...";
                if ((busyToken = await ctx.Sequence.Manager!.Engine.BeginBusyOperationAsync(token)) == null) {
                    return;
                }
            }

            try {
                ctx.Progress.Text = "Setting memory";
                bool appendNullChar = value.DataType == DataType.String && provider.AppendNullCharToString;
                uint count = this.iterateCount;
                byte[] data = value.GetBytes(ctx.Connection.IsLittleEndian);
                byte[] buffer = new byte[data.Length * count + (appendNullChar ? 1 : 0)];
                for (int i = 0, j = 0; i < count; i++, j += data.Length) {
                    Buffer.BlockCopy(data, 0, buffer, j, data.Length);
                }

                await ctx.Connection.WriteBytes(this.address, buffer);
            }
            finally {
                if (!busyToken.Equals(ctx.BusyToken)) {
                    busyToken.Dispose();
                }
            }
        }
    }
}