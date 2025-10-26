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
using MemEngine360.Engine.StructViewing.Entries;
using PFXToolKitUI.Avalonia.AdvancedMenuService;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.BaseFrontEnd.StructViewing;

public sealed class StructViewerTreeViewItem : TreeViewItem {
    public static readonly StyledProperty<bool> IsDroppableTargetOverProperty = AvaloniaProperty.Register<StructViewerTreeViewItem, bool>(nameof(IsDroppableTargetOver));

    public bool IsDroppableTargetOver {
        get => this.GetValue(IsDroppableTargetOverProperty);
        set => this.SetValue(IsDroppableTargetOverProperty, value);
    }

    public StructViewerTreeView? MyTree { get; private set; }
    public StructViewerTreeViewItem? ParentNode { get; private set; }
    public BaseStructViewerEntry? EntryObject { get; private set; }

    private ObservableItemProcessorIndexing<StructViewerFieldEntry>? compositeListener;

    private TextBlock? PART_Description;
    private TextBlock? PART_AddressTextBlock;
    private TextBlock? PART_DataTypeText;
    private TextBlock? PART_ValueText;

    // private readonly IBinder<StructViewerClassEntry> class_NameBinder = new EventUpdateBinder<StructViewerClassEntry>(nameof(StructViewerClassEntry.NameChanged), (b) => ((TextBlock) b.Control).Text = b.Model.Name);
    // private readonly IBinder<StructViewerFieldEntry> field_NameBinder = new EventUpdateBinder<StructViewerFieldEntry>(nameof(StructViewerFieldEntry.FieldNameChanged), (b) => ((TextBlock) b.Control).Text = b.Model.FieldName);

    public StructViewerTreeViewItem() {
        DragDrop.SetAllowDrop(this, true);
    }

    static StructViewerTreeViewItem() {
    }

    public void Focus() => base.Focus();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_Description = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_Description));
        this.PART_DataTypeText = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_DataTypeText));
        this.PART_AddressTextBlock = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_AddressTextBlock));
        this.PART_ValueText = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_ValueText));

        if (this.EntryObject is StructViewerClassEntry)
            this.PART_AddressTextBlock.Text = "";

        // this.class_NameBinder.AttachControl(this.PART_Description);
        // this.field_NameBinder.AttachControl(this.PART_Description);
    }

    public void OnAdding(StructViewerTreeView tree, StructViewerTreeViewItem? parentNode, BaseStructViewerEntry layer) {
        this.MyTree = tree;
        this.ParentNode = parentNode;
        this.EntryObject = layer;
    }

    public void OnAdded() {
        // if (this.EntryObject is StructViewerClassEntry classType) {
        //     // this.compositeListener = ObservableItemProcessor.MakeIndexable(classType.Fields, this.OnLayerAdded, this.OnLayerRemoved, this.OnLayerMoved);
        //     // this.compositeListener.AddExistingItems();
        //     // this.class_NameBinder.SwitchModel(classType);
        // }
        // else if (this.EntryObject is StructViewerFieldEntry fieldElement) {
        //     // this.field_NameBinder.SwitchModel(fieldElement);
        //     switch (fieldElement.FieldType) {
        //         // case ArrayTypeDescriptor arrayTypeDescriptor:         break;
        //         case ClassTypeDescriptor classTypeDescriptor:
        //             // this.compositeListener = ObservableItemProcessor.MakeIndexable(classTypeDescriptor.StructViewerClassEntry.Fields, this.OnLayerAdded, this.OnLayerRemoved, this.OnLayerMoved);
        //             // this.compositeListener.AddExistingItems();
        //             break;
        //         // case PointerTypeDescriptor pointerTypeDescriptor:     break;
        //         // case PrimitiveTypeDescriptor primitiveTypeDescriptor: break;
        //     }
        // }

        if (this.EntryObject is StructViewerClassEntry klass) {
            if (this.PART_AddressTextBlock != null)
                this.PART_AddressTextBlock!.Text = "";
            if (this.PART_DataTypeText != null)
                this.PART_DataTypeText!.Text = "";
            if (this.PART_ValueText != null)
                this.PART_ValueText!.Text = "";
        }
    }

    public void OnRemoving() {
        this.compositeListener?.RemoveExistingItems();
        this.compositeListener?.Dispose();
        this.compositeListener = null;
        int count = this.Items.Count;
        for (int i = count - 1; i >= 0; i--) {
            this.RemoveNode(i);
        }

        // this.class_NameBinder.SwitchModel(null);
        // this.field_NameBinder.SwitchModel(null);
    }

    public void OnRemoved() {
        this.MyTree = null;
        this.ParentNode = null;
        this.EntryObject = null;
        AdvancedContextMenu.SetContextRegistry(this, null);
    }

    private void OnLayerAdded(object sender, int index, BaseStructViewerEntry item) {
        this.InsertNode(item, index);
    }

    private void OnLayerRemoved(object sender, int index, BaseStructViewerEntry item) {
        this.RemoveNode(index);
    }

    private void OnLayerMoved(object sender, int oldindex, int newindex, BaseStructViewerEntry item) {
        this.MoveNode(oldindex, newindex);
    }

    public StructViewerTreeViewItem GetNodeAt(int index) => (StructViewerTreeViewItem) this.Items[index]!;

    public void InsertNode(BaseStructViewerEntry item, int index) {
        this.InsertNode(null, item, index);
    }

    public void InsertNode(StructViewerTreeViewItem? control, BaseStructViewerEntry layer, int index) {
        StructViewerTreeView? tree = this.MyTree;
        if (tree == null)
            throw new InvalidOperationException("Cannot add children when we have no resource tree associated");
        if (control == null)
            control = tree.GetCachedItemOrNew();

        control.OnAdding(tree, this, layer);
        this.Items.Insert(index, control);
        tree.itemMap.AddMapping(layer, control);
        control.OnAdded();
    }

    public void RemoveNode(int index, bool canCache = true) {
        StructViewerTreeView? tree = this.MyTree;
        if (tree == null)
            throw new InvalidOperationException("Cannot remove children when we have no resource tree associated");

        StructViewerTreeViewItem control = (StructViewerTreeViewItem) this.Items[index]!;
        BaseStructViewerEntry resource = control.EntryObject ?? throw new Exception("Invalid application state");
        control.OnRemoving();
        this.Items.RemoveAt(index);
        tree.itemMap.RemoveMapping(resource, control);
        control.OnRemoved();
        if (canCache)
            tree.PushCachedItem(control);
    }

    public void MoveNode(int oldIndex, int newIndex) {
        StructViewerTreeView? tree = this.MyTree;
        if (tree == null)
            throw new InvalidOperationException("Cannot remove children when we have no resource tree associated");

        StructViewerTreeViewItem control = (StructViewerTreeViewItem) this.Items[oldIndex]!;
        this.Items.RemoveAt(oldIndex);
        this.Items.Insert(newIndex, control);
    }
}