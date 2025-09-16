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
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.BaseFrontEnd.SavedAddressing;

public sealed class AddressTableTreeView : TreeView {
    public static readonly StyledProperty<AddressTableManager?> AddressTableManagerProperty = AvaloniaProperty.Register<AddressTableTreeView, AddressTableManager?>("AddressTableManager");
    public static readonly StyledProperty<IBrush?> ColumnSeparatorBrushProperty = AvaloniaProperty.Register<AddressTableTreeView, IBrush?>(nameof(ColumnSeparatorBrush));
    // public static readonly StyledProperty<GridLength> ColumnWidth0Property = AvaloniaProperty.Register<AddressTableTreeView, GridLength>("ColumnWidth0", new GridLength(125));
    // public static readonly StyledProperty<GridLength> ColumnWidth1Property = AvaloniaProperty.Register<AddressTableTreeView, GridLength>("ColumnWidth1", new GridLength(250));
    // public static readonly StyledProperty<GridLength> ColumnWidth2Property = AvaloniaProperty.Register<AddressTableTreeView, GridLength>("ColumnWidth2", new GridLength(75));
    // public static readonly StyledProperty<GridLength> ColumnWidth3Property = AvaloniaProperty.Register<AddressTableTreeView, GridLength>("ColumnWidth3", new GridLength(150));
    
    internal readonly Stack<AddressTableTreeViewItem> itemCache;
    internal readonly ModelControlMap<BaseAddressTableEntry, AddressTableTreeViewItem> itemMap;

    private IDisposable? collectionChangeListener;
    
    public IModelControlMap<BaseAddressTableEntry, AddressTableTreeViewItem> ItemMap => this.itemMap;

    // public GridLength ColumnWidth0 { get => this.GetValue(ColumnWidth0Property); set => this.SetValue(ColumnWidth0Property, value); }
    // public GridLength ColumnWidth1 { get => this.GetValue(ColumnWidth1Property); set => this.SetValue(ColumnWidth1Property, value); }
    // public GridLength ColumnWidth2 { get => this.GetValue(ColumnWidth2Property); set => this.SetValue(ColumnWidth2Property, value); }
    // public GridLength ColumnWidth3 { get => this.GetValue(ColumnWidth3Property); set => this.SetValue(ColumnWidth3Property, value); }
    
    public AddressTableManager? AddressTableManager {
        get => this.GetValue(AddressTableManagerProperty);
        set => this.SetValue(AddressTableManagerProperty, value);
    }
    
    public IBrush? ColumnSeparatorBrush {
        get => this.GetValue(ColumnSeparatorBrushProperty);
        set => this.SetValue(ColumnSeparatorBrushProperty, value);
    }

    private ScrollViewer? PART_ScrollViewer;

    public AddressTableTreeView() {
        this.itemMap = new ModelControlMap<BaseAddressTableEntry, AddressTableTreeViewItem>();
        this.itemCache = new Stack<AddressTableTreeViewItem>();
        DragDrop.SetAllowDrop(this, true);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_ScrollViewer = e.NameScope.GetTemplateChild<ScrollViewer>(nameof(this.PART_ScrollViewer));
    }

    public static void MarkContainerSelected(Control container, bool selected) {
        container.SetCurrentValue(SelectingItemsControl.IsSelectedProperty, selected);
    }

    static AddressTableTreeView() {
        AddressTableManagerProperty.Changed.AddClassHandler<AddressTableTreeView, AddressTableManager?>((o, e) => o.OnATMChanged(e));
        DragDrop.DragOverEvent.AddClassHandler<AddressTableTreeView>((s, e) => s.OnDragOver(e), handledEventsToo: true /* required */);
    }

    private void OnDragOver(DragEventArgs e) {
        Point mPos = e.GetPosition(this);
        const double DragBorderThickness = 15;

        if (mPos.Y < DragBorderThickness) { // scroll up
            this.PART_ScrollViewer?.LineUp();
        }
        else if ((this.Bounds.Height - mPos.Y) < DragBorderThickness) { // scroll down
            this.PART_ScrollViewer?.LineDown();
        }
    }

    public AddressTableTreeViewItem GetNodeAt(int index) {
        return (AddressTableTreeViewItem) this.Items[index]!;
    }
    
    public void InsertNode(BaseAddressTableEntry item, int index) {
        this.InsertNode(this.GetCachedItemOrNew(), item, index);
    }

    public void InsertNode(AddressTableTreeViewItem control, BaseAddressTableEntry layer, int index) {
        control.OnAdding(this, null, layer);
        this.Items.Insert(index, control);
        this.itemMap.AddMapping(layer, control);
        control.OnAdded();
    }

    public void RemoveNode(int index, bool canCache = true) {
        AddressTableTreeViewItem control = (AddressTableTreeViewItem) this.Items[index]!;
        BaseAddressTableEntry model = control.EntryObject ?? throw new Exception("Expected node to have a resource");
        control.OnRemoving();
        this.Items.RemoveAt(index);
        this.itemMap.RemoveMapping(model, control);
        control.OnRemoved();
        if (canCache)
            this.PushCachedItem(control);
    }

    public void MoveNode(int oldIndex, int newIndex) {
        AddressTableTreeViewItem control = (AddressTableTreeViewItem) this.Items[oldIndex]!;
        this.Items.RemoveAt(oldIndex);
        this.Items.Insert(newIndex, control);
    }

    private void OnATMChanged(AvaloniaPropertyChangedEventArgs<AddressTableManager?> e) {
        this.collectionChangeListener?.Dispose();
        this.collectionChangeListener = null;
        if (e.TryGetOldValue(out AddressTableManager? oldAtm)) {
            for (int i = this.Items.Count - 1; i >= 0; i--) {
                this.RemoveNode(i);
            }
        }

        if (e.TryGetNewValue(out AddressTableManager? newAtm)) {
            this.collectionChangeListener = ObservableItemProcessor.MakeIndexable(newAtm.RootEntry.Items, this.OnATMLayerAdded, this.OnATMLayerRemoved, this.OnATMLayerIndexMoved);
            int i = 0;
            foreach (BaseAddressTableEntry layer in newAtm.RootEntry.Items) {
                this.InsertNode(layer, i++);
            }
        }
    }

    private void OnATMLayerAdded(object sender, int index, BaseAddressTableEntry item) => this.InsertNode(item, index);

    private void OnATMLayerRemoved(object sender, int index, BaseAddressTableEntry item) => this.RemoveNode(index);

    private void OnATMLayerIndexMoved(object sender, int oldIndex, int newIndex, BaseAddressTableEntry item) => this.MoveNode(oldIndex, newIndex);

    public AddressTableTreeViewItem GetCachedItemOrNew() {
        return this.itemCache.Count > 0 ? this.itemCache.Pop() : new AddressTableTreeViewItem();
    }

    public void PushCachedItem(AddressTableTreeViewItem item) {
        if (this.itemCache.Count < 128) {
            this.itemCache.Push(item);
        }
    }

    public void SetSelection(AddressTableTreeViewItem item) {
        this.SelectedItems.Clear();
        this.SelectedItems.Add(item);
    }

    public void SetSelection(IEnumerable<AddressTableTreeViewItem> items) {
        this.SelectedItems.Clear();
        foreach (AddressTableTreeViewItem item in items) {
            this.SelectedItems.Add(item);
        }
    }

    public void SetSelection(List<BaseAddressTableEntry> modelItems) {
        this.SelectedItems.Clear();
        foreach (BaseAddressTableEntry item in modelItems) {
            if (this.itemMap.TryGetControl(item, out AddressTableTreeViewItem? control)) {
                control.IsSelected = true;
            }
        }
    }

    public static (List<AddressTableTreeViewItem>?, DropListResult) GetEffectiveDropList(AddressTableTreeViewItem target, List<AddressTableTreeViewItem> source) {
        List<AddressTableTreeViewItem> roots = new List<AddressTableTreeViewItem>();
        foreach (AddressTableTreeViewItem item in source) {
            if (item == target) {
                return (null, DropListResult.DropListIntoSelf);
            }
            
            if (IsParent(target, item)) {
                return (null, DropListResult.DropListIntoDescendentOfList);
            }

            for (int i = roots.Count - 1; i >= 0; i--) {
                if (IsParent(roots[i], item) || IsParent(item, roots[i])) {
                    roots.RemoveAt(i);
                }
            }
            
            roots.Add(item);
        }

        foreach (AddressTableTreeViewItem item in roots) {
            if (item.ParentNode != target) {
                return (roots, DropListResult.Valid);
            }
        }
        
        return (roots, DropListResult.ValidButDropListAlreadyInTarget);
    }

    private static bool IsParent(AddressTableTreeViewItem @this, AddressTableTreeViewItem item) {
        for (AddressTableTreeViewItem? thisOrParent = @this; thisOrParent != null; thisOrParent = thisOrParent.ParentNode) {
            if (thisOrParent == item) {
                return true;
            }
        }

        return false;
    }
}

public enum DropListResult {
    /// <summary>
    /// User tried to drop the items into an item within the drop list
    /// </summary>
    DropListIntoDescendentOfList,
        
    /// <summary>
    /// User tried to drop a single item into itself.
    /// </summary>
    DropListIntoSelf,
        
    /// <summary>
    /// The drop list contains items already present in the drop target
    /// </summary>
    ValidButDropListAlreadyInTarget,
        
    /// <summary>
    /// List contains a valid list of all the highest level tree nodes that can be moved into the target
    /// </summary>
    Valid
}