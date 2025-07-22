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

namespace MemEngine360.BaseFrontEnd.Debugging;

public class ConsoleHexBinarySource : IBinarySource {
    private readonly FragmentedMemoryBuffer cachedMemory;
    private readonly RateLimitedDispatchAction rldaRead;
    private readonly BitRangeUnion requestedRanges = new BitRangeUnion();
    private readonly IConnectionLockPair pair;

    public IReadOnlyBitRangeUnion ValidRanges { get; } = new ReadOnlyBitRangeUnion(new BitRangeUnion([new BitRange(0, uint.MaxValue)]));

    public event BinarySourceDataReceivedEventHandler? DataReceived;

    public ConsoleHexBinarySource(IConnectionLockPair pair) {
        this.pair = pair;
        this.cachedMemory = new FragmentedMemoryBuffer();
        this.rldaRead = new RateLimitedDispatchAction(this.ReadDataCore, TimeSpan.FromSeconds(0.2));
    }

    private async Task ReadDataCore() {
        using IDisposable? t = await this.pair.BusyLock.BeginBusyOperationAsync(500);
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
            byte[] buffer = new byte[range.ByteLength];
            try {
                await connection.ReadBytes((uint) range.Start.ByteIndex, (int) range.ByteLength);
            }
            catch (Exception) {
                continue;
            }
            
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

    public int ReadAvailableData(ulong offset, Span<byte> buffer) {
        lock (this.cachedMemory) {
            return this.cachedMemory.Read(offset, buffer);
        }
    }

    public void RequestDataLater(ulong offset, ulong count) {
        lock (this.requestedRanges) {
            this.requestedRanges.Add(new BitRange(offset, offset + count));
        }

        this.rldaRead.InvokeAsync();
    }

    public void WriteBytes(ulong offset, byte[] data) {
    }

    public void OnAutoRefreshed(byte[] bytes, uint address, int length) {
        lock (this.requestedRanges) {
            lock (this.cachedMemory) {
                this.requestedRanges.Remove(new BitRange(address, address + (uint) length));
                this.cachedMemory.Write(address, bytes.AsSpan(0, length));
            }
        }
        
        this.DataReceived?.Invoke(this, address, (ulong) length);
    }
}