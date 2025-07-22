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

using AvaloniaHex.Async;
using AvaloniaHex.Async.Rendering;
using AvaloniaHex.Base.Document;

namespace MemEngine360.BaseFrontEnd.Services.HexEditing;

public class HexEditorChangeManager {
    private readonly Stack<ChangedRegionLayer> layerCache;
    private readonly List<ChangedRegionLayer> myLayers;
    private const int MaxCache = 128;

    public AsyncHexEditor Editor { get; }

    public AsyncHexView View { get; }

    public IBinarySource? BinarySource { get; private set; }

    public HexEditorChangeManager(AsyncHexEditor editor) {
        this.Editor = editor;
        this.View = editor.HexView;
        this.layerCache = new Stack<ChangedRegionLayer>(MaxCache);
        this.myLayers = new List<ChangedRegionLayer>(32);
    }

    private void PushLayerAsCached(ChangedRegionLayer layer) {
        if (this.layerCache.Count < MaxCache) {
            this.layerCache.Push(layer);
        }
    }

    private ChangedRegionLayer GetCachedLayerOrNew() {
        return this.layerCache.Count > 0 ? this.layerCache.Pop() : new ChangedRegionLayer(this);
    }

    public static BitRangeUnion GetChangedRanges(ulong baseAddress, Span<byte> oldData, Span<byte> newData, int count) {
        int start = -1;
        BitRangeUnion union = new BitRangeUnion();
        for (int i = 0; i < count; i++) {
            if (oldData[i] != newData[i]) {
                if (start == -1) {
                    start = i;
                }
            }
            else if (start != -1) {
                union.Add(new BitRange(baseAddress + (ulong) start, baseAddress + (ulong) i));
                start = -1;
            }
        }

        if (start != -1) {
            union.Add(new BitRange(baseAddress + (ulong) start, baseAddress + (ulong) count));
        }

        return union;
    }

    public void OnBinarySourceChanged(IBinarySource? source) {
        this.BinarySource = source;
    }

    public void ProcessChanges(uint baseAddress, byte[] newData, int count) {
        this.ProcessChanges(baseAddress, newData.AsSpan(0, count));
    }

    public void ProcessChanges(uint baseAddress, Span<byte> newData) {
        if (this.BinarySource == null) {
            return;
        }

        byte[] oldBytes = new byte[newData.Length];
        
        BitRangeUnion availableOldBytes = new BitRangeUnion();
        this.BinarySource.ReadAvailableData(baseAddress, oldBytes, availableOldBytes);

        BitRangeUnion newRanges = GetChangedRanges(baseAddress, oldBytes, newData, oldBytes.Length);
        foreach (BitRange newRange in newRanges) {
            if (availableOldBytes.Contains(newRange.Start)) {
                this.TryMergeOrCreateLayer(newRange);
            }
        }

        this.CleanupInvalidatedRanges();
    }

    public void Clear() {
        for (int i = this.myLayers.Count - 1; i >= 0; i--) {
            ChangedRegionLayer theLayer = this.myLayers[i];
            this.View.Layers.Remove(theLayer);
            this.myLayers.RemoveAt(i);
            this.PushLayerAsCached(theLayer);
        }
    }

    private void TryMergeOrCreateLayer(BitRange newRange) {
        if (this.myLayers.Count > 1000) {
            return;
        }

        for (int i = 0; i < this.myLayers.Count; i++) {
            ChangedRegionLayer layer = this.myLayers[i];
            int timeLived = (DateTime.Now - layer.LastUpdatedTime).Milliseconds;
            if (timeLived >= 1200 ? RangesOverlapOrNearby(layer.Range, newRange) : layer.Range == newRange) {
                layer.SetRange(new BitRange(Math.Min(layer.Range.Start.ByteIndex, newRange.Start.ByteIndex), Math.Max(layer.Range.End.ByteIndex, newRange.End.ByteIndex)));
                return;
            }
        }

        ChangedRegionLayer newLayer = this.GetCachedLayerOrNew();
        newLayer.SetRange(newRange);
        this.myLayers.Add(newLayer);
        this.View.Layers.Add(newLayer);
    }

    private void CleanupInvalidatedRanges() {
        for (int i = this.myLayers.Count - 1; i >= 0; i--) {
            ChangedRegionLayer theLayer = this.myLayers[i];
            if ((DateTime.Now - theLayer.LastUpdatedTime).Milliseconds >= 1500) {
                this.View.Layers.Remove(theLayer);
                this.myLayers.RemoveAt(i);
                this.PushLayerAsCached(theLayer);
            }
        }
    }

    public void OnFadeOutCompleted(ChangedRegionLayer layer) {
        for (int i = this.myLayers.Count - 1; i >= 0; i--) {
            ChangedRegionLayer theLayer = this.myLayers[i];
            if (theLayer == layer) {
                this.myLayers.RemoveAt(i);
                this.View.Layers.Remove(theLayer);
                this.PushLayerAsCached(theLayer);
                break;
            }
        }
    }

    private static bool RangesOverlapOrNearby(BitRange a, BitRange b) {
        return b.End.ByteIndex >= a.Start.ByteIndex && b.Start.ByteIndex <= (a.End.ByteIndex + 1);
    }
}