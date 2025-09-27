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
using MemEngine360.Engine.FileBrowsing;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.BaseFrontEnd.FileBrowsing;

public sealed class FileBrowserTreeView : TreeView {
    public static readonly StyledProperty<FileTreeExplorer?> FileTreeExplorerProperty = AvaloniaProperty.Register<FileBrowserTreeView, FileTreeExplorer?>("FileTreeManager");
    public static readonly StyledProperty<IBrush?> ColumnSeparatorBrushProperty = AvaloniaProperty.Register<FileBrowserTreeView, IBrush?>(nameof(ColumnSeparatorBrush));
    
    internal readonly Stack<FileBrowserTreeViewItem> itemCache;
    internal readonly ModelControlMap<BaseFileTreeNode, FileBrowserTreeViewItem> itemMap;

    private IDisposable? collectionChangeListener;
    
    public IModelControlMap<BaseFileTreeNode, FileBrowserTreeViewItem> ItemMap => this.itemMap;

    public FileTreeExplorer? FileTreeExplorer {
        get => this.GetValue(FileTreeExplorerProperty);
        set => this.SetValue(FileTreeExplorerProperty, value);
    }
    
    public IBrush? ColumnSeparatorBrush {
        get => this.GetValue(ColumnSeparatorBrushProperty);
        set => this.SetValue(ColumnSeparatorBrushProperty, value);
    }

    private ScrollViewer? PART_ScrollViewer;

    public FileBrowserTreeView() {
        this.itemMap = new ModelControlMap<BaseFileTreeNode, FileBrowserTreeViewItem>();
        this.itemCache = new Stack<FileBrowserTreeViewItem>();
        DragDrop.SetAllowDrop(this, true);
#if DEBUG
        if (Design.IsDesignMode) {
            this.FileTreeExplorer = FileTreeExplorer.DummyInstance_UITest;
        }
#endif
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_ScrollViewer = e.NameScope.GetTemplateChild<ScrollViewer>(nameof(this.PART_ScrollViewer));
    }

    public static void MarkContainerSelected(Control container, bool selected) {
        container.SetCurrentValue(SelectingItemsControl.IsSelectedProperty, selected);
    }

    static FileBrowserTreeView() {
        FileTreeExplorerProperty.Changed.AddClassHandler<FileBrowserTreeView, FileTreeExplorer?>((o, e) => o.OnATMChanged(e));
        DragDrop.DragOverEvent.AddClassHandler<FileBrowserTreeView>((s, e) => s.OnDragOver(e), handledEventsToo: true /* required */);
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

    public FileBrowserTreeViewItem GetNodeAt(int index) {
        return (FileBrowserTreeViewItem) this.Items[index]!;
    }
    
    public void InsertNode(BaseFileTreeNode item, int index) {
        this.InsertNode(this.GetCachedItemOrNew(), item, index);
    }

    public void InsertNode(FileBrowserTreeViewItem control, BaseFileTreeNode layer, int index) {
        control.OnAdding(this, null, layer);
        this.Items.Insert(index, control);
        this.itemMap.AddMapping(layer, control);
        control.OnAdded();
    }

    public void RemoveNode(int index, bool canCache = true) {
        FileBrowserTreeViewItem control = (FileBrowserTreeViewItem) this.Items[index]!;
        BaseFileTreeNode model = control.EntryObject ?? throw new Exception("Expected node to have a resource");
        control.OnRemoving();
        this.Items.RemoveAt(index);
        this.itemMap.RemoveMapping(model, control);
        control.OnRemoved();
        if (canCache)
            this.PushCachedItem(control);
    }

    public void MoveNode(int oldIndex, int newIndex) {
        FileBrowserTreeViewItem control = (FileBrowserTreeViewItem) this.Items[oldIndex]!;
        this.Items.RemoveAt(oldIndex);
        this.Items.Insert(newIndex, control);
    }

    private void OnATMChanged(AvaloniaPropertyChangedEventArgs<FileTreeExplorer?> e) {
        this.collectionChangeListener?.Dispose();
        this.collectionChangeListener = null;
        if (e.TryGetOldValue(out FileTreeExplorer? oldAtm)) {
            for (int i = this.Items.Count - 1; i >= 0; i--) {
                this.RemoveNode(i);
            }
        }

        if (e.TryGetNewValue(out FileTreeExplorer? newAtm)) {
            this.collectionChangeListener = ObservableItemProcessor.MakeIndexable(newAtm.RootEntry.Items, this.OnATMLayerAdded, this.OnATMLayerRemoved, this.OnATMLayerIndexMoved);
            int i = 0;
            foreach (BaseFileTreeNode layer in newAtm.RootEntry.Items) {
                this.InsertNode(layer, i++);
            }
        }
    }

    private void OnATMLayerAdded(object sender, int index, BaseFileTreeNode item) => this.InsertNode(item, index);

    private void OnATMLayerRemoved(object sender, int index, BaseFileTreeNode item) => this.RemoveNode(index);

    private void OnATMLayerIndexMoved(object sender, int oldIndex, int newIndex, BaseFileTreeNode item) => this.MoveNode(oldIndex, newIndex);

    public FileBrowserTreeViewItem GetCachedItemOrNew() {
        return this.itemCache.Count > 0 ? this.itemCache.Pop() : new FileBrowserTreeViewItem();
    }

    public void PushCachedItem(FileBrowserTreeViewItem item) {
        if (this.itemCache.Count < 128) {
            this.itemCache.Push(item);
        }
    }

    public void SetSelection(FileBrowserTreeViewItem item) {
        this.SelectedItems.Clear();
        this.SelectedItems.Add(item);
    }

    public void SetSelection(IEnumerable<FileBrowserTreeViewItem> items) {
        this.SelectedItems.Clear();
        foreach (FileBrowserTreeViewItem item in items) {
            this.SelectedItems.Add(item);
        }
    }

    public void SetSelection(List<BaseFileTreeNode> modelItems) {
        this.SelectedItems.Clear();
        foreach (BaseFileTreeNode item in modelItems) {
            if (this.itemMap.TryGetControl(item, out FileBrowserTreeViewItem? control)) {
                control.IsSelected = true;
            }
        }
    }

    public static (List<FileBrowserTreeViewItem>?, DropListResult) GetEffectiveDropList(FileBrowserTreeViewItem target, List<FileBrowserTreeViewItem> source) {
        List<FileBrowserTreeViewItem> roots = new List<FileBrowserTreeViewItem>();
        foreach (FileBrowserTreeViewItem item in source) {
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

        foreach (FileBrowserTreeViewItem item in roots) {
            if (item.ParentNode != target) {
                return (roots, DropListResult.Valid);
            }
        }
        
        return (roots, DropListResult.ValidButDropListAlreadyInTarget);
    }

    private static bool IsParent(FileBrowserTreeViewItem @this, FileBrowserTreeViewItem item) {
        for (FileBrowserTreeViewItem? thisOrParent = @this; thisOrParent != null; thisOrParent = thisOrParent.ParentNode) {
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