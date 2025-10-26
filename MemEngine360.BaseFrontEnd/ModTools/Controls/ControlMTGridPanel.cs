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
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.BaseFrontEnd.ModTools.Controls;

public class ControlMTGridPanel : Grid {
    public static readonly StyledProperty<MTGridPanel?> MTGridPanelProperty = AvaloniaProperty.Register<ControlMTGridPanel, MTGridPanel?>(nameof(MTGridPanel));

    public MTGridPanel? MTGridPanel {
        get => this.GetValue(MTGridPanelProperty);
        set => this.SetValue(MTGridPanelProperty, value);
    }

    private ObservableItemProcessorIndexing<MTGridPanel.Entry>? itemsProcessor;
    private ObservableItemProcessorIndexing<MTGridPanel.RowDefinition>? rowsProcessor;
    private ObservableItemProcessorIndexing<MTGridPanel.ColumnDefinition>? columnsProcessor;
    private readonly IBinder<MTGridPanel> horzAlignBinder = new EventUpdateBinder<MTGridPanel>(nameof(BaseMTElement.HorizontalAlignmentChanged), (b) => b.Control.HorizontalAlignment = (HorizontalAlignment) b.Model.HorizontalAlignment);
    private readonly IBinder<MTGridPanel> vertAlignBinder = new EventUpdateBinder<MTGridPanel>(nameof(BaseMTElement.VerticalAlignmentChanged), (b) => b.Control.VerticalAlignment = (VerticalAlignment) b.Model.VerticalAlignment);
    private readonly AttachedModelHelper<MTGridPanel> fullyLoadedHelper;

    protected override Type StyleKeyOverride => typeof(Grid);
    
    public ControlMTGridPanel() {
        this.fullyLoadedHelper = new AttachedModelHelper<MTGridPanel>(this, this.OnIsFullyLoadedChanged);
        Binders.AttachControls(this, this.horzAlignBinder, this.vertAlignBinder);
    }

    static ControlMTGridPanel() {
        MTGridPanelProperty.Changed.AddClassHandler<ControlMTGridPanel, MTGridPanel?>((s, e) => s.OnMTGridPanelChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnMTGridPanelChanged(MTGridPanel? oldValue, MTGridPanel? newValue) {
        this.fullyLoadedHelper.Model = Optionals.OfNullable(newValue);
    }
    
    private void OnIsFullyLoadedChanged(MTGridPanel panel, bool attached) {
        this.horzAlignBinder.SwitchModel(attached ? panel : null);
        this.vertAlignBinder.SwitchModel(attached ? panel : null);
        
        if (!attached) {
            this.itemsProcessor!.RemoveExistingItems();
            this.itemsProcessor!.Dispose();
            this.itemsProcessor = null;
            
            this.rowsProcessor!.RemoveExistingItems();
            this.rowsProcessor!.Dispose();
            this.rowsProcessor = null;
            
            this.columnsProcessor!.RemoveExistingItems();
            this.columnsProcessor!.Dispose();
            this.columnsProcessor = null;
        }
        else {
            this.itemsProcessor = ObservableItemProcessor.MakeIndexable(panel.Children, this.OnItemAdded, this.OnItemRemoved, this.OnItemMoved);
            this.itemsProcessor.AddExistingItems();
            
            this.rowsProcessor = ObservableItemProcessor.MakeIndexable(panel.Rows, this.OnRowAdded, this.OnRowRemoved, this.OnRowMoved);
            this.rowsProcessor.AddExistingItems();
            
            this.columnsProcessor = ObservableItemProcessor.MakeIndexable(panel.Columns, this.OnColumnAdded, this.OnColumnRemoved, this.OnColumnMoved);
            this.columnsProcessor.AddExistingItems();
        }
    }

    private void OnColumnAdded(object sender, int index, MTGridPanel.ColumnDefinition item) {
        this.ColumnDefinitions.Insert(index, new ColumnDefinition(item.Height.Value, (GridUnitType) item.Height.SizeType));
    }

    private void OnColumnRemoved(object sender, int index, MTGridPanel.ColumnDefinition item) {
        this.ColumnDefinitions.RemoveAt(index);
    }

    private void OnColumnMoved(object sender, int oldindex, int newindex, MTGridPanel.ColumnDefinition item) {
        this.ColumnDefinitions.Move(oldindex, newindex);
    }

    private void OnRowAdded(object sender, int index, MTGridPanel.RowDefinition item) {
        this.RowDefinitions.Insert(index, new RowDefinition(item.Height.Value, (GridUnitType) item.Height.SizeType));
    }

    private void OnRowRemoved(object sender, int index, MTGridPanel.RowDefinition item) {
        this.RowDefinitions.RemoveAt(index);
    }

    private void OnRowMoved(object sender, int oldindex, int newindex, MTGridPanel.RowDefinition item) {
        this.RowDefinitions.Move(oldindex, newindex);
    }

    private void OnItemAdded(object sender, int index, MTGridPanel.Entry entry) {
        Control control = ModToolView.CreateControl(entry.Element);
        SetColumn(control, entry.Slot.Column);
        SetRow(control, entry.Slot.Row);
        SetColumnSpan(control, entry.Span.Columns);
        SetRowSpan(control, entry.Span.Rows);
        this.Children.Insert(index, control);
    }

    private void OnItemRemoved(object sender, int index, MTGridPanel.Entry entry) {
        this.Children.RemoveAt(index);
    }

    private void OnItemMoved(object sender, int oldIndex, int newIndex, MTGridPanel.Entry entry) {
        this.Children.Move(oldIndex, newIndex);
    }
}