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
using PFXToolKitUI.Utils.Ranges;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.Engine.HexEditing;

public class ConsoleHexBinarySource : IBinarySource {
    private readonly FragmentedMemoryBuffer cachedMemory;
    private readonly RateLimitedDispatchAction rldaRead;
    private readonly IntegerSet<ulong> requestedRanges = new IntegerSet<ulong>();
    private readonly IntegerSet<ulong> availableRanges = new IntegerSet<ulong>();
    private readonly IConnectionLockPair pair;
    private readonly Lock memoryLock = new Lock();
    private readonly ObservableIntegerSet<ulong> validRanges = new ObservableIntegerSet<ulong>();

    // We cannot use ((ulong) uint.MaxValue) + 1 because for some reason XBDM cannot
    // read 16 bytes at 0xFFFFFFF0. It can read 15 bytes no problem.
    // But when you read 16, it just returns a blank line for the data.
    public BitRange ApplicableRange => new BitRange(0, uint.MaxValue);

    /// <summary>
    /// Gets the IntRange union of valid ranges 
    /// </summary>
    public IObservableIntegerSet<ulong> ValidRanges => this.validRanges;

    public IReadOnlyIntegerSet<ulong> AvailableDataRanges => this.availableRanges;
    
    public bool CanWriteBackInto => true;

    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    public ConsoleHexBinarySource(IConnectionLockPair pair) {
        this.pair = pair;
        this.cachedMemory = new FragmentedMemoryBuffer();
        this.rldaRead = new RateLimitedDispatchAction(this.ReadFromConsole, TimeSpan.FromSeconds(0.1));
    }

    private async Task ReadFromConsole() {
        BusyLock busyLocker = this.pair.BusyLock;
        if (busyLocker.IsBusy) {
            if (this.requestedRanges.Ranges.Count > 0)
                this.rldaRead.InvokeAsync();
            return;
        }

        using IBusyToken? t = await busyLocker.BeginBusyOperation(500);
        IConsoleConnection? connection;
        if (t == null || (connection = this.pair.Connection) == null || connection.IsClosed) {
            if (this.requestedRanges.Ranges.Count > 0)
                this.rldaRead.InvokeAsync();
            
            return;
        }

        List<IntegerRange<ulong>> requests;
        lock (this.requestedRanges) {
            requests = this.requestedRanges.ToList();
            this.requestedRanges.Clear();
        }

        if (requests.Count < 1) {
            return;
        }

        // Maximum temporary buffer size of 8192. On a 1080p monitor with the window maximized,
        // and bytes/row set as 64 (default is 32), the maximum number of visible bytes is 4032.
        // This buffer is big enough for most scenarios 
        int maximumBuffer = connection.GetRecommendedReadChunkSize(8192);
        IntegerSet<ulong> receivedRanges = new IntegerSet<ulong>();
        
        using (ArrayPools.Rent(maximumBuffer, out byte[] buffer)) {
            foreach (IntegerRange<ulong> range in requests) {
                for (ulong i = 0; i < range.Length; i += (uint) maximumBuffer) {
                    int length = Math.Min(maximumBuffer, (int) (range.Length - i));
                    
                    try {
                        await connection.ReadBytes((uint) range.Start, buffer, 0, length);
                    }
                    catch (Exception) {
                        continue;
                    }

                    receivedRanges.Add(range);
                    using (this.memoryLock.EnterScope()) {
                        this.cachedMemory.Write(range.Start, buffer.AsSpan(0, length));
                        this.availableRanges.Add(IntegerRange.FromStartAndLength(range.Start, (ulong) length));
                        this.validRanges.Add(IntegerRange.FromStartAndEnd(range.Start, range.Start + (ulong) length));
                    }
                }
            }
        }
        
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
            foreach (IntegerRange<ulong> range in receivedRanges) {
                this.DataReceived?.Invoke(this, new DataReceivedEventArgs(range.Start, range.Length));
            }
        });
    }

    public void InvalidateCache(ulong offset, ulong count) {
        // offset = Maths.SumAndClampOverflow(offset, -256);
        // count = Maths.SumAndClampOverflow(count, 256);
        
        lock (this.requestedRanges) {
            using (this.memoryLock.EnterScope()) {
                IntegerRange<ulong> range = IntegerRange.FromStartAndLength(offset, count);
                this.requestedRanges.Remove(range);
                this.cachedMemory.Clear(offset, count);
                this.availableRanges.Remove(range);
                this.validRanges.Remove(range);
            }
        }
    }

    public int ReadAvailableData(ulong offset, Span<byte> buffer, BitRangeUnion? affectedRanges) {
        using (this.memoryLock.EnterScope()) {
            List<IntegerRange<ulong>>? affectedRanges2 = affectedRanges != null ? new List<IntegerRange<ulong>>() : null; 
            int cbRead = this.cachedMemory.Read(offset, buffer, affectedRanges2);
            if (affectedRanges2 != null) {
                foreach (IntegerRange<ulong> range in affectedRanges2) {
                    affectedRanges!.Add(BitRange.FromLength(range.Start, range.Length));
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
            using IBusyToken? t = await this.pair.BusyLock.BeginBusyOperation(cancellation);
            IConsoleConnection? connection;
            if (t == null || (connection = this.pair.Connection) == null || connection.IsClosed) {
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
            this.requestedRanges.Add(IntegerRange.FromStartAndLength(offset, count));
        }

        this.rldaRead.InvokeAsync();
    }

    public void OnUserInput(ulong offset, byte[] data) {
        using (this.memoryLock.EnterScope()) {
            this.cachedMemory.Write(offset, data);
        }
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
                IntegerRange<ulong> range = IntegerRange.FromStartAndLength(address, (ulong) buffer.Length);
                this.requestedRanges.Remove(range);
                this.cachedMemory.Write(address, buffer);
                this.availableRanges.Add(range);
            }
        }

        this.DataReceived?.Invoke(this, new DataReceivedEventArgs(address, (ulong) buffer.Length));
    }
}