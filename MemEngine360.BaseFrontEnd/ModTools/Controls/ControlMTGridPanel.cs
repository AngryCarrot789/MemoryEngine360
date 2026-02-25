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
using PFXToolKitUI.Utils.Events;

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
    private readonly BaseControlBindingHelper baseBindingHelper;
    private readonly AttachedModelHelper<MTGridPanel> fullyLoadedHelper;

    protected override Type StyleKeyOverride => typeof(Grid);
    
    public ControlMTGridPanel() {
        this.baseBindingHelper = new BaseControlBindingHelper(this);
        this.fullyLoadedHelper = new AttachedModelHelper<MTGridPanel>(this, this.OnIsFullyLoadedChanged);
    }

    static ControlMTGridPanel() {
        MTGridPanelProperty.Changed.AddClassHandler<ControlMTGridPanel, MTGridPanel?>((s, e) => s.OnMTGridPanelChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnMTGridPanelChanged(MTGridPanel? oldValue, MTGridPanel? newValue) {
        this.fullyLoadedHelper.Model = Optionals.OfNullable(newValue);
    }
    
    private void OnIsFullyLoadedChanged(MTGridPanel panel, bool attached) {
        this.baseBindingHelper.SetModel(attached ? panel : null);
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

    private void OnColumnAdded(ItemAddOrRemoveEventArgs<MTGridPanel.ColumnDefinition> e) {
        this.ColumnDefinitions.Insert(e.Index, new ColumnDefinition(e.Item.Height.Value, (GridUnitType) e.Item.Height.SizeType));
    }

    private void OnColumnRemoved(ItemAddOrRemoveEventArgs<MTGridPanel.ColumnDefinition> e) {
        this.ColumnDefinitions.RemoveAt(e.Index);
    }

    private void OnColumnMoved(ItemMoveEventArgs<MTGridPanel.ColumnDefinition> e) {
        this.ColumnDefinitions.Move(e.OldIndex, e.NewIndex);
    }

    private void OnRowAdded(ItemAddOrRemoveEventArgs<MTGridPanel.RowDefinition> e) {
        this.RowDefinitions.Insert(e.Index, new RowDefinition(e.Item.Height.Value, (GridUnitType) e.Item.Height.SizeType));
    }

    private void OnRowRemoved(ItemAddOrRemoveEventArgs<MTGridPanel.RowDefinition> e) {
        this.RowDefinitions.RemoveAt(e.Index);
    }

    private void OnRowMoved(ItemMoveEventArgs<MTGridPanel.RowDefinition> e) {
        this.RowDefinitions.Move(e.OldIndex, e.NewIndex);
    }

    private void OnItemAdded(ItemAddOrRemoveEventArgs<MTGridPanel.Entry> e) {
        Control control = ModToolView.CreateControl(e.Item.Element);
        SetColumn(control, e.Item.Slot.Column);
        SetRow(control, e.Item.Slot.Row);
        SetColumnSpan(control, e.Item.Span.Columns);
        SetRowSpan(control, e.Item.Span.Rows);
        this.Children.Insert(e.Index, control);
    }

    private void OnItemRemoved(ItemAddOrRemoveEventArgs<MTGridPanel.Entry> e) {
        this.Children.RemoveAt(e.Index);
    }

    private void OnItemMoved(ItemMoveEventArgs<MTGridPanel.Entry> e) {
        this.Children.Move(e.OldIndex, e.NewIndex);
    }
}