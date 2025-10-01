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
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using MemEngine360.Engine;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Contexts;
using PFXToolKitUI.Avalonia.AdvancedMenuService;
using PFXToolKitUI.Avalonia.AvControls;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class SequenceListBoxItem : ModelBasedListBoxItem<TaskSequence> {
    private readonly IBinder<TaskSequence> nameBinder = new EventUpdateBinder<TaskSequence>(nameof(TaskSequence.DisplayNameChanged), (b) => ((SequenceListBoxItem) b.Control).Content = b.Model.DisplayName);

    private readonly IBinder<TaskSequence> runCountBinder = new TextBoxToEventPropertyBinder<TaskSequence>(nameof(TaskSequence.RunCountChanged), (b) => {
        int count = b.Model.RunCount;
        return count < 0 ? "Infinity" : count.ToString();
    }, async (b, text) => {
        string txt = text.Trim().ToLowerInvariant();
        if (int.TryParse(txt, out int value)) {
            b.Model.RunCount = value;
            return true;
        }
        else {
            if (txt.Equals("forever") || txt.StartsWith("inf") || txt.StartsWith("endless")) {
                b.Model.RunCount = -1;
                return true;
            }
            else {
                await IMessageDialogService.Instance.ShowMessage("Invalid value", $"Run count is invalid. Is must be between 0 and {uint.MaxValue}, or \"infinity\" or just \"inf\"", defaultButton: MessageBoxResult.OK);
                return false;
            }
        }
    });

    private IconButton? PART_CancelActivityButton;
    private IconButton? PART_RunButton;
    private TextBox? PART_RunCountTextBox;
    private MemoryEngine? myEngine;

    public TaskSequence TaskSequence => this.Model ?? throw new Exception("Not connected to a model");

    public SequenceListBoxItem() {
        this.nameBinder.AttachControl(this);
        this.AddBinderForModel(this.nameBinder, this.runCountBinder);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        TaskSequencerView? view = VisualTreeUtils.FindLogicalParent<TaskSequencerView>(this.ListBox);
        if (view != null) {
            PointerPointProperties pointer = e.GetCurrentPoint(this).Properties;
            if (pointer.PointerUpdateKind == PointerUpdateKind.RightButtonPressed) {
                if (!this.IsSelected) {
                    this.ListBox!.UnselectAll();
                    this.IsSelected = true;
                }

                e.Handled = true;
            }

            base.OnPointerPressed(e);
            if (pointer.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed) {
                ListSelectionModel<TaskSequence> selection = view.State.SelectedSequences;
                if (selection.Count == 1 && selection.IsItemSelected(this.Model!) == true) {
                    Debug.Assert(this.IsSelected);
                    view.State.ConditionHost = this.Model!;
                }
            }
        }
        else {
            base.OnPointerPressed(e);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.SetDragSourceControl(e.NameScope.GetTemplateChild<Border>("PART_DragGrip"));

        this.PART_CancelActivityButton = e.NameScope.GetTemplateChild<IconButton>(nameof(this.PART_CancelActivityButton));
        this.PART_RunButton = e.NameScope.GetTemplateChild<IconButton>(nameof(this.PART_RunButton));

        this.PART_RunCountTextBox = e.NameScope.GetTemplateChild<TextBox>(nameof(this.PART_RunCountTextBox));
        this.runCountBinder.AttachControl(this.PART_RunCountTextBox);

        this.UpdateControlsForIsRunning();
    }

    protected override void OnAddingToList() {
    }

    protected override void OnAddedToList() {
        this.myEngine = this.Model!.Manager!.MemoryEngine;
        this.Model.IsRunningChanged += this.OnIsRunningChanged;

        DataManager.GetContextData(this).Set(TaskSequence.DataKey, this.Model);
        AdvancedContextMenu.SetContextRegistry(this, TaskSequenceContextRegistry.Registry);
    }

    protected override void OnRemovingFromList() {
        this.myEngine = null;
        this.Model!.IsRunningChanged -= this.OnIsRunningChanged;
        AdvancedContextMenu.SetContextRegistry(this, null);

        DataManager.GetContextData(this).Remove(TaskSequence.DataKey);
    }

    protected override void OnRemovedFromList() {
    }

    private void OnIsRunningChanged(TaskSequence sender) => this.UpdateControlsForIsRunning();

    private void UpdateControlsForIsRunning() {
        if (this.PART_RunCountTextBox != null && this.Model != null) {
            this.PART_RunCountTextBox.IsEnabled = !this.Model.IsRunning;
        }
    }
}