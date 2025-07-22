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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaHex.Base.Document;
using PFXToolKitUI;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.Avalonia;

public class TestFileBinarySource : IBinarySource, IDisposable, IAsyncDisposable {
    private FileStream? fileStream;

    // private readonly List<(ulong, byte[])> buffers = new List<(ulong, byte[])>();
    private readonly FragmentedMemoryBuffer cachedMemory;
    private readonly RateLimitedDispatchAction rldaRead;
    private readonly BitRangeUnion requestedRanges = new BitRangeUnion();

    public BitRange ApplicableRange => new BitRange(0, uint.MaxValue);
    public IReadOnlyBitRangeUnion AvailableDataRanges { get; } = new BitRangeUnion([new BitRange(0, uint.MaxValue)]);
    public event BinarySourceDataReceivedEventHandler? DataReceived;

    public TestFileBinarySource(FileStream fileStream) {
        this.fileStream = fileStream;
        this.cachedMemory = new FragmentedMemoryBuffer();
        this.rldaRead = new RateLimitedDispatchAction(this.ReadDataCore);
    }

    private async Task ReadDataCore() {
        if (this.fileStream == null) {
            return;
        }

        List<BitRange> requests;
        lock (this.requestedRanges) {
            requests = this.requestedRanges.ToList();
            this.requestedRanges.Clear();
        }

        BitRangeUnion union = new BitRangeUnion();
        foreach (BitRange range in requests) {
            byte[] buffer = new byte[range.ByteLength];
            this.fileStream.Seek((long) range.Start.ByteIndex, SeekOrigin.Begin);
            int read = await this.fileStream.ReadAsync(buffer.AsMemory());
            union.Add(range);

            lock (this.cachedMemory) {
                this.cachedMemory.Write(range.Start.ByteIndex, buffer);
            }
        }

        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
            foreach (BitRange range in union) {
                this.DataReceived?.Invoke(this, range.Start.ByteIndex, range.ByteLength);
            }
        });
    }

    public void InvalidateCache(ulong offset, ulong count) {
        lock (this.requestedRanges) {
            lock (this.cachedMemory) {
                this.requestedRanges.Remove(new BitRange(offset, offset + count));
                this.cachedMemory.Clear(offset, count);
            }
        }
    }

    public int ReadAvailableData(ulong offset, Span<byte> buffer, BitRangeUnion? affectedRanges) {
        lock (this.cachedMemory) {
            affectedRanges?.Add(BitRange.FromLength(offset, (ulong) buffer.Length));
            return this.cachedMemory.Read(offset, buffer);
        }
    }

    public Task<int> ReadAvailableDataAsync(ulong offset, Memory<byte> buffer, CancellationToken cancellation) {
        return Task.FromResult(0);
    }

    public void RequestDataLater(ulong offset, ulong count) {
        lock (this.requestedRanges) {
            this.requestedRanges.Add(new BitRange(offset, offset + count));
        }

        this.rldaRead.InvokeAsync();
    }

    public void WriteBytesForUserInput(ulong offset, byte[] data) {
    }

    public void Dispose() {
        this.fileStream?.Dispose();
        this.fileStream = null;
    }

    public async ValueTask DisposeAsync() {
        if (this.fileStream != null) {
            try {
                await this.fileStream.DisposeAsync();
            }
            finally {
                this.fileStream = null;
            }
        }
    }
}