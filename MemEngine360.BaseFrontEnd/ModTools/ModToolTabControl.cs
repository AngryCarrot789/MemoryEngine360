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
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.BaseFrontEnd.ModTools;

public class ModToolTabControl : TabControl {
    public static readonly StyledProperty<ModToolManager?> ModToolManagerProperty = AvaloniaProperty.Register<ModToolTabControl, ModToolManager?>(nameof(ModToolManager));

    public ModToolManager? ModToolManager {
        get => this.GetValue(ModToolManagerProperty);
        set => this.SetValue(ModToolManagerProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TabControl);

    private ObservableItemProcessorIndexing<ModTool>? processor;
    
    public ModToolTabControl() {
    }

    static ModToolTabControl() {
        ModToolManagerProperty.Changed.AddClassHandler<ModToolTabControl, ModToolManager?>((s, e) => s.OnModToolManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnModToolManagerChanged(ModToolManager? oldValue, ModToolManager? newValue) {
        if (oldValue != null) {
            this.processor!.RemoveExistingItems();
            this.processor!.Dispose();
            this.processor = null;
        }
        
        if (newValue != null) {
            this.processor = ObservableItemProcessor.MakeIndexable(newValue.ModTools, this.OnModToolAdded, this.OnModToolRemoved, this.OnModToolMoved);
            this.processor.AddExistingItems();
        }
    }

    private void OnModToolAdded(object sender, int index, ModTool item) {
        this.Items.Insert(index, new ModToolTabItem() {ModTool = item});
    }
    
    private void OnModToolRemoved(object sender, int index, ModTool item) {
        ((ModToolTabItem) this.Items[index]!).ModTool = null;
        this.Items.RemoveAt(index);
    }
    
    private void OnModToolMoved(object sender, int oldindex, int newindex, ModTool item) {
        object? theTabItem = this.Items[oldindex];
        this.Items.RemoveAt(oldindex);
        this.Items.Insert(newindex, theTabItem);
    }
}