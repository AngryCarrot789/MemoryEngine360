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
using MemEngine360.ModTools.Gui;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.BaseFrontEnd.ModTools.Controls;

public class ControlMTDockPanel : DockPanel {
    public static readonly StyledProperty<MTDockPanel?> MTDockPanelProperty = AvaloniaProperty.Register<ControlMTDockPanel, MTDockPanel?>(nameof(MTDockPanel));

    public MTDockPanel? MTDockPanel {
        get => this.GetValue(MTDockPanelProperty);
        set => this.SetValue(MTDockPanelProperty, value);
    }

    private ObservableItemProcessorIndexing<(BaseMTElement, MTDockPanel.DockType?)>? processor;
    private readonly BaseControlBindingHelper baseBindingHelper;
    private readonly AttachedModelHelper<MTDockPanel> fullyLoadedHelper;

    protected override Type StyleKeyOverride => typeof(DockPanel);

    public ControlMTDockPanel() {
        this.baseBindingHelper = new BaseControlBindingHelper(this);
        this.fullyLoadedHelper = new AttachedModelHelper<MTDockPanel>(this, this.OnIsFullyLoadedChanged);
    }

    static ControlMTDockPanel() {
        MTDockPanelProperty.Changed.AddClassHandler<ControlMTDockPanel, MTDockPanel?>((s, e) => s.OnMTDockPanelChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnMTDockPanelChanged(MTDockPanel? oldValue, MTDockPanel? newValue) {
        this.fullyLoadedHelper.Model = Optionals.OfNullable(newValue);
        if (newValue != null) {
            this.LastChildFill = newValue.FillLast;
        }
    }

    private void OnIsFullyLoadedChanged(MTDockPanel panel, bool attached) {
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

    private void OnItemAdded(object sender, int index, (BaseMTElement, MTDockPanel.DockType?) item) {
        Control control = ModToolView.CreateControl(item.Item1);
        if (item.Item2.HasValue)
            SetDock(control, (Dock) item.Item2.Value);
        else
            control.ClearValue(DockProperty);
        this.Children.Insert(index, control);
    }

    private void OnItemRemoved(object sender, int index, (BaseMTElement, MTDockPanel.DockType?) item) {
        this.Children.RemoveAt(index);
    }

    private void OnItemMoved(object sender, int oldIndex, int newIndex, (BaseMTElement, MTDockPanel.DockType?) item) {
        this.Children.Move(oldIndex, newIndex);
    }
}