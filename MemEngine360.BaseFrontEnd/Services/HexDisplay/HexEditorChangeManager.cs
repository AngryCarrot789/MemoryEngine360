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

using System.Reflection.Metadata.Ecma335;
using AvaloniaHex;
using AvaloniaHex.Core.Document;
using AvaloniaHex.Rendering;

namespace MemEngine360.BaseFrontEnd.Services.HexDisplay;

public class HexEditorChangeManager {
    private readonly HexEditor editor;
    private readonly HexView view;
    private MemoryBinaryDocument document;
    private readonly Stack<ChangedRegionLayer> layerCache;
    private readonly List<ChangedRegionLayer> myLayers;
    private const int MaxCache = 128;

    public HexEditor Editor => this.editor;

    public HexView View => this.view;

    public MemoryBinaryDocument Document => this.document;

    public HexEditorChangeManager(HexEditor editor) {
        this.editor = editor;
        this.view = editor.HexView;
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

    public void OnDocumentChanged(MemoryBinaryDocument newDocument) {
        this.document = newDocument;
    }

    public void ProcessChanges(uint baseAddress, byte[] newData, int count) {
        byte[] oldBytes = new byte[Math.Min(count, (uint) this.document.Length)];
        this.document.ReadBytes(baseAddress, oldBytes);
        
        List<BitRange> newRanges = GetChangedRanges(baseAddress, oldBytes, newData, oldBytes.Length);
        foreach (BitRange newRange in newRanges) {
            this.TryMergeOrCreateLayer(newRange);
        }

        this.CleanupInvalidatedRanges();
    }

    private void TryMergeOrCreateLayer(BitRange newRange) {
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
        this.view.Layers.Add(newLayer);
    }

    private void CleanupInvalidatedRanges() {
        for (int i = this.myLayers.Count - 1; i >= 0; i--) {
            ChangedRegionLayer theLayer = this.myLayers[i];
            if ((DateTime.Now - theLayer.LastUpdatedTime).Milliseconds >= 1500) {
                this.view.Layers.Remove(theLayer);
                this.myLayers.RemoveAt(i);
            }
        }
    }

    public void OnFadeOutCompleted(ChangedRegionLayer layer) {
        for (int i = this.myLayers.Count - 1; i >= 0; i--) {
            ChangedRegionLayer theLayer = this.myLayers[i];
            if (theLayer == layer) {
                this.myLayers.RemoveAt(i);   
                this.view.Layers.Remove(theLayer);
                break;
            }
        }
    }
    
    private static bool RangesOverlapOrNearby(BitRange a, BitRange b) {
        return b.End.ByteIndex >= a.Start.ByteIndex && b.Start.ByteIndex <= (a.End.ByteIndex + 1);
    }
}