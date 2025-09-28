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
using Avalonia.Interactivity;
using MemEngine360.Engine.FileBrowsing;
using MemEngine360.Engine.Scanners;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Avalonia.AdvancedMenuService;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.FileBrowsing;

public sealed class FileBrowserTreeViewItem : TreeViewItem {
    public static readonly DirectProperty<FileBrowserTreeViewItem, bool> IsFolderItemProperty = AvaloniaProperty.RegisterDirect<FileBrowserTreeViewItem, bool>("IsFolderItem", o => o.IsFolderItem, null);
    public static readonly StyledProperty<bool> IsDroppableTargetOverProperty = AvaloniaProperty.Register<FileBrowserTreeViewItem, bool>(nameof(IsDroppableTargetOver));
    public static readonly StyledProperty<bool> HasContentsLoadedProperty = AvaloniaProperty.Register<FileBrowserTreeViewItem, bool>(nameof(HasContentsLoaded));

    public bool IsDroppableTargetOver {
        get => this.GetValue(IsDroppableTargetOverProperty);
        set => this.SetValue(IsDroppableTargetOverProperty, value);
    }

    public bool HasContentsLoaded {
        get => this.GetValue(HasContentsLoadedProperty);
        set => this.SetValue(HasContentsLoadedProperty, value);
    }

    public FileBrowserTreeView? MyTree { get; private set; }
    public FileBrowserTreeViewItem? ParentNode { get; private set; }
    public BaseFileTreeNode? EntryObject { get; private set; }

    public bool IsFolderItem {
        get => this.isFolderItem;
        private set => this.SetAndRaise(IsFolderItemProperty, ref this.isFolderItem, value);
    }

    private readonly IBinder<BaseFileTreeNode> fileNameBinder = new EventUpdateBinder<BaseFileTreeNode>(nameof(BaseFileTreeNode.FileNameChanged), b => b.Control.SetValue(HeaderProperty, b.Model.FileName));
    private readonly IBinder<BaseFileTreeNode> fileSizeBinder = new EventUpdateBinder<BaseFileTreeNode>(nameof(BaseFileTreeNode.SizeChanged), b => b.Control.SetValue(TextBlock.TextProperty, b.Model.IsTopLevelEntry || !(b.Model is FileTreeNodeDirectory) ? ValueScannerUtils.ByteFormatter.ToString(b.Model.Size, false) : ""));
    private readonly IBinder<BaseFileTreeNode> dateCreatedBinder = new EventUpdateBinder<BaseFileTreeNode>(nameof(BaseFileTreeNode.CreationTimeUtcChanged), b => b.Control.SetValue(TextBlock.TextProperty, !b.Model.IsTopLevelEntry ? b.Model.CreationTimeUtc.ToString() : ""));
    private readonly IBinder<BaseFileTreeNode> dateModifiedBinder = new EventUpdateBinder<BaseFileTreeNode>(nameof(BaseFileTreeNode.ModifiedTimeUtcChanged), b => b.Control.SetValue(TextBlock.TextProperty, !b.Model.IsTopLevelEntry ? b.Model.ModifiedTimeUtc.ToString() : ""));
    private ObservableItemProcessorIndexing<BaseFileTreeNode>? compositeListener;
    private Border? PART_DragDropMoveBorder;
    private bool isFolderItem;

    private TextBlock? PART_DateCreated;
    private TextBlock? PART_FileName;
    private TextBlock? PART_DateModified;
    private TextBlock? PART_FileType;
    private TextBlock? PART_FileSize;

    private readonly AsyncRelayCommand LoadContentsCommand;

    public FileBrowserTreeViewItem() {
        DragDrop.SetAllowDrop(this, true);
        this.Expanded += this.OnExpanded;
        this.Collapsed += this.OnCollapsed;
        this.LoadContentsCommand = new AsyncRelayCommand(async () => {
            if (!(this.EntryObject is FileTreeNodeDirectory dir)) {
                return;
            }

            if (dir.ParentDirectory == null || dir.HasLoadedContents) {
                return;
            }

            this.IsEnabled = false;

            FileTreeExplorerViewState state = FileTreeExplorerViewState.GetInstance(dir.FileTreeManager!);
            state.TreeSelection.DeselectHierarchy(dir);

            await CommandManager.Instance.RunActionAsync((e) => state.Explorer.LoadContentsCommand(dir), DataManager.GetFullContextData(this));

            this.IsEnabled = true;
        });
    }

    private void OnCollapsed(object? sender, RoutedEventArgs e) {
        if (this.EntryObject?.FileTreeManager == null) {
            return;
        }
        
        FileTreeExplorerViewState state = FileTreeExplorerViewState.GetInstance(this.EntryObject!.FileTreeManager!);
        state.TreeSelection.DeselectHierarchy(this.EntryObject);
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.F5 && this.EntryObject is FileTreeNodeDirectory dir && !this.LoadContentsCommand.IsRunning) {
            e.Handled = true;
            dir.HasLoadedContents = false;
            this.LoadContentsCommand.Execute(null);
        }
    }

    private void OnExpanded(object? sender, RoutedEventArgs e) {
        if (this.EntryObject != null && this.IsExpanded) {
            this.LoadContentsCommand.Execute(null);
            e.Handled = true;
        }
    }

    public void Focus() => base.Focus();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_DragDropMoveBorder = e.NameScope.GetTemplateChild<Border>(nameof(this.PART_DragDropMoveBorder));
        this.PART_FileName = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_FileName));
        this.PART_DateModified = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_DateModified));
        this.PART_DateCreated = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_DateCreated));
        this.PART_FileType = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_FileType));
        this.PART_FileSize = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_FileSize));

        this.fileNameBinder.AttachControl(this);
        this.fileSizeBinder.AttachControl(this.PART_FileSize);
        this.dateCreatedBinder.AttachControl(this.PART_DateCreated);
        this.dateModifiedBinder.AttachControl(this.PART_DateModified);
    }

    public void OnAdding(FileBrowserTreeView tree, FileBrowserTreeViewItem? parentNode, BaseFileTreeNode layer) {
        this.MyTree = tree;
        this.ParentNode = parentNode;
        this.EntryObject = layer;
        this.IsFolderItem = layer is FileTreeNodeDirectory;
        this.ClearValue(IsSelectedProperty);
    }

    public void OnAdded() {
        if (this.EntryObject is FileTreeNodeDirectory folder) {
            this.compositeListener = ObservableItemProcessor.MakeIndexable(folder.Items, this.OnLayerAdded, this.OnLayerRemoved, this.OnLayerMoved);
            this.compositeListener.AddExistingItems();
            // int i = 0;
            // foreach (BaseFileTreeNode item in folder.Items) {
            //     this.InsertNode(item, i++);
            // }
        }

        Binders.AttachModels(this.EntryObject!, this.fileNameBinder, this.fileSizeBinder, this.dateCreatedBinder, this.dateModifiedBinder);
        DataManager.GetContextData(this).Set(BaseFileTreeNode.DataKey, this.EntryObject!);
        AdvancedContextMenu.SetContextRegistry(this, AddressTableContextRegistry.Registry);
    }

    public void OnRemoving() {
        this.compositeListener?.RemoveExistingItems();
        this.compositeListener?.Dispose();
        this.compositeListener = null;
        // int count = this.Items.Count;
        // for (int i = count - 1; i >= 0; i--) {
        //     this.RemoveNode(i);
        // }

        Binders.DetachModels(this.fileNameBinder, this.fileSizeBinder, this.dateCreatedBinder, this.dateModifiedBinder);
        DataManager.GetContextData(this).Remove(BaseFileTreeNode.DataKey);
    }

    public void OnRemoved() {
        this.MyTree = null;
        this.ParentNode = null;
        this.EntryObject = null;
        this.IsFolderItem = false;
        AdvancedContextMenu.SetContextRegistry(this, null);
    }

    private void OnLayerAdded(object sender, int index, BaseFileTreeNode item) {
        this.InsertNode(item, index);
    }

    private void OnLayerRemoved(object sender, int index, BaseFileTreeNode item) {
        this.RemoveNode(index);
    }

    private void OnLayerMoved(object sender, int oldIndex, int newIndex, BaseFileTreeNode item) {
        this.MoveNode(oldIndex, newIndex);
    }

    public FileBrowserTreeViewItem GetNodeAt(int index) => (FileBrowserTreeViewItem) this.Items[index]!;

    public void InsertNode(BaseFileTreeNode item, int index) {
        this.InsertNode(null, item, index);
    }

    public void InsertNode(FileBrowserTreeViewItem? control, BaseFileTreeNode layer, int index) {
        FileBrowserTreeView? tree = this.MyTree;
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
        FileBrowserTreeView? tree = this.MyTree;
        if (tree == null)
            throw new InvalidOperationException("Cannot remove children when we have no resource tree associated");

        FileBrowserTreeViewItem control = (FileBrowserTreeViewItem) this.Items[index]!;
        BaseFileTreeNode resource = control.EntryObject ?? throw new Exception("Invalid application state");
        control.OnRemoving();
        this.Items.RemoveAt(index);
        tree.itemMap.RemoveMapping(resource, control);
        control.OnRemoved();
        if (canCache)
            tree.PushCachedItem(control);
    }

    public void MoveNode(int oldIndex, int newIndex) {
        FileBrowserTreeView? tree = this.MyTree;
        if (tree == null)
            throw new InvalidOperationException("Cannot remove children when we have no resource tree associated");

        FileBrowserTreeViewItem control = (FileBrowserTreeViewItem) this.Items[oldIndex]!;
        this.Items.RemoveAt(oldIndex);
        this.Items.Insert(newIndex, control);
    }
}

public static class AddressTableContextRegistry {
    public static readonly ContextRegistry Registry = new ContextRegistry("File");

    static AddressTableContextRegistry() {
        // FixedContextGroup modEdit = Registry.GetFixedGroup("modify.edit");
        FixedContextGroup modGeneric = Registry.GetFixedGroup("modify.general");
        modGeneric.AddCommand("commands.memengine.RenameFileCommand", "Rename", "Rename this item");
        modGeneric.AddSeparator();
        modGeneric.AddCommand("commands.memengine.LaunchFileCommand", "Launch File", "Launches the file");
        modGeneric.AddSeparator();
        modGeneric.AddCommand("commands.memengine.DeleteFilesCommand", "Delete", "Deletes the selected items(s)");
        // modEdit.AddHeader("Modify");
        // modEdit.AddCommand("commands.memengine.EditSavedAddressAddressCommand", "Edit Address");
        // modEdit.AddCommand("commands.memengine.EditSavedAddressValueCommand", "Edit Value");
        // modEdit.AddCommand("commands.memengine.EditSavedAddressDataTypeCommand", "Edit Data Type");
        // modEdit.AddCommand("commands.memengine.EditSavedAddressDescriptionCommand", "Edit Description");
        // modGeneric.AddHeader("General");
        // modGeneric.AddCommand("commands.memengine.CopyAddressToClipboardCommand", "Copy Address");
        // modGeneric.AddCommand("commands.memengine.CopyAbsoluteAddressToClipboardCommand", "Copy Absolute Address");
        // modGeneric.AddCommand("commands.memengine.CopySavedAddressValuesToClipboardCommand", "Copy Value");
        // modGeneric.AddCommand("commands.memengine.RefreshSavedAddressesCommand", "Refresh");
        // modGeneric.AddCommand("commands.meengine.ToggleSavedAddressAutoRefreshCommand", "Toggle Enabled");
        // modGeneric.AddSeparator();
        // modGeneric.AddCommand("commands.memengine.GroupEntriesCommand", "Group");
        // modGeneric.AddCommand("commands.memengine.DuplicateSelectedSavedAddressesCommand", "Duplicate");
        // modGeneric.AddSeparator();
        // modGeneric.AddCommand("commands.memengine.DeleteSelectedSavedAddressesCommand", "Delete");
    }
}