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

using AvaloniaHex.Base.Document;
using MemEngine360.Connections;
using PFXToolKitUI;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.Engine.HexEditing;

public class ConsoleHexBinarySource : IBinarySource {
    private readonly FragmentedMemoryBuffer cachedMemory;
    private readonly RateLimitedDispatchAction rldaRead;
    private readonly BitRangeUnion requestedRanges = new BitRangeUnion();
    private readonly BitRangeUnion availableRanges = new BitRangeUnion();
    private readonly IConnectionLockPair pair;
    private readonly Lock memoryLock = new Lock();

    public BitRange ApplicableRange => new BitRange(0, uint.MaxValue);

    public IReadOnlyBitRangeUnion AvailableDataRanges => this.availableRanges;

    public event BinarySourceDataReceivedEventHandler? DataReceived;

    public ConsoleHexBinarySource(IConnectionLockPair pair) {
        this.pair = pair;
        this.cachedMemory = new FragmentedMemoryBuffer();
        this.rldaRead = new RateLimitedDispatchAction(this.ReadFromConsole, TimeSpan.FromSeconds(0.2));
    }

    private async Task ReadFromConsole() {
        BusyLock busyLocker = this.pair.BusyLock;
        if (busyLocker.IsBusy) {
            return;
        }

        using IDisposable? t = await busyLocker.BeginBusyOperationAsync(500);
        IConsoleConnection? connection;
        if (t == null || (connection = this.pair.Connection) == null || !connection.IsConnected) {
            return;
        }

        List<BitRange> requests;
        lock (this.requestedRanges) {
            requests = this.requestedRanges.ToList();
            this.requestedRanges.Clear();
        }

        if (requests.Count < 1) {
            return;
        }

        BitRangeUnion union = new BitRangeUnion();
        foreach (BitRange range in requests) {
            byte[] buffer;
            try {
                buffer = await connection.ReadBytes((uint) range.Start.ByteIndex, (int) range.ByteLength);
            }
            catch (Exception) {
                continue;
            }

            union.Add(range);
            using (this.memoryLock.EnterScope()) {
                this.cachedMemory.Write(range.Start.ByteIndex, buffer);
                this.availableRanges.Add(BitRange.FromLength(range.Start.ByteIndex, (ulong) buffer.Length));
            }
        }

        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
            foreach (BitRange range in union) {
                this.DataReceived?.Invoke(this, range.Start.ByteIndex, range.ByteLength);
            }
        });
    }

    public void InvalidateCache(ulong offset, ulong count) {
        // offset = Maths.SumAndClampOverflow(offset, -256);
        // count = Maths.SumAndClampOverflow(count, 256);
        
        lock (this.requestedRanges) {
            using (this.memoryLock.EnterScope()) {
                BitRange range = BitRange.FromLength(offset, count);
                this.requestedRanges.Remove(range);
                this.cachedMemory.Clear(offset, count);
                this.availableRanges.Remove(range);
            }
        }
    }

    public int ReadAvailableData(ulong offset, Span<byte> buffer, BitRangeUnion? affectedRanges) {
        using (this.memoryLock.EnterScope()) {
            List<(ulong, ulong)>? affectedRanges2 = affectedRanges != null ? new List<(ulong, ulong)>() : null; 
            int cbRead = this.cachedMemory.Read(offset, buffer, affectedRanges2);
            if (affectedRanges2 != null) {
                foreach ((ulong Start, ulong Count) range in affectedRanges2) {
                    affectedRanges!.Add(BitRange.FromLength(range.Start, range.Count));
                }
            }

            return cbRead;
        }
    }

    public int ReadAvailableData(ulong offset, Span<byte> buffer) {
        using (this.memoryLock.EnterScope()) {
            return this.cachedMemory.Read(offset, buffer);
        }
    }

    public async Task<int> ReadAvailableDataAsync(ulong offset, Memory<byte> dstBuffer, CancellationToken cancellation) {
        int read = this.ReadAvailableData(offset, dstBuffer.Span);
        if (read < dstBuffer.Length) {
            using IDisposable? t = await this.pair.BusyLock.BeginBusyOperationAsync(cancellation);
            IConsoleConnection? connection;
            if (t == null || (connection = this.pair.Connection) == null || !connection.IsConnected) {
                return read;
            }

            byte[] buffer = await connection.ReadBytes((uint) offset + (uint) read, dstBuffer.Length - read);
            buffer.CopyTo(dstBuffer);
            return dstBuffer.Length;
        }

        return read;
    }

    public void RequestDataLater(ulong offset, ulong count) {
        lock (this.requestedRanges) {
            this.requestedRanges.Add(BitRange.FromLength(offset, count));
        }

        this.rldaRead.InvokeAsync();
    }

    public void WriteBytesForUserInput(ulong offset, byte[] data) {
    }

    /// <summary>
    /// Writes data into our cache, removing the range from the request list in the process, then fires <see cref="DataReceived"/>
    /// </summary>
    /// <param name="address"></param>
    /// <param name="bytes"></param>
    /// <param name="length"></param>
    public void WriteBytesToCache(uint address, Span<byte> buffer) {
        lock (this.requestedRanges) {
            using (this.memoryLock.EnterScope()) {
                BitRange range = BitRange.FromLength(address, (uint) buffer.Length);
                this.requestedRanges.Remove(range);
                this.cachedMemory.Write(address, buffer);
                this.availableRanges.Add(range);
            }
        }

        this.DataReceived?.Invoke(this, address, (ulong) buffer.Length);
    }
}