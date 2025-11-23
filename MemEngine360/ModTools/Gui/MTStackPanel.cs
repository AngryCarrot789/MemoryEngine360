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
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.ModTools.Gui;

public sealed class MTStackPanel : BaseMTPanel {
    public ObservableList<BaseMTElement> Children { get; } = new ObservableList<BaseMTElement>();

    public bool IsVertical {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsVerticalChanged);
    }

    public event EventHandler? IsVerticalChanged;

    public MTStackPanel() {
        this.Children.ItemsAdded += (list, index, items) => items.ForEach(this, (x, self) => self.OnElementAdded(x));
        this.Children.ItemsRemoved += (list, index, items) => items.ForEach(this, (x, self) => self.OnElementRemoved(x));
        this.Children.ItemReplaced += (list, index, oldItem, newItem) => {
            this.OnElementRemoved(oldItem);
            this.OnElementAdded(newItem);
        };
    }
    
    public override IEnumerable<BaseMTElement> GetChildren() {
        return this.Children;
    }
}