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
using Avalonia.VisualTree;
using MemEngine360.BaseFrontEnd.TaskSequencing.Operations.ListContent;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Contexts;
using PFXToolKitUI.Avalonia.AdvancedMenuService;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Operations;

public class OperationListBoxItem : ModelBasedListBoxItem<BaseSequenceOperation>, IOperationItemUI {
    public static readonly StyledProperty<bool> IsRunningProperty = AvaloniaProperty.Register<OperationListBoxItem, bool>(nameof(IsRunning));

    public bool IsRunning {
        get => this.GetValue(IsRunningProperty);
        set => this.SetValue(IsRunningProperty, value);
    }

    public BaseSequenceOperation Operation => this.Model ?? throw new Exception("Not connected to a model");

    private readonly IBinder<BaseSequenceOperation> isRunningBinder = new EventUpdateBinder<BaseSequenceOperation>(nameof(BaseSequenceOperation.IsRunningChanged), (b) => ((OperationListBoxItem) b.Control).IsRunning = b.Model.IsRunning);

    public OperationListBoxItem() {
        DataManager.GetContextData(this).Set(IOperationItemUI.DataKey, this);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        if (this.ListBox != null) {
            PointerPointProperties pointer = e.GetCurrentPoint(this).Properties;
            if (pointer.PointerUpdateKind == PointerUpdateKind.RightButtonPressed) {
                if (!this.IsSelected) {
                    this.ListBox.UnselectAll();
                    this.IsSelected = true;
                }

                e.Handled = true;
            }

            base.OnPointerPressed(e);

            if (pointer.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed && this.ListBox.GetVisualRoot() is TaskSequencerWindow window) {
                // The condition source may currently be a task sequence. But since this operation was clicked
                // and is already selected, a selection change won't be processed and the source won't get updated.
                // So, we do it manually here
                ObservableList<BaseSequenceOperation>? state = window.State!.SelectedOperations;
                if (state != null && state.Count == 1 && this.IsSelected) {
                    window.SetConditionSourceAsOperation(this.Model!);
                }
            }
        }
        else {
            base.OnPointerPressed(e);
        }
    }

    // replaced by ToggleOperationEnabledCommand
    // protected override void OnKeyDown(KeyEventArgs e) {
    //     base.OnKeyDown(e);
    //     if (!e.Handled && e.Key == Key.Space && this.IsFocused && this.Model != null) {
    //         this.Model.IsEnabled = !this.Model.IsEnabled;
    //     }
    // }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.SetDragSourceControl(e.NameScope.GetTemplateChild<Border>("PART_DragGrip"));
    }

    protected override void OnAddingToList() {
        this.Content = ((OperationListBox) this.ListBox!).GetContentObject(this.Model!);
    }

    protected override void OnAddedToList() {
        BaseOperationListContent content = (BaseOperationListContent) this.Content!;
        TemplateUtils.Apply(content);
        content.Operation = this.Model;

        this.isRunningBinder.Attach(this, this.Model!);
        AdvancedContextMenu.SetContextRegistry(this, OperationsContextRegistry.Registry);
    }

    protected override void OnRemovingFromList() {
        this.isRunningBinder.Detach();

        AdvancedContextMenu.SetContextRegistry(this, null);
        BaseOperationListContent content = (BaseOperationListContent) this.Content!;
        BaseSequenceOperation operation = content.Operation!;
        content.Operation = null;
        this.Content = null;
        ((OperationListBox) this.ListBox!).ReleaseContentObject(operation, content);
    }

    protected override void OnRemovedFromList() {
    }
}