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
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Sequencing;
using PFXToolKitUI.Avalonia.AvControls;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class SequenceListBoxItem : ModelBasedListBoxItem<TaskSequence> {
    private readonly IBinder<TaskSequence> nameBinder = new AvaloniaPropertyToEventPropertyBinder<TaskSequence>(ContentProperty, nameof(TaskSequence.DisplayNameChanged), (b) => ((SequenceListBoxItem) b.Control).Content = b.Model.DisplayName, null);
    private readonly IBinder<TaskSequence> busyLockPriorityBinder = new AvaloniaPropertyToEventPropertyBinder<TaskSequence>(CheckBox.IsCheckedProperty, nameof(TaskSequence.HasBusyLockPriorityChanged), (b) => ((CheckBox) b.Control).IsChecked = b.Model.HasBusyLockPriority, (b) => b.Model.HasBusyLockPriority = ((CheckBox) b.Control).IsChecked == true);

    private readonly IBinder<TaskSequence> repeatBinder = new TextBoxToEventPropertyBinder<TaskSequence>(nameof(TaskSequence.RepeatCountChanged), (b) => b.Model.RepeatCount.ToString(), async (b, text) => {
        if (uint.TryParse(text, out uint value)) {
            b.Model.RepeatCount = value;
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Repeat count is invalid", defaultButton: MessageBoxResult.OK);
        }
    });

    private IconButton? PART_CancelActivityButton;
    private IconButton? PART_RunButton;
    private TextBox? PART_RepeatCounter;
    private CheckBox? PART_ToggleBusyExclusive;

    private readonly AsyncRelayCommand runCommand, cancelCommand;

    private CancellationTokenSource? myCts;
    private MemoryEngine360? myEngine;

    public SequenceListBoxItem() {
        this.runCommand = new AsyncRelayCommand(async () => {
            TaskSequence? seq = this.Model;
            if (seq == null || seq.IsRunning) {
                return;
            }

            IDisposable? token = null;
            if (seq.HasBusyLockPriority && (token = seq.Manager!.Engine.BeginBusyOperation()) == null) {
                using CancellationTokenSource cts = new CancellationTokenSource();
                token = await ActivityManager.Instance.RunTask(() => {
                    ActivityTask task = ActivityManager.Instance.CurrentTask;
                    task.Progress.Caption = $"Start '{seq.DisplayName}'";
                    task.Progress.Text = "Waiting for busy operations...";
                    return seq.Manager!.Engine.BeginBusyOperationAsync(task.CancellationToken);
                }, seq.Progress, cts);

                // User cancelled operation so don't run the sequence, since it wants busy lock priority
                if (token == null) {
                    return;
                }
            }

            try {
                IConsoleConnection? connection = seq.Manager!.Engine.Connection;
                if (connection != null && connection.IsConnected) {
                    this.myCts = new CancellationTokenSource();
                    await seq.Run(this.myCts, connection, token);
                    this.myCts.Dispose();
                    this.myCts = null;
                    this.cancelCommand!.RaiseCanExecuteChanged();

                    if (seq.LastException != null) {
                        await IMessageDialogService.Instance.ShowMessage("Error encountered", "An exception occured while running sequence", seq.LastException.GetToString());
                    }
                }
            }
            finally {
                token?.Dispose();
            }
        }, () => {
            TaskSequence? seq = this.Model;
            return seq != null && !seq.IsRunning && seq.Manager!.Engine.Connection != null;
        });

        this.cancelCommand = new AsyncRelayCommand(async () => {
            try {
                this.myCts?.Cancel();
            }
            catch (ObjectDisposedException) {
                // ignored
            }

            if (this.Model != null)
                await this.Model!.WaitForCompletion();
        }, () => {
            TaskSequence? seq = this.Model;
            return seq != null && seq.IsRunning && this.myCts != null;
        });
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_CancelActivityButton = e.NameScope.GetTemplateChild<IconButton>(nameof(this.PART_CancelActivityButton));
        this.PART_CancelActivityButton.Command = this.cancelCommand;

        this.PART_RunButton = e.NameScope.GetTemplateChild<IconButton>(nameof(this.PART_RunButton));
        this.PART_RunButton.Command = this.runCommand;

        this.PART_RepeatCounter = e.NameScope.GetTemplateChild<TextBox>(nameof(this.PART_RepeatCounter));
        this.repeatBinder.AttachControl(this.PART_RepeatCounter);

        this.PART_ToggleBusyExclusive = e.NameScope.GetTemplateChild<CheckBox>(nameof(this.PART_ToggleBusyExclusive));
        this.busyLockPriorityBinder.AttachControl(this.PART_ToggleBusyExclusive);
    }

    protected override void OnAddingToList() {
    }

    protected override void OnAddedToList() {
        this.nameBinder.Attach(this, this.Model!);
        this.busyLockPriorityBinder.AttachModel(this.Model!);
        this.repeatBinder.AttachModel(this.Model!);
        this.Model!.IsRunningChanged += this.OnIsRunningChanged;
        this.myEngine = this.Model!.Manager!.Engine;

        this.myEngine.ConnectionChanged += this.OnEngineConnectionChanged;
        this.myEngine.ConnectionAboutToChange += this.OnConnectionAboutToChange;

        DataManager.GetContextData(this).Set(ITaskSequencerUI.TaskSequenceDataKey, this.Model!);
    }

    protected override void OnRemovingFromList() {
        this.nameBinder.DetachModel();
        this.busyLockPriorityBinder.DetachModel();
        this.repeatBinder.DetachModel();
        this.Model!.IsRunningChanged -= this.OnIsRunningChanged;
        this.myEngine!.ConnectionChanged -= this.OnEngineConnectionChanged;
        this.myEngine!.ConnectionAboutToChange -= this.OnConnectionAboutToChange;
        this.myEngine = null;

        DataManager.GetContextData(this).Set(ITaskSequencerUI.TaskSequenceDataKey, null);
    }

    protected override void OnRemovedFromList() {
    }

    private void OnIsRunningChanged(TaskSequence sender) {
        this.runCommand.RaiseCanExecuteChanged();
        this.cancelCommand.RaiseCanExecuteChanged();
    }

    private void OnEngineConnectionChanged(MemoryEngine360 sender, IConsoleConnection? oldconnection, IConsoleConnection? newconnection, ConnectionChangeCause cause) {
        this.runCommand.RaiseCanExecuteChanged();
        this.cancelCommand.RaiseCanExecuteChanged();
    }

    private async Task OnConnectionAboutToChange(MemoryEngine360 sender, IActivityProgress progress) {
        await this.cancelCommand.ExecuteAsync(null);
    }
}