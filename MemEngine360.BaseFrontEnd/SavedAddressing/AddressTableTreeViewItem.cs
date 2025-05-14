// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.SavedAddressing;

public sealed class AddressTableTreeViewItem : TreeViewItem, IAddressTableEntryUI {
    public static readonly DirectProperty<AddressTableTreeViewItem, bool> IsFolderItemProperty = AvaloniaProperty.RegisterDirect<AddressTableTreeViewItem, bool>("IsFolderItem", o => o.IsFolderItem, null);

    public AddressTableTreeView? MyTree { get; private set; }
    public AddressTableTreeViewItem? ParentNode { get; private set; }
    public BaseAddressTableEntry? EntryObject { get; private set; }

    public bool IsFolderItem {
        get => this.isFolderItem;
        private set => this.SetAndRaise(IsFolderItemProperty, ref this.isFolderItem, value);
    }

    private ObservableItemProcessorIndexing<BaseAddressTableEntry>? compositeListener;

    private readonly IBinder<BaseAddressTableEntry> descriptionBinder = new AvaloniaPropertyToEventPropertyGetSetBinder<BaseAddressTableEntry>(TextBlock.TextProperty, nameof(BaseAddressTableEntry.DescriptionChanged), b => ((TextBlock) b.Control).Text = b.Model.Description, null);
    private readonly IBinder<AddressTableGroupEntry> groupAddressBinder = new AvaloniaPropertyToEventPropertyGetSetBinder<AddressTableGroupEntry>(HeaderProperty, nameof(AddressTableGroupEntry.GroupAddressChanged), b => b.Model.GroupAddress?.ToString("X8") ?? "Group", null);
    private readonly IBinder<AddressTableEntry> entryAddressBinder = new AvaloniaPropertyToEventPropertyGetSetBinder<AddressTableEntry>(HeaderProperty, nameof(AddressTableEntry.AddressChanged), b => b.Model.Address.ToString("X8"), null);
    private readonly IBinder<AddressTableEntry> dataTypeTextBinder = new AvaloniaPropertyToEventPropertyGetSetBinder<AddressTableEntry>(TextBlock.TextProperty, nameof(AddressTableEntry.DataTypeChanged), b => ((TextBlock) b.Control).Text = b.Model.DataType.ToString(), null);
    private readonly IBinder<AddressTableEntry> valueTextBinder = new AvaloniaPropertyToEventPropertyGetSetBinder<AddressTableEntry>(TextBlock.TextProperty, nameof(AddressTableEntry.ValueChanged), b => ((TextBlock) b.Control).Text = b.Model.Value, null);
    private Border? PART_DragDropMoveBorder;
    private bool isFolderItem;

    private TextBlock? PART_AddressTextBlock;
    private TextBlock? PART_Description;
    private TextBlock? PART_DataTypeText;
    private TextBlock? PART_ValueText;

    private readonly AsyncRelayCommand EditAddressCommand, EditDescriptionCommand, EditDataTypeCommand;

    BaseAddressTableEntry IAddressTableEntryUI.Entry => this.EntryObject ?? throw new Exception("Not connected to an entry");

    public AddressTableTreeViewItem() {
        DataManager.GetContextData(this).Set(IAddressTableEntryUI.DataKey, this);

        this.EditAddressCommand = new AsyncRelayCommand(async () => {
            if (!(this.EntryObject is AddressTableEntry entry)) {
                return;
            }

            SingleUserInputInfo info = new SingleUserInputInfo(entry.Address.ToString("X8")) {
                Caption = "Dump memory region",
                Message = "Change the address of this saved address table entry",
                DefaultButton = true,
                Label = "Address (hex)",
                Validate = (a) => {
                    if (!uint.TryParse(a.Input, NumberStyles.HexNumber, null, out _)) {
                        if (ulong.TryParse(a.Input, NumberStyles.HexNumber, null, out _)) {
                            a.Errors.Add("Value is too big. Maximum is 0xFFFFFFFF");
                        }
                        else {
                            a.Errors.Add("Invalid UInt32.");
                        }
                    }
                }
            };

            if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
                entry.Address = uint.Parse(info.Text, NumberStyles.HexNumber);
                entry.ScanningProcessor.RefreshSavedAddressesLater();
            }
        });
        
        this.EditDescriptionCommand = new AsyncRelayCommand(async () => {
            await CommandManager.Instance.Execute("commands.memengine.EditSavedAddressDescriptionCommand", DataManager.GetFullContextData(this));
        });

        this.EditDataTypeCommand = new AsyncRelayCommand(async () => {
            await CommandManager.Instance.Execute("commands.memengine.EditSavedAddressDataTypeCommand", DataManager.GetFullContextData(this));
        });
    }

    static AddressTableTreeViewItem() {
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_DragDropMoveBorder = e.NameScope.GetTemplateChild<Border>(nameof(this.PART_DragDropMoveBorder));
        this.PART_AddressTextBlock = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_AddressTextBlock));
        this.PART_AddressTextBlock.DoubleTapped += (s, args) => this.EditAddressCommand.Execute(null);
        this.PART_Description = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_Description));
        this.PART_Description.DoubleTapped += (s, args) => this.EditDescriptionCommand.Execute(null);
        this.PART_DataTypeText = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_DataTypeText));
        this.PART_DataTypeText.DoubleTapped += (s, args) => this.EditDataTypeCommand.Execute(null);
        this.PART_ValueText = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_ValueText));

        this.descriptionBinder.AttachControl(this.PART_Description);
        this.groupAddressBinder.AttachControl(this);
        this.entryAddressBinder.AttachControl(this);
        this.dataTypeTextBinder.AttachControl(this.PART_DataTypeText);
        this.valueTextBinder.AttachControl(this.PART_ValueText);
    }

    public void OnAdding(AddressTableTreeView tree, AddressTableTreeViewItem? parentNode, BaseAddressTableEntry layer) {
        this.MyTree = tree;
        this.ParentNode = parentNode;
        this.EntryObject = layer;
        this.IsFolderItem = layer is AddressTableGroupEntry;
    }

    public void OnAdded() {
        TemplateUtils.ApplyRecursive(this);
        if (this.EntryObject is AddressTableGroupEntry folder) {
            this.compositeListener = ObservableItemProcessor.MakeIndexable(folder.Items, this.OnLayerAdded, this.OnLayerRemoved, this.OnLayerMoved);
            int i = 0;
            foreach (BaseAddressTableEntry item in folder.Items) {
                this.InsertNode(item, i++);
            }
        }

        this.descriptionBinder.AttachModel(this.EntryObject!);
        if (this.EntryObject is AddressTableGroupEntry group) {
            this.groupAddressBinder.AttachModel(group);
            if (this.PART_DataTypeText != null)
                this.PART_DataTypeText!.Text = "";
            if (this.PART_ValueText != null)
                this.PART_ValueText!.Text = "";
            this.OnIsAutoRefreshEnabledChanged(null);
        }

        if (this.EntryObject is AddressTableEntry entry) {
            this.entryAddressBinder.AttachModel(entry);
            this.dataTypeTextBinder.AttachModel(entry);
            this.valueTextBinder.AttachModel(entry);
            entry.IsAutoRefreshEnabledChanged += this.OnIsAutoRefreshEnabledChanged;
            this.OnIsAutoRefreshEnabledChanged(entry);
        }

        DataManager.GetContextData(this).Set(BaseAddressTableEntry.DataKey, this.EntryObject);
    }

    private void OnIsAutoRefreshEnabledChanged(AddressTableEntry? sender) {
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
        if (this.groupAddressBinder.IsFullyAttached)
            this.groupAddressBinder.DetachModel();
        
        if (this.entryAddressBinder.IsFullyAttached) {
            this.entryAddressBinder.DetachModel();
            this.dataTypeTextBinder.DetachModel();
            this.valueTextBinder.DetachModel();
        }

        if (this.EntryObject is AddressTableEntry entry) {
            entry.IsAutoRefreshEnabledChanged -= this.OnIsAutoRefreshEnabledChanged;
        }

        DataManager.GetContextData(this).Set(BaseAddressTableEntry.DataKey, null);
    }

    public void OnRemoved() {
        this.MyTree = null;
        this.ParentNode = null;
        this.EntryObject = null;
        this.IsFolderItem = false;
        DataManager.ClearContextData(this);
    }

    private void OnLayerAdded(object sender, int index, BaseAddressTableEntry item) {
        this.InsertNode(item, index);
    }

    private void OnLayerRemoved(object sender, int index, BaseAddressTableEntry item) {
        this.RemoveNode(index);
    }

    private void OnLayerMoved(object sender, int oldindex, int newindex, BaseAddressTableEntry item) {
        this.MoveNode(oldindex, newindex);
    }

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
        tree.AddResourceMapping(control, layer);
        control.ApplyStyling();
        control.ApplyTemplate();
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
        tree.RemoveResourceMapping(control, resource);
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

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);
        if (e.Handled || this.EntryObject == null) {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(this);
        if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed) {
            bool isToggle = (e.KeyModifiers & KeyModifiers.Control) != 0;
            if ((e.ClickCount % 2) == 0) {
                if (!isToggle) {
                    this.SetCurrentValue(IsExpandedProperty, !this.IsExpanded);
                    e.Handled = true;
                }
            }
        }
    }
}