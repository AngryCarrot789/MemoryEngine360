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

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using MemEngine360.Engine;
using MemEngine360.Sequencing;
using PFXToolKitUI.Avalonia.AvControls;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Contexts;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class SequenceListBoxItem : ModelBasedListBoxItem<TaskSequence>, ITaskSequenceEntryUI {
    private readonly IBinder<TaskSequence> nameBinder = new AvaloniaPropertyToEventPropertyBinder<TaskSequence>(ContentProperty, nameof(TaskSequence.DisplayNameChanged), (b) => ((SequenceListBoxItem) b.Control).Content = b.Model.DisplayName, null);
    private readonly IBinder<TaskSequence> busyLockPriorityBinder = new AvaloniaPropertyToEventPropertyBinder<TaskSequence>(CheckBox.IsCheckedProperty, nameof(TaskSequence.HasBusyLockPriorityChanged), (b) => ((CheckBox) b.Control).IsChecked = b.Model.HasBusyLockPriority, (b) => b.Model.HasBusyLockPriority = ((CheckBox) b.Control).IsChecked == true);

    private readonly IBinder<TaskSequence> runCountBinder = new TextBoxToEventPropertyBinder<TaskSequence>(nameof(TaskSequence.RunCountChanged), (b) => b.Model.RunCount.ToString(), async (b, text) => {
        if (uint.TryParse(text, out uint value)) {
            b.Model.RunCount = value;
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", $"Run count is invalid. Is must be between 0 and {uint.MaxValue}", defaultButton: MessageBoxResult.OK);
        }
    });

    private IconButton? PART_CancelActivityButton;
    private IconButton? PART_RunButton;
    private TextBox? PART_RunCountTextBox;
    private CheckBox? PART_ToggleBusyExclusive;
    private MemoryEngine360? myEngine;

    public TaskSequence TaskSequence => this.Model ?? throw new Exception("Not connected to a model");
    
    public SequenceListBoxItem() {
    }
    
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.SetDragSourceControl(e.NameScope.GetTemplateChild<Border>("PART_DragGrip"));
        
        this.PART_CancelActivityButton = e.NameScope.GetTemplateChild<IconButton>(nameof(this.PART_CancelActivityButton));
        this.PART_RunButton = e.NameScope.GetTemplateChild<IconButton>(nameof(this.PART_RunButton));

        this.PART_RunCountTextBox = e.NameScope.GetTemplateChild<TextBox>(nameof(this.PART_RunCountTextBox));
        this.runCountBinder.AttachControl(this.PART_RunCountTextBox);

        this.PART_ToggleBusyExclusive = e.NameScope.GetTemplateChild<CheckBox>(nameof(this.PART_ToggleBusyExclusive));
        this.busyLockPriorityBinder.AttachControl(this.PART_ToggleBusyExclusive);
    }

    protected override void OnAddingToList() {
    }

    protected override void OnAddedToList() {
        this.nameBinder.Attach(this, this.Model!);
        this.busyLockPriorityBinder.AttachModel(this.Model!);
        this.runCountBinder.AttachModel(this.Model!);
        this.myEngine = this.Model!.Manager!.Engine;

        using MultiChangeToken batch = DataManager.GetContextData(this).BeginChange();
        batch.Context.Set(ITaskSequencerUI.TaskSequenceDataKey, this.Model!).Set(ITaskSequenceEntryUI.DataKey, this);
    }

    protected override void OnRemovingFromList() {
        this.nameBinder.DetachModel();
        this.busyLockPriorityBinder.DetachModel();
        this.runCountBinder.DetachModel();
        this.myEngine = null;

        using MultiChangeToken batch = DataManager.GetContextData(this).BeginChange();
        batch.Context.Set(ITaskSequencerUI.TaskSequenceDataKey, null).Set(ITaskSequenceEntryUI.DataKey, null);
    }

    protected override void OnRemovedFromList() {
    }
}