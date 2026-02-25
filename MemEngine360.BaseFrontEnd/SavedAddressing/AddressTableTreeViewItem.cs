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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using MemEngine360.Engine;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.Engine.View;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Avalonia.AdvancedMenuService;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.BaseFrontEnd.SavedAddressing;

public sealed class AddressTableTreeViewItem : TreeViewItem {
    public static readonly DirectProperty<AddressTableTreeViewItem, bool> IsFolderItemProperty = AvaloniaProperty.RegisterDirect<AddressTableTreeViewItem, bool>("IsFolderItem", o => o.IsFolderItem, null);
    public static readonly StyledProperty<bool> IsDroppableTargetOverProperty = AvaloniaProperty.Register<AddressTableTreeViewItem, bool>(nameof(IsDroppableTargetOver));

    public bool IsDroppableTargetOver {
        get => this.GetValue(IsDroppableTargetOverProperty);
        set => this.SetValue(IsDroppableTargetOverProperty, value);
    }

    public AddressTableTreeView? MyTree { get; private set; }
    public AddressTableTreeViewItem? ParentNode { get; private set; }
    public BaseAddressTableEntry? EntryObject { get; private set; }

    public bool IsFolderItem {
        get => field;
        private set => this.SetAndRaise(IsFolderItemProperty, ref field, value);
    }

    private readonly IBinder<BaseAddressTableEntry> descriptionBinder = new EventUpdateBinder<BaseAddressTableEntry>(nameof(BaseAddressTableEntry.DescriptionChanged), b => b.Control.SetValue(HeaderProperty, b.Model.Description));

    private readonly IBinder<AddressTableEntry> entryAddressBinder = new EventUpdateBinder<AddressTableEntry>(nameof(AddressTableEntry.MemoryAddressChanged), b => {
        b.Control.SetValue(TextBlock.TextProperty, b.Model.MemoryAddress.ToString());
    });

    private readonly IBinder<AddressTableEntry> dataTypeTextBinder = new EventUpdateBinder<AddressTableEntry>(nameof(AddressTableEntry.DataTypeChanged), b => b.Control.SetValue(TextBlock.TextProperty, b.Model.DataType.ToString()));
    private readonly IBinder<AddressTableEntry> valueTextBinder = new EventUpdateBinder<AddressTableEntry>([nameof(AddressTableEntry.ValueChanged), nameof(AddressTableEntry.NumericDisplayTypeChanged), nameof(AddressTableEntry.DataTypeChanged)], b => b.Control.SetValue(TextBlock.TextProperty, b.Model.Value != null ? DataValueUtils.GetStringFromDataValue(b.Model, b.Model.Value, putStringInQuotes: true) : ""));
    private ObservableItemProcessorIndexing<BaseAddressTableEntry>? compositeListener;
    private Border? PART_DragDropMoveBorder;

    private TextBlock? PART_AddressTextBlock;
    private TextBlock? PART_Description;
    private TextBlock? PART_DataTypeText;
    private TextBlock? PART_ValueText;

    private readonly DataManagerCommandWrapper EditAddressCommand, EditDataTypeCommand;

    private NodeDragState dragBtnState;
    private bool hasCompletedDrop;
    private bool wasSelectedOnPress;
    private Point clickMousePoint;
    private bool isProcessingAsyncDrop;

    private DispatcherTimer? expandForDragOverTimer;

    public AddressTableTreeViewItem() {
        this.EditAddressCommand = new DataManagerCommandWrapper(this, "commands.memengine.EditSavedAddressAddressCommand", false);
        this.EditDataTypeCommand = new DataManagerCommandWrapper(this, "commands.memengine.EditSavedAddressDataTypeCommand", false);
        DragDrop.SetAllowDrop(this, true);
    }

    static AddressTableTreeViewItem() {
        DragDrop.DragEnterEvent.AddClassHandler<AddressTableTreeViewItem>((o, e) => o.OnDragEnter(e));
        DragDrop.DragOverEvent.AddClassHandler<AddressTableTreeViewItem>((o, e) => o.OnDragOver(e));
        DragDrop.DragLeaveEvent.AddClassHandler<AddressTableTreeViewItem>((o, e) => o.OnDragLeave(e));
        DragDrop.DropEvent.AddClassHandler<AddressTableTreeViewItem>((o, e) => o.OnDrop(e));
    }

    public void Focus() => base.Focus();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_DragDropMoveBorder = e.NameScope.GetTemplateChild<Border>(nameof(this.PART_DragDropMoveBorder));
        this.PART_Description = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_Description));
        this.PART_DataTypeText = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_DataTypeText));
        this.PART_AddressTextBlock = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_AddressTextBlock));
        this.PART_ValueText = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_ValueText));

        // Handle pointer press on double or more click, so that it doesn't expand/collapse this tree item
        this.PART_AddressTextBlock.DoubleTapped += (s, args) => this.EditAddressCommand.Execute(null);
        this.PART_AddressTextBlock.PointerPressed += (s, args) => {
            if (args.ClickCount > 1)
                args.Handled = true;
        };

        this.PART_DataTypeText.DoubleTapped += (s, args) => this.EditDataTypeCommand.Execute(null);
        this.PART_DataTypeText.PointerPressed += (s, args) => {
            if (args.ClickCount > 1)
                args.Handled = true;
        };

        this.descriptionBinder.AttachControl(this);
        this.entryAddressBinder.AttachControl(this.PART_AddressTextBlock);
        this.dataTypeTextBinder.AttachControl(this.PART_DataTypeText);
        this.valueTextBinder.AttachControl(this.PART_ValueText);
        if (this.EntryObject is AddressTableGroupEntry)
            this.PART_AddressTextBlock.Text = "";
    }

    public void OnAdding(AddressTableTreeView tree, AddressTableTreeViewItem? parentNode, BaseAddressTableEntry layer) {
        this.MyTree = tree;
        this.ParentNode = parentNode;
        this.EntryObject = layer;
        this.IsFolderItem = layer is AddressTableGroupEntry;
    }

    public void OnAdded() {
        if (this.EntryObject is AddressTableGroupEntry folder) {
            this.compositeListener = ObservableItemProcessor.MakeIndexable(folder.Items, this.OnLayerAdded, this.OnLayerRemoved, this.OnLayerMoved);
            int i = 0;
            foreach (BaseAddressTableEntry item in folder.Items) {
                this.InsertNode(item, i++);
            }
        }

        this.descriptionBinder.AttachModel(this.EntryObject!);
        if (this.EntryObject is AddressTableGroupEntry group) {
            if (this.PART_AddressTextBlock != null)
                this.PART_AddressTextBlock!.Text = "";
            if (this.PART_DataTypeText != null)
                this.PART_DataTypeText!.Text = "";
            if (this.PART_ValueText != null)
                this.PART_ValueText!.Text = "";
            this.OnIsAutoRefreshEnabledChanged(null, EventArgs.Empty);
        }

        if (this.EntryObject is AddressTableEntry entry) {
            this.entryAddressBinder.AttachModel(entry);
            this.dataTypeTextBinder.AttachModel(entry);
            this.valueTextBinder.AttachModel(entry);
            entry.IsAutoRefreshEnabledChanged += this.OnIsAutoRefreshEnabledChanged;
            this.OnIsAutoRefreshEnabledChanged(entry, EventArgs.Empty);
        }

        DataManager.GetContextData(this).Set(BaseAddressTableEntry.DataKey, this.EntryObject!);
        AdvancedContextMenu.SetContextRegistry(this, AddressTableContextRegistry.Registry);
    }

    private void OnIsAutoRefreshEnabledChanged(object? o, EventArgs eventArgs) {
        AddressTableEntry? sender = (AddressTableEntry?) o;
        if (sender == null || sender.IsAutoRefreshEnabled) {
            this.Opacity = 1.0;
        }
        else {
            this.Opacity = 0.4;
        }
    }

    public void OnRemoving() {
        this.compositeListener?.Dispose();
        int count = this.Items.Count;
        for (int i = count - 1; i >= 0; i--) {
            this.RemoveNode(i);
        }

        this.descriptionBinder.DetachModel();
        if (this.entryAddressBinder.HasModel) {
            this.entryAddressBinder.DetachModel();
            this.dataTypeTextBinder.DetachModel();
            this.valueTextBinder.DetachModel();
        }

        if (this.EntryObject is AddressTableEntry entry) {
            entry.IsAutoRefreshEnabledChanged -= this.OnIsAutoRefreshEnabledChanged;
        }

        DataManager.GetContextData(this).Remove(BaseAddressTableEntry.DataKey);
    }

    public void OnRemoved() {
        this.MyTree = null;
        this.ParentNode = null;
        this.EntryObject = null;
        this.IsFolderItem = false;
        AdvancedContextMenu.SetContextRegistry(this, null);
    }

    private void OnLayerAdded(ItemAddOrRemoveEventArgs<BaseAddressTableEntry> e) => this.InsertNode(e.Item, e.Index);

    private void OnLayerRemoved(ItemAddOrRemoveEventArgs<BaseAddressTableEntry> e) => this.RemoveNode(e.Index);

    private void OnLayerMoved(ItemMoveEventArgs<BaseAddressTableEntry> e) => this.MoveNode(e.OldIndex, e.NewIndex);

    public AddressTableTreeViewItem GetNodeAt(int index) => (AddressTableTreeViewItem) this.Items[index]!;

    public void InsertNode(BaseAddressTableEntry item, int index) {
        this.InsertNode(null, item, index);
    }

    public void InsertNode(AddressTableTreeViewItem? control, BaseAddressTableEntry layer, int index) {
        AddressTableTreeView? tree = this.MyTree;
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
        AddressTableTreeView? tree = this.MyTree;
        if (tree == null)
            throw new InvalidOperationException("Cannot remove children when we have no resource tree associated");

        AddressTableTreeViewItem control = (AddressTableTreeViewItem) this.Items[index]!;
        BaseAddressTableEntry resource = control.EntryObject ?? throw new Exception("Invalid application state");
        control.OnRemoving();
        this.Items.RemoveAt(index);
        tree.itemMap.RemoveMapping(resource, control);
        control.OnRemoved();
        if (canCache)
            tree.PushCachedItem(control);
    }

    public void MoveNode(int oldIndex, int newIndex) {
        AddressTableTreeView? tree = this.MyTree;
        if (tree == null)
            throw new InvalidOperationException("Cannot remove children when we have no resource tree associated");

        AddressTableTreeViewItem control = (AddressTableTreeViewItem) this.Items[oldIndex]!;
        this.Items.RemoveAt(oldIndex);
        this.Items.Insert(newIndex, control);
    }

    #region Drag Drop

    private void ResetDragDropState(PointerEventArgs e) {
        this.dragBtnState = NodeDragState.None;
        if (this == e.Pointer.Captured) {
            e.Pointer.Capture(null);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);
        if (e.Handled || this.EntryObject == null || this.MyTree == null) {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(this);
        PointerUpdateKind updateKind = point.Properties.PointerUpdateKind;

        if (updateKind == PointerUpdateKind.LeftButtonPressed) {
            bool isToggle = (e.KeyModifiers & KeyModifiers.Control) != 0;
            if ((e.ClickCount % 2) == 0) {
                if (!isToggle) {
                    this.SetCurrentValue(IsExpandedProperty, !this.IsExpanded);
                    e.Handled = true;
                }
            }
            else if (e.KeyModifiers == KeyModifiers.None || e.KeyModifiers == KeyModifiers.Control) {
                if (this.dragBtnState == NodeDragState.None && (this.IsFocused || this.Focus(NavigationMethod.Pointer, e.KeyModifiers))) {
                    this.dragBtnState = NodeDragState.Initiated;
                    e.Pointer.Capture(this);
                    this.clickMousePoint = point.Position;
                    this.wasSelectedOnPress = this.IsSelected;
                    if (isToggle) {
                        if (this.wasSelectedOnPress) {
                            // do nothing; toggle selection in mouse release
                        }
                        else {
                            this.SetCurrentValue(IsSelectedProperty, true);
                        }
                    }
                    else if (!this.wasSelectedOnPress || this.MyTree!.SelectedItems.Count < 2) {
                        // Set as only selection if 0 or 1 items selected, or we aren't selected
                        this.MyTree!.SetSelection(this);
                    }
                }

                // handle to stop tree view from selecting stuff
                e.Handled = true;
            }
        }
        else if (updateKind == PointerUpdateKind.RightButtonPressed) {
            if (!this.IsSelected) {
                this.MyTree!.UnselectAll();
                this.IsSelected = true;
            }

            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e) {
        base.OnPointerReleased(e);
        PointerPoint point = e.GetCurrentPoint(this);
        if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased) {
            NodeDragState lastDragState = this.dragBtnState;
            if (lastDragState != NodeDragState.None) {
                this.ResetDragDropState(e);
                if (this.MyTree != null) {
                    bool isToggle = (e.KeyModifiers & KeyModifiers.Control) != 0;
                    int selCount = this.MyTree.SelectedItems.Count;
                    if (selCount == 0) {
                        // very rare scenario, shouldn't really occur
                        this.MyTree.SetSelection(this);
                    }
                    else if (isToggle && this.wasSelectedOnPress && lastDragState != NodeDragState.Completed) {
                        // Check we want to toggle, check we were selected on click and we probably are still selected,
                        // and also check that the last drag wasn't completed/cancelled just because it feels more normal that way
                        this.SetCurrentValue(IsSelectedProperty, false);
                    }
                    else if (selCount > 1 && !isToggle && lastDragState != NodeDragState.Completed) {
                        this.MyTree.SetSelection(this);
                    }
                }
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e) {
        base.OnPointerMoved(e);
        PointerPoint point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) {
            this.ResetDragDropState(e);
            this.clickMousePoint = new Point(0, 0);
            return;
        }

        if (this.dragBtnState != NodeDragState.Initiated || this.MyTree == null) {
            return;
        }

        if (this.EntryObject == null || this.EntryObject.AddressTableManager == null) {
            return;
        }

        Point mPos = point.Position;
        Point clickPos = this.clickMousePoint;
        Point change = new Point(Math.Abs(mPos.X - clickPos.X), Math.Abs(mPos.X - clickPos.X));
        if (change.X > 4 || change.Y > 4) {
            this.IsSelected = true;
            List<AddressTableTreeViewItem> selection = this.MyTree.SelectedItems.Cast<AddressTableTreeViewItem>().ToList();
            if (selection.Count < 1 || !selection.Contains(this)) {
                this.ResetDragDropState(e);
                return;
            }

            try {
                DataObject obj = new DataObject();
                obj.Set(DropKey, selection);

                this.dragBtnState = NodeDragState.Active;
                Task<DragDropEffects> task = DragDrop.DoDragDrop(e, obj, DragDropEffects.Move | DragDropEffects.Copy);
                Debug.Assert(task.IsCompleted);
            }
            catch (Exception ex) {
                Debug.WriteLine("Exception while executing resource tree item drag drop: " + ex.GetToString());
            }
            finally {
                this.dragBtnState = NodeDragState.Completed;
                if (this.hasCompletedDrop) {
                    this.hasCompletedDrop = false;
                    // this.IsSelected = false;
                }
            }
        }
    }

    public const string DropKey = "MemoryEngine.ATEItem.Drop";

    private DropLocation GetDropLocation(Point pt, bool isFolder, bool isDropValid) {
        const double NormalBorder = 8.0;
        // Inside drop is only allowed when a folder and the drop is valid
        if (isFolder && isDropValid) {
            if (DoubleUtils.LessThan(pt.Y, NormalBorder)) {
                return DropLocation.Above;
            }
            else if (DoubleUtils.GreaterThanOrClose(pt.Y, this.Bounds.Height - NormalBorder)) {
                return DropLocation.Below;
            }
            else {
                return DropLocation.Inside;
            }
        }
        else {
            double middle = this.Bounds.Height / 2.0;
            return DoubleUtils.LessThan(pt.Y, middle) ? DropLocation.Above : DropLocation.Below;
        }
    }

    /*
     WTF does DragEffects do?

     During DragEnter() and DragOver(), DragEventArgs.DragEffects will be the allowed drag effects
     for the specific drag-drop operation, which are the effects passed to DoDragDrop.
     You have to process DragEffects (and ideally set Handled to true) and set it to a single
     value that represents what you want the drop to actually perform. This is purely a visual
     feedback thing. Changing it won't affect what's passed to Drop().

     In Drop(), you're still provided the allowed drop types, and you have to do the same
     processing for what drop to actually do based on which modifier keys are pressed.

     */

    private void OnDragEnter(DragEventArgs e) {
        if (this.IsFolderItem) {
            this.expandForDragOverTimer?.Stop();
            this.expandForDragOverTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(0.5), DispatcherPriority.Normal, (sender, args) => {
                this.IsExpanded = true;
                this.expandForDragOverTimer?.Stop();
            });

            this.expandForDragOverTimer.Start();
        }

        this.OnDragOver(e);
    }

    private void OnDragOver(DragEventArgs e) {
        e.Handled = true;

        Point point = e.GetPosition(this);
        DropLocation? location;
        if (!GetResourceListFromDragEvent(e, out List<AddressTableTreeViewItem>? items)) {
            e.DragEffects = DragDropEffects.None;
            location = null;
        }
        else {
            EnumDropType dropType = DropUtils.GetDropFromPressedModifiers(e.KeyModifiers, (EnumDropType) e.DragEffects);
            (List<AddressTableTreeViewItem>?, DropListResult) result = AddressTableTreeView.GetEffectiveDropList(this, items);
            if (result.Item2 != DropListResult.Valid) {
                if (result.Item2 == DropListResult.DropListIntoSelf) {
                    location = null;
                    e.DragEffects = DragDropEffects.None;
                }
                else {
                    location = this.GetDropLocation(point, this.IsFolderItem, false);
                    Debug.Assert(location != DropLocation.Inside);

                    e.DragEffects = (DragDropEffects) dropType;
                }
            }
            else {
                e.DragEffects = (DragDropEffects) dropType;
                location = this.GetDropLocation(point, this.IsFolderItem, true);
            }
        }

        if (location == DropLocation.Inside || e.DragEffects == DragDropEffects.None) {
            this.IsDroppableTargetOver = e.DragEffects != DragDropEffects.None;
            this.PART_DragDropMoveBorder!.BorderThickness = default;
        }
        else if (location.HasValue) {
            this.IsDroppableTargetOver = false;
            this.PART_DragDropMoveBorder!.BorderThickness = location.Value == DropLocation.Above ? new Thickness(0, 1, 0, 0) : new Thickness(0, 0, 0, 1);
        }
        else {
            this.IsDroppableTargetOver = false;
            this.PART_DragDropMoveBorder!.BorderThickness = default;
        }
    }

    private void OnDragLeave(DragEventArgs e) {
        if (!this.IsPointerOver) {
            this.IsDroppableTargetOver = false;
            this.PART_DragDropMoveBorder!.BorderThickness = default;
        }

        this.expandForDragOverTimer?.Stop();
    }

    private async void OnDrop(DragEventArgs e) {
        e.Handled = true;
        if (this.isProcessingAsyncDrop || this.EntryObject == null) {
            return;
        }

        try {
            Point point = e.GetPosition(this);
            EnumDropType dropType = DropUtils.GetDropFromPressedModifiers(e.KeyModifiers, (EnumDropType) e.DragEffects);
            e.DragEffects = (DragDropEffects) dropType;

            this.isProcessingAsyncDrop = true;
            // Dropped non-resources into this node
            if (!GetResourceListFromDragEvent(e, out List<AddressTableTreeViewItem>? theDroppedItemList)) {
                await IMessageDialogService.Instance.ShowMessage("Unknown Data", "Unknown dropped item(s)");
                return;
            }

            (List<AddressTableTreeViewItem>?, DropListResult) result = AddressTableTreeView.GetEffectiveDropList(this, theDroppedItemList);
            DropLocation dropLocation;
            if (result.Item2 == DropListResult.DropListIntoSelf) {
                return;
            }

            if (result.Item2 == DropListResult.ValidButDropListAlreadyInTarget) {
                // Technically the drop is valid, but we specify false so that inside is not
                // allowed, to use the full height of this tree node for above/below
                dropLocation = this.GetDropLocation(point, this.IsFolderItem, false);
                Debug.Assert(dropLocation != DropLocation.Inside, "Impossible condition");
            }
            else {
                dropLocation = this.GetDropLocation(point, this.IsFolderItem, result.Item2 == DropListResult.Valid);
            }

            List<BaseAddressTableEntry> droppedModels = result.Item1!.Select(x => x.EntryObject!).ToList();
            List<BaseAddressTableEntry> selection;

            if (dropType == EnumDropType.Move) {
                if (dropLocation == DropLocation.Inside) {
                    AddressTableGroupEntry thisModel = (AddressTableGroupEntry) this.EntryObject!;
                    Debug.Assert(thisModel.Parent != null, "Why is there a ATTreeViewItem for the root entry object? Or why is it removed???");

                    foreach (IGrouping<AddressTableGroupEntry, BaseAddressTableEntry> entry in droppedModels.GroupBy(x => x.Parent!)) {
                        entry.Key.Items.RemoveRange(entry);
                        thisModel.Items.AddRange(entry);
                    }
                }
                else {
                    BaseAddressTableEntry thisModel = this.EntryObject!;
                    AddressTableGroupEntry dstModel = this.EntryObject!.Parent!;
                    Debug.Assert(dstModel != null, "This ATTreeViewItem is not in a parent...?");

                    // User wants to move items above or below current instance. First, remove all
                    // dropped entries to make calculating indices easier. TODO improve this though 
                    foreach (IGrouping<AddressTableGroupEntry, BaseAddressTableEntry> entry in droppedModels.GroupBy(x => x.Parent!)) {
                        entry.Key.Items.RemoveRange(entry);
                    }

                    int index = dstModel.IndexOf(thisModel);
                    if (dropLocation == DropLocation.Below) {
                        index++;
                    }

                    dstModel.Items.InsertRange(index, droppedModels);
                }

                selection = droppedModels;
            }
            else {
                List<BaseAddressTableEntry> cloneList = droppedModels.Select(x => x.CreateClone()).ToList();
                if (dropLocation == DropLocation.Inside) {
                    AddressTableGroupEntry thisModel = (AddressTableGroupEntry) this.EntryObject!;
                    Debug.Assert(thisModel.Parent != null, "Why is there a ATTreeViewItem for the root entry object? Or why is it removed???");

                    thisModel.Items.AddRange(cloneList);
                }
                else {
                    BaseAddressTableEntry thisModel = this.EntryObject!;
                    AddressTableGroupEntry dstModel = thisModel.Parent!;
                    Debug.Assert(dstModel != null, "This ATTreeViewItem is not in a parent...?");

                    int index = dstModel.IndexOf(thisModel);
                    if (dropLocation == DropLocation.Below) {
                        index++;
                    }

                    dstModel.Items.InsertRange(index, cloneList);
                }

                selection = cloneList;
            }

            this.MyTree!.SetSelection(selection);
        }
#if !DEBUG
            catch (Exception exception) {
                await PFXToolKitUI.Services.Messaging.IMessageDialogService.Instance.ShowMessage("Error", "An error occurred while processing list item drop", exception.ToString());
            }
#endif
        finally {
            this.isProcessingAsyncDrop = false;
            this.IsDroppableTargetOver = false;
            this.PART_DragDropMoveBorder!.BorderThickness = default;
        }
    }

    /// <summary>
    /// Tries to get the list of resources being drag-dropped from the given drag event
    /// </summary>
    /// <param name="e">Drag event (enter, over, drop, etc.)</param>
    /// <param name="items">The resources in the drag event</param>
    /// <returns>True if there were resources available, otherwise false, meaning no resources are being dragged</returns>
    public static bool GetResourceListFromDragEvent(DragEventArgs e, [NotNullWhen(true)] out List<AddressTableTreeViewItem>? items) {
        if (e.Data.Contains(DropKey)) {
            object? obj = e.Data.Get(DropKey);
            if ((items = obj as List<AddressTableTreeViewItem>) != null) {
                return true;
            }
        }

        items = null;
        return false;
    }

    #endregion
}

public enum NodeDragState {
    // No drag drop has been started yet
    None = 0,

    // User left-clicked, so wait for enough move movement
    Initiated = 1,

    // User moved their mouse enough. DragDrop is running
    Active = 2,

    // Node dropped, this is used to ensure we don't restart when the mouse moves
    // again e.g. if they right-click (which win32 takes as cancelling the drop) but
    // the user keeps left mouse pressed
    Completed = 3
}

internal enum DropLocation {
    Inside,
    Below,
    Above
}

public static class AddressTableContextRegistry {
    public static readonly ContextRegistry Registry = new ContextRegistry("Saved Addresses");

    static AddressTableContextRegistry() {
        Registry.Opened += static (_, context) => {
            if (MemoryEngineViewState.DataKey.TryGetContext(context, out MemoryEngineViewState? engineVs)) {
                TreeSelectionModel<BaseAddressTableEntry> atsm = engineVs.AddressTableSelectionManager;
                
                if (atsm.HasOneSelectedItem) {
                    string? first = atsm.SelectedItems.First().Description;
                    Registry.ObjectName = string.IsNullOrWhiteSpace(first) ? "(unnamed)" : first;
                    return;
                }
                else if (atsm.Count > 1) {
                    Registry.ObjectName = $"{atsm.Count} {(atsm.Count == 1 ? "entry" : "entries")}";
                    return;
                }
            }

            Registry.ObjectName = null;
        };

        FixedWeightedMenuEntryGroup modEdit = Registry.GetFixedGroup("modify.edit");
        FixedWeightedMenuEntryGroup modGeneric = Registry.GetFixedGroup("modify.general");
        modEdit.AddHeader("Modify");
        modEdit.AddCommand("commands.memengine.EditSavedAddressAddressCommand", "Edit Address");
        modEdit.AddCommand("commands.memengine.EditSavedAddressValueCommand", "Edit Value");
        modEdit.AddCommand("commands.memengine.EditSavedAddressDataTypeCommand", "Edit Data Type");
        modEdit.AddCommand("commands.memengine.EditSavedAddressDescriptionCommand", "Edit Description");
        modGeneric.AddHeader("General");
        modGeneric.AddCommand("commands.memengine.CopyAddressToClipboardCommand", "Copy Address", "Copy the address string in the Address column");
        modGeneric.AddCommand("commands.memengine.CopyAbsoluteAddressToClipboardCommand", "Copy Absolute Address", "Copy the fully resolved address if using a dynamic (aka pointer) address. Same as Copy Address otherwise.");
        modGeneric.AddCommand("commands.memengine.CopySavedAddressValuesToClipboardCommand", "Copy Value");
        modGeneric.AddCommand("commands.memengine.RefreshSavedAddressesCommand", "Refresh");
        modGeneric.AddCommand("commands.meengine.ToggleSavedAddressAutoRefreshCommand", "Toggle Enabled");
        modGeneric.AddSeparator();
        modGeneric.AddCommand("commands.memengine.GroupEntriesCommand", "Group");
        modGeneric.AddCommand("commands.memengine.DuplicateSelectedSavedAddressesCommand", "Duplicate");
        modGeneric.AddSeparator();
        modGeneric.AddCommand("commands.memengine.DeleteSelectedSavedAddressesCommand", "Delete");
    }
}