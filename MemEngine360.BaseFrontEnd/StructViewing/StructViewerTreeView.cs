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
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using MemEngine360.Engine.StructViewing;
using MemEngine360.Engine.StructViewing.ClassBuilding;
using MemEngine360.Engine.StructViewing.Entries;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.BaseFrontEnd.StructViewing;

public sealed class StructViewerTreeView : TreeView {
    public static readonly StyledProperty<StructViewerManager?> StructViewerManagerProperty = AvaloniaProperty.Register<StructViewerTreeView, StructViewerManager?>("StructViewerManager");
    public static readonly StyledProperty<IBrush?> ColumnSeparatorBrushProperty = AvaloniaProperty.Register<StructViewerTreeView, IBrush?>(nameof(ColumnSeparatorBrush));
    
    internal readonly Stack<StructViewerTreeViewItem> itemCache;
    internal readonly ModelControlMap<BaseStructViewerEntry, StructViewerTreeViewItem> itemMap;

    private ObservableItemProcessorIndexing<FieldElement>? collectionChangeListener;
    
    public IModelControlMap<BaseStructViewerEntry, StructViewerTreeViewItem> ItemMap => this.itemMap;

    public StructViewerManager? StructViewerManager {
        get => this.GetValue(StructViewerManagerProperty);
        set => this.SetValue(StructViewerManagerProperty, value);
    }
    
    public IBrush? ColumnSeparatorBrush {
        get => this.GetValue(ColumnSeparatorBrushProperty);
        set => this.SetValue(ColumnSeparatorBrushProperty, value);
    }

    private ScrollViewer? PART_ScrollViewer;

    public StructViewerTreeView() {
        this.itemMap = new ModelControlMap<BaseStructViewerEntry, StructViewerTreeViewItem>();
        this.itemCache = new Stack<StructViewerTreeViewItem>();
        DragDrop.SetAllowDrop(this, true);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_ScrollViewer = e.NameScope.GetTemplateChild<ScrollViewer>(nameof(this.PART_ScrollViewer));
    }

    static StructViewerTreeView() {
        StructViewerManagerProperty.Changed.AddClassHandler<StructViewerTreeView, StructViewerManager?>((o, e) => o.OnATMChanged(e));
    }

    public StructViewerTreeViewItem GetNodeAt(int index) {
        return (StructViewerTreeViewItem) this.Items[index]!;
    }
    
    public void InsertNode(BaseStructViewerEntry item, int index) {
        this.InsertNode(this.GetCachedItemOrNew(), item, index);
    }

    public void InsertNode(StructViewerTreeViewItem control, BaseStructViewerEntry layer, int index) {
        control.OnAdding(this, null, layer);
        this.Items.Insert(index, control);
        this.itemMap.AddMapping(layer, control);
        control.OnAdded();
    }

    public void RemoveNode(int index, bool canCache = true) {
        StructViewerTreeViewItem control = (StructViewerTreeViewItem) this.Items[index]!;
        BaseStructViewerEntry model = control.EntryObject ?? throw new Exception("Expected node to have a resource");
        control.OnRemoving();
        this.Items.RemoveAt(index);
        this.itemMap.RemoveMapping(model, control);
        control.OnRemoved();
        if (canCache)
            this.PushCachedItem(control);
    }

    public void MoveNode(int oldIndex, int newIndex) {
        StructViewerTreeViewItem control = (StructViewerTreeViewItem) this.Items[oldIndex]!;
        this.Items.RemoveAt(oldIndex);
        this.Items.Insert(newIndex, control);
    }

    private void OnATMChanged(AvaloniaPropertyChangedEventArgs<StructViewerManager?> e) {
        this.collectionChangeListener?.RemoveExistingItems();
        this.collectionChangeListener?.Dispose();
        this.collectionChangeListener = null;

        // if (e.TryGetNewValue(out StructViewerManager? newAtm)) {
        //     this.collectionChangeListener = ObservableItemProcessor.MakeIndexable(newAtm.RootClass.Fields, this.OnATMLayerAdded, this.OnATMLayerRemoved, this.OnATMLayerIndexMoved);
        //     this.collectionChangeListener.AddExistingItems();
        // }
    }

    private void OnATMLayerAdded(object sender, int index, BaseStructViewerEntry item) => this.InsertNode(item, index);

    private void OnATMLayerRemoved(object sender, int index, BaseStructViewerEntry item) => this.RemoveNode(index);

    private void OnATMLayerIndexMoved(object sender, int oldIndex, int newIndex, BaseStructViewerEntry item) => this.MoveNode(oldIndex, newIndex);

    public StructViewerTreeViewItem GetCachedItemOrNew() {
        return this.itemCache.Count > 0 ? this.itemCache.Pop() : new StructViewerTreeViewItem();
    }

    public void PushCachedItem(StructViewerTreeViewItem item) {
        if (this.itemCache.Count < 128) {
            this.itemCache.Push(item);
        }
    }
}