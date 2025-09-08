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

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using MemEngine360.Engine;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.Avalonia;

public class ScanningOrderListBox : ModelBasedListBox<ScanningOrderModel> {
    private UnknownDataTypeOptions? options;

    protected override bool CanDragItemPositionCore => true;

    public ScanningOrderListBox() : base(1) {
        this.CanDragItemPosition = true;
    }

    public void SetScanningProcessor(ScanningProcessor? processor) {
        this.options = processor?.UnknownDataTypeOptions;
        this.SetItemsSource(this.options?.Orders);
    }

    protected override ModelBasedListBoxItem<ScanningOrderModel> CreateItem() => new ScanningOrderListBoxItem();

    protected override void MoveItemIndexOverride(int oldIndex, int newIndex) {
        this.options!.Orders.Move(oldIndex, newIndex);
    }
}

public class ScanningOrderListBoxItem : ModelBasedListBoxItem<ScanningOrderModel> {
    private readonly IBinder<ScanningOrderModel> toggleBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningOrderModel>(CheckBox.IsCheckedProperty, nameof(ScanningOrderModel.IsEnabledChanged), (b) => ((CheckBox) b.Control).IsChecked = b.Model.IsEnabled, (b) => b.Model.IsEnabled = ((CheckBox) b.Control).IsChecked == true);

    private CheckBox? PART_Toggle;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_Toggle = e.NameScope.GetTemplateChild<CheckBox>(nameof(this.PART_Toggle));
        this.toggleBinder.AttachControl(this.PART_Toggle);
        if (this.Model != null)
            this.PART_Toggle.Content = this.Model!.DataType.ToString();

        this.SetDragSourceControl(e.NameScope.GetTemplateChild<Border>("PART_DragGrip"));
    }

    private void OnDataTypeChanged(ScanningOrderModel sender) {
        this.PART_Toggle!.Content = sender.DataType.ToString();
    }

    protected override void OnAddingToList() {
    }

    protected override void OnAddedToList() {
        this.toggleBinder.AttachModel(this.Model!);
        this.Model!.DataTypeChanged += this.OnDataTypeChanged;
        if (this.PART_Toggle != null)
            this.PART_Toggle.Content = this.Model!.DataType.ToString();
    }

    protected override void OnRemovingFromList() {
        this.toggleBinder.DetachModel();
        this.Model!.DataTypeChanged -= this.OnDataTypeChanged;
    }

    protected override void OnRemovedFromList() {
    }
}