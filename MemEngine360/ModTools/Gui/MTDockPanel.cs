﻿// 
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

public delegate void MTDockPanelFillLastChangedEventHandler(MTDockPanel sender);

public sealed class MTDockPanel : BaseMTPanel {
    private bool fillLast;
    
    public ObservableList<(BaseMTElement, DockType?)> Children { get; } = new ObservableList<(BaseMTElement, DockType?)>();

    public bool FillLast {
        get => this.fillLast;
        set => PropertyHelper.SetAndRaiseINE(ref this.fillLast, value, this, static t => t.FillLastChanged?.Invoke(t));
    }

    public event MTDockPanelFillLastChangedEventHandler? FillLastChanged;

    public MTDockPanel() {
        this.Children.ItemsAdded += (list, index, items) => items.ForEach(this, (x, self) => self.OnElementAdded(x.Item1));
        this.Children.ItemsRemoved += (list, index, items) => items.ForEach(this, (x, self) => self.OnElementRemoved(x.Item1));
        this.Children.ItemReplaced += (list, index, oldItem, newItem) => {
            this.OnElementRemoved(oldItem.Item1);
            this.OnElementAdded(newItem.Item1);
        };
    }

    public void Add(BaseMTElement elem, DockType? dockType) {
        if (this.Children.Any(x => x.Item1 == elem))
            throw new InvalidOperationException("Attempt to add duplicate element entry");

        this.Children.Add((elem, dockType));
    }

    public void SetDock(BaseMTElement elem, DockType? dockType) {
        int index = this.Children.FindIndex(elem, static (a, b) => a.Item1 == b);
        if (index == -1)
            throw new InvalidOperationException("Element not added yet");

        this.Children[index] = (elem, dockType);
    }

    public override IEnumerable<BaseMTElement> GetChildren() {
        return this.Children.Select(x => x.Item1);
    }

    public enum DockType {
        Left,
        Bottom,
        Right,
        Top
    }
}