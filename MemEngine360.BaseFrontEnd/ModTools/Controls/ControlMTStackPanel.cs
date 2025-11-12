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
using Avalonia.Layout;
using MemEngine360.ModTools.Gui;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.BaseFrontEnd.ModTools.Controls;

public class ControlMTStackPanel : StackPanel {
    public static readonly StyledProperty<MTStackPanel?> MTStackPanelProperty = AvaloniaProperty.Register<ControlMTStackPanel, MTStackPanel?>(nameof(MTStackPanel));

    public MTStackPanel? MTStackPanel {
        get => this.GetValue(MTStackPanelProperty);
        set => this.SetValue(MTStackPanelProperty, value);
    }

    private ObservableItemProcessorIndexing<BaseMTElement>? processor;
    private readonly BaseControlBindingHelper baseBindingHelper;
    private readonly AttachedModelHelper<MTStackPanel> fullyLoadedHelper;

    protected override Type StyleKeyOverride => typeof(StackPanel);
    
    public ControlMTStackPanel() {
        this.baseBindingHelper = new BaseControlBindingHelper(this);
        this.fullyLoadedHelper = new AttachedModelHelper<MTStackPanel>(this, this.OnIsFullyLoadedChanged);
    }

    static ControlMTStackPanel() {
        MTStackPanelProperty.Changed.AddClassHandler<ControlMTStackPanel, MTStackPanel?>((s, e) => s.OnMTStackPanelChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnMTStackPanelChanged(MTStackPanel? oldValue, MTStackPanel? newValue) {
        this.fullyLoadedHelper.Model = Optionals.OfNullable(newValue);
        if (newValue != null) {
            this.Orientation = newValue.IsVertical ? Orientation.Vertical : Orientation.Horizontal;
        }
    }
    
    private void OnIsFullyLoadedChanged(MTStackPanel panel, bool attached) {
        this.baseBindingHelper.SetModel(attached ? panel : null);
        if (!attached) {
            this.processor!.RemoveExistingItems();
            this.processor!.Dispose();
            this.processor = null;
        }
        else {
            this.processor = ObservableItemProcessor.MakeIndexable(panel.Children, this.OnItemAdded, this.OnItemRemoved, this.OnItemMoved);
            this.processor.AddExistingItems();
        }
    }

    private void OnItemAdded(object sender, int index, BaseMTElement item) {
        this.Children.Insert(index, ModToolView.CreateControl(item));
    }

    private void OnItemRemoved(object sender, int index, BaseMTElement item) {
        this.Children.RemoveAt(index);
    }

    private void OnItemMoved(object sender, int oldIndex, int newIndex, BaseMTElement item) {
        this.Children.Move(oldIndex, newIndex);
    }
}