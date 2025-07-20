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
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.BaseFrontEnd.Debugging;

public class AsyncInfiniteDocument : IBinaryDocument {
    private readonly struct ByteRange(ulong begin, ulong end) : IEquatable<ByteRange> {
        public readonly ulong Begin = begin, End = end;
        public ulong Length => this.End - this.Begin;

        public bool Equals(ByteRange other) => this.Begin == other.Begin && this.End == other.End;

        public override bool Equals(object? obj) => obj is ByteRange other && this.Equals(other);

        public override int GetHashCode() => HashCode.Combine(this.Begin, this.End);
    }
    
    private readonly BitRangeUnion dirtyBitRange = new BitRangeUnion();
    private readonly IConnectionLockPair connectionLockPair;
    private readonly RateLimitedDispatchAction rldaReadQueues;
    private readonly SortedList<ByteRange, byte[]> tempBuffers = new SortedList<ByteRange,byte[]>(Comparer<ByteRange>.Create((a, b) => a.Begin.CompareTo(b.Begin)));

    public ulong Length => uint.MaxValue;
    public bool IsReadOnly => false;
    public bool CanInsert => false;
    public bool CanRemove => false;

    public IReadOnlyBitRangeUnion ValidRanges { get; } = new ReadOnlyBitRangeUnion(new BitRangeUnion([new BitRange(0, uint.MaxValue)]));

    public event EventHandler<BinaryDocumentChange>? Changed;

    public AsyncInfiniteDocument(IConnectionLockPair connectionLockPair) {
        this.connectionLockPair = connectionLockPair;

        // Limit maximum updates per second to 100 milliseconds
        this.rldaReadQueues = RateLimitedDispatchActionBase.ForDispatcherAsync(this.OnProcessReadQueues, TimeSpan.FromMilliseconds(100));
    }

    private async Task OnProcessReadQueues() {
        this.tempBuffers.Clear();
        
        IConsoleConnection? connection;
        using IDisposable? token = await this.connectionLockPair.BusyLock.BeginBusyOperationAsync(timeoutMilliseconds: 500);
        if (token != null && (connection = this.connectionLockPair.Connection) != null) {
            if (this.dirtyBitRange.Count < 1) {
                return;
            }
            
            ulong minRange = ulong.MaxValue;
            ulong maxRange = ulong.MinValue;
            foreach (BitRange range in this.dirtyBitRange) {
                byte[] buffer = await connection.ReadBytes((uint) range.Start.ByteIndex, (int) range.ByteLength);
                this.tempBuffers.Add(new ByteRange(range.Start.ByteIndex, range.Start.ByteIndex + range.ByteLength), buffer);
                minRange = Math.Min(minRange, range.Start.ByteIndex);
                maxRange = Math.Max(maxRange, range.End.ByteIndex);
            }
            
            this.dirtyBitRange.Clear();
            this.Changed?.Invoke(this, new BinaryDocumentChange(BinaryDocumentChangeType.Modify, new BitRange(minRange, maxRange)));
        }
    }

    public void ReadBytes(ulong offset, Span<byte> buffer) {
        ulong bufferEndIndex = offset + (ulong)buffer.Length;
        int bytesCopied = 0;
        foreach (KeyValuePair<ByteRange, byte[]> entry in this.tempBuffers) {
            ByteRange range = entry.Key;
            if (range.End <= offset || range.Begin >= bufferEndIndex) {
                continue;
            }

            ulong copyStart = Math.Max(offset, range.Begin);
            ulong copyEnd = Math.Min(bufferEndIndex, range.End);

            int sourceStart = (int)(copyStart - range.Begin);
            int destStart = (int)(copyStart - offset);
            int length = (int)(copyEnd - copyStart);
            
            if (length > 0) {
                entry.Value.AsSpan().Slice(sourceStart, length).CopyTo(buffer.Slice(destStart, length));
                bytesCopied += length;
            }
        }
        
        // If entire buffer is filled, return
        if (bytesCopied == buffer.Length) {
            return;
        }

        this.dirtyBitRange.Add(new BitRange(offset, offset + (ulong) buffer.Length));
        this.rldaReadQueues.InvokeAsync();
    }

    public void WriteBytes(ulong offset, ReadOnlySpan<byte> buffer) {
    }

    public void InsertBytes(ulong offset, ReadOnlySpan<byte> buffer) {
    }

    public void RemoveBytes(ulong offset, ulong length) {
    }

    public void Flush() {
    }

    public void Dispose() {
    }
}