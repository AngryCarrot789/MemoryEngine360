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

using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.ModTools.Gui;

public sealed class MTGridPanel : BaseMTPanel {
    public readonly struct Entry(BaseMTElement element, SlotIndex slot, SpanInfo span) {
        public readonly BaseMTElement Element = element;
        public readonly SlotIndex Slot = slot;
        public readonly SpanInfo Span = span;
    }
    
    public ObservableList<Entry> Children { get; } = new ObservableList<Entry>();
    public ObservableList<RowDefinition> Rows { get; } = new ObservableList<RowDefinition>();
    public ObservableList<ColumnDefinition> Columns { get; } = new ObservableList<ColumnDefinition>();
    
    public MTGridPanel() {
        this.Children.ItemsAdded += (list, index, items) => items.ForEach(this, (x, self) => self.OnElementAdded(x.Element));
        this.Children.ItemsRemoved += (list, index, items) => items.ForEach(this, (x, self) => self.OnElementRemoved(x.Element));
        this.Children.ItemReplaced += (list, index, oldItem, newItem) => {
            this.OnElementRemoved(oldItem.Element);
            this.OnElementAdded(newItem.Element);
        };
    }

    public override IEnumerable<BaseMTElement> GetChildren() {
        return this.Children.Select(x => x.Element);
    }

    public void Add(BaseMTElement element, SlotIndex index) {
        this.Children.Add(new Entry(element, index, new SpanInfo(1, 1)));
    }
    
    public void Add(BaseMTElement element, SlotIndex index, SpanInfo span) {
        this.Children.Add(new Entry(element, index, span));
    }

    public readonly struct SlotIndex(int row, int column) {
        public readonly int Row = row, Column = column;
    }
    
    public readonly struct SpanInfo(int rows, int columns) {
        public readonly int Rows = rows, Columns = columns;
    }

    public readonly struct RowDefinition(GridDefinitionSize height) {
        public readonly GridDefinitionSize Height = height;
    }

    public readonly struct ColumnDefinition(GridDefinitionSize height) {
        public readonly GridDefinitionSize Height = height;
    }

    public readonly struct GridDefinitionSize(double value, GridSizeType sizeType = GridSizeType.Pixel) {
        public readonly double Value = value;
        public readonly GridSizeType SizeType = sizeType;
    }

    public enum GridSizeType {
        /// <summary>The row or column is auto-sized to fit its content.</summary>
        Auto,

        /// <summary>
        /// The row or column is sized in device independent pixels.
        /// </summary>
        Pixel,

        /// <summary>
        /// The row or column is sized as a weighted proportion of available space.
        /// </summary>
        Star,
    }
}