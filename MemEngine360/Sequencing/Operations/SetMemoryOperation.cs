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

using MemEngine360.ValueAbstraction;

namespace MemEngine360.Sequencing.Operations;

public delegate void SetVariableOperationEventHandler(SetMemoryOperation sender);

/// <summary>
/// An operation that sets a value on the console
/// </summary>
public class SetMemoryOperation : BaseSequenceOperation {
    private uint address;
    private IDataValue? dataValue;
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
    public IDataValue? DataValue {
        get => this.dataValue;
        set {
            if (!Equals(this.dataValue, value)) {
                this.dataValue = value;
                this.DataValueChanged?.Invoke(this);
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
    public event SetVariableOperationEventHandler? DataValueChanged;
    public event SetVariableOperationEventHandler? IterateCountChanged;

    public SetMemoryOperation() {
    }

    protected override async Task RunOperation(SequenceExecutionContext ctx, CancellationToken token) {
        if (this.dataValue != null) {
            IDisposable? busyToken = ctx.BusyToken;
            if (busyToken == null) {
                ctx.Progress.Text = "Waiting for busy operations...";
                if ((busyToken = await ctx.Sequence.Manager!.Engine.BeginBusyOperationAsync(token)) == null) {
                    return;
                }
            }

            try {
                ctx.Progress.Text = "Setting memory";
                if (this.iterateCount == 0) {
                    await this.dataValue.WriteToConnection(this.address, ctx.Connection);
                }
                else {
                    uint count = this.iterateCount;
                    // optimised repeat write
                    byte[] data = this.dataValue.GetBytes();
                    byte[] buffer = new byte[data.Length * count];
                    for (int i = 0, j = 0; i < count; i++, j += data.Length) {
                        Buffer.BlockCopy(data, 0, buffer, j, data.Length);
                    }

                    await ctx.Connection.WriteBytes(this.address, buffer);
                }
            }
            finally {
                if (!busyToken.Equals(ctx.BusyToken)) {
                    busyToken.Dispose();
                }
            }
        }
    }
}