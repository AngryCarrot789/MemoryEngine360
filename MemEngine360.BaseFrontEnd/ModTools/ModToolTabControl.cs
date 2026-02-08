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

using Avalonia;
using Avalonia.Controls;
using MemEngine360.ModTools;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.BaseFrontEnd.ModTools;

public class ModToolTabControl : TabControl {
    public static readonly StyledProperty<ModToolManager?> ModToolManagerProperty = AvaloniaProperty.Register<ModToolTabControl, ModToolManager?>(nameof(ModToolManager));

    public ModToolManager? ModToolManager {
        get => this.GetValue(ModToolManagerProperty);
        set => this.SetValue(ModToolManagerProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TabControl);

    public ModToolTabControl() {
    }

    static ModToolTabControl() {
        ModToolManagerProperty.Changed.AddClassHandler<ModToolTabControl, ModToolManager?>((s, e) => s.OnModToolManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnModToolManagerChanged(ModToolManager? oldValue, ModToolManager? newValue) {
        if (oldValue != null) {
            oldValue.ToolAdded -= this.OnToolAdded;
            oldValue.ToolRemoved -= this.OnToolRemoved;
            oldValue.ToolMoved -= this.OnToolMoved;
            ItemEventUtils.InvokeItems(oldValue.ModTools, null, this.OnToolRemoved, isAdding: false);
        }

        if (newValue != null) {
            newValue.ToolAdded += this.OnToolAdded;
            newValue.ToolRemoved += this.OnToolRemoved;
            newValue.ToolMoved += this.OnToolMoved;
            ItemEventUtils.InvokeItems(newValue.ModTools, null, this.OnToolAdded, isAdding: true);
        }
    }

    private void OnToolAdded(object? sender, ItemIndexEventArgs<ModTool> e) {
        this.Items.Insert(e.Index, new ModToolTabItem() { ModTool = e.Item });
    }

    private void OnToolRemoved(object? sender, ItemIndexEventArgs<ModTool> e) {
        ((ModToolTabItem) this.Items[e.Index]!).ModTool = null;
        this.Items.RemoveAt(e.Index);
    }

    private void OnToolMoved(object? sender, ItemMovedEventArgs<ModTool> e) {
        object? oldItem = this.Items[e.OldIndex];
        _ = this.Items[e.NewIndex];
        this.Items.RemoveAt(e.OldIndex);
        this.Items.Insert(e.NewIndex, oldItem);
    }
}