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

using AvaloniaHex;
using AvaloniaHex.Base.Document;
using AvaloniaHex.Rendering;

namespace MemEngine360.BaseFrontEnd.Services.HexEditing;

public class HexEditorChangeManager {
    private readonly Stack<ChangedRegionLayer> layerCache;
    private readonly List<ChangedRegionLayer> myLayers;
    private const int MaxCache = 128;

    public HexEditor Editor { get; }

    public HexView View { get; }

    public IBinaryDocument Document { get; private set; }

    public HexEditorChangeManager(HexEditor editor) {
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

    public static List<BitRange> GetChangedRanges(ulong baseAddress, byte[] oldData, byte[] newData, int count) {
        int start = -1;
        List<BitRange> ranges = new List<BitRange>();
        for (int i = 0; i < count; i++) {
            if (oldData[i] != newData[i]) {
                if (start == -1) {
                    start = i;
                }
            }
            else if (start != -1) {
                ranges.Add(new BitRange(baseAddress + (ulong) start, baseAddress + (ulong) i));
                start = -1;
            }
        }

        if (start != -1) {
            ranges.Add(new BitRange(baseAddress + (ulong) start, baseAddress + (ulong) count));
        }

        return ranges;
    }

    public void OnDocumentChanged(IBinaryDocument newDocument) {
        this.Document = newDocument;
    }

    public void ProcessChanges(uint baseAddress, byte[] newData, int count) {
        byte[] oldBytes = new byte[Math.Min(count, (uint) this.Document.Length)];
        this.Document.ReadBytes(baseAddress, oldBytes);
        
        List<BitRange> newRanges = GetChangedRanges(baseAddress, oldBytes, newData, oldBytes.Length);
        foreach (BitRange newRange in newRanges) {
            this.TryMergeOrCreateLayer(newRange);
        }

        this.CleanupInvalidatedRanges();
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
            }
        }
    }

    public void OnFadeOutCompleted(ChangedRegionLayer layer) {
        for (int i = this.myLayers.Count - 1; i >= 0; i--) {
            ChangedRegionLayer theLayer = this.myLayers[i];
            if (theLayer == layer) {
                this.myLayers.RemoveAt(i);   
                this.View.Layers.Remove(theLayer);
                break;
            }
        }
    }
    
    private static bool RangesOverlapOrNearby(BitRange a, BitRange b) {
        return b.End.ByteIndex >= a.Start.ByteIndex && b.Start.ByteIndex <= (a.End.ByteIndex + 1);
    }
}