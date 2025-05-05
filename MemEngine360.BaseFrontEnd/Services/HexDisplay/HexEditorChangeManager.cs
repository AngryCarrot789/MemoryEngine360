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

using AvaloniaHex;
using AvaloniaHex.Core.Document;
using AvaloniaHex.Rendering;

namespace MemEngine360.BaseFrontEnd.Services.HexDisplay;

public class HexEditorChangeManager {
    private readonly HexEditor editor;
    private readonly HexView view;
    private MemoryBinaryDocument document;

    private readonly List<(BitRange Range, ChangedRegionLayer Layer)> outlineLayers = new();
    private readonly Dictionary<ChangedRegionLayer, long> layerLastUpdatedTime;

    public HexEditorChangeManager(HexEditor editor) {
        this.editor = editor;
        this.view = editor.HexView;
        this.layerLastUpdatedTime = new Dictionary<ChangedRegionLayer, long>();
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

    public void ProcessChanges(uint baseAddress, byte[] newBytes, int count) {
        byte[] oldBytes = new byte[count];
        this.document.ReadBytes(baseAddress, oldBytes);
        List<BitRange> newRanges = GetChangedRanges(baseAddress, oldBytes, newBytes, count);
        foreach (BitRange newRange in newRanges) {
            this.TryMergeOrCreateLayer(newRange);
        }

        this.CleanupInvalidatedRanges();
    }

    private void TryMergeOrCreateLayer(BitRange newRange) {
        for (int i = 0; i < this.outlineLayers.Count; i++) {
            (BitRange existingRange, ChangedRegionLayer layer) = this.outlineLayers[i];
            long timeNow = DateTime.Now.Ticks;
            long lastTime = this.layerLastUpdatedTime[layer];
            
            if (new DateTime(timeNow - lastTime).Millisecond >= 1200 ? RangesOverlapOrNearby(existingRange, newRange) : existingRange == newRange) {
                BitRange merged = new BitRange(Math.Min(existingRange.Start.ByteIndex, newRange.Start.ByteIndex), Math.Max(existingRange.End.ByteIndex, newRange.End.ByteIndex));
                layer.SetRange(merged);
                this.outlineLayers[i] = (merged, layer);
                this.layerLastUpdatedTime[layer] = timeNow;
                return;
            }
        }

        ChangedRegionLayer newLayer = new ChangedRegionLayer(this, this.editor.Caret);
        newLayer.SetRange(newRange);
        this.view.Layers.Add(newLayer);
        this.outlineLayers.Add((newRange, newLayer));
        this.layerLastUpdatedTime[newLayer] = DateTime.Now.Ticks;
    }

    private void CleanupInvalidatedRanges() {
        for (int i = this.outlineLayers.Count - 1; i >= 0; i--) {
            (BitRange Range, ChangedRegionLayer Layer) obj = this.outlineLayers[i];
            long creation = this.layerLastUpdatedTime[obj.Layer];
            if (TimeSpan.FromTicks(DateTime.Now.Ticks - creation).Seconds > 1.5d) {
                this.view.Layers.Remove(obj.Layer);
                this.layerLastUpdatedTime.Remove(obj.Layer);
                this.outlineLayers.RemoveAt(i);
            }
        }
    }

    private void ClearAllOutlines() {
        foreach ((_, ChangedRegionLayer layer) in this.outlineLayers) {
            this.view.Layers.Remove(layer);
        }

        this.layerLastUpdatedTime.Clear();
        this.outlineLayers.Clear();
    }

    private static bool RangesOverlapOrNearby(BitRange a, BitRange b) {
        return b.End.ByteIndex >= a.Start.ByteIndex && b.Start.ByteIndex <= (a.End.ByteIndex + 1);
    }

    public void OnChangeExpired(ChangedRegionLayer layer) {
        for (int i = this.outlineLayers.Count - 1; i >= 0; i--) {
            (BitRange Range, ChangedRegionLayer Layer) obj = this.outlineLayers[i];
            if (obj.Layer == layer) {
                this.view.Layers.Remove(obj.Layer);
                this.layerLastUpdatedTime.Remove(obj.Layer);
                this.outlineLayers.RemoveAt(i);   
            }
        }
    }
}