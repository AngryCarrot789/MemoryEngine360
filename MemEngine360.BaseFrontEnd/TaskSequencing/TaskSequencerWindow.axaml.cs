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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Operations;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Tasks;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public partial class TaskSequencerWindow : DesktopWindow, ITaskSequencerUI {
    public static readonly StyledProperty<TaskSequencerManager?> TaskSequencerManagerProperty = AvaloniaProperty.Register<TaskSequencerWindow, TaskSequencerManager?>(nameof(TaskSequencerManager));

    public TaskSequencerManager? TaskSequencerManager {
        get => this.GetValue(TaskSequencerManagerProperty);
        set => this.SetValue(TaskSequencerManagerProperty, value);
    }
    
    public TaskSequencerManager Manager => this.TaskSequencerManager ?? throw new Exception("Window Closed");

    private readonly IBinder<TaskSequencerManager> selectedSequenceBinder1 = new EventPropertyBinder<TaskSequencerManager>(nameof(Sequencing.TaskSequencerManager.SelectedSequenceChanged), (b) => ((OperationListBox) b.Control).TaskSequence = b.Model.SelectedSequence);
    private readonly IBinder<TaskSequencerManager> selectedSequenceBinder2 = new EventPropertyBinder<TaskSequencerManager>(nameof(Sequencing.TaskSequencerManager.SelectedSequenceChanged), (b) => ((TextBlock) b.Control).Text = b.Model.SelectedSequence?.DisplayName ?? "<No Sequence Select>");
    
    public TaskSequencerWindow() {
        this.InitializeComponent();
        this.selectedSequenceBinder1.AttachControl(this.PART_OperationListBox);
        this.selectedSequenceBinder2.AttachControl(this.PART_SelectedSequenceTextBlock);
        DataManager.GetContextData(this).Set(ITaskSequencerUI.TaskSequencerUIDataKey, this);
    }
    
    static TaskSequencerWindow() {
        TaskSequencerManagerProperty.Changed.AddClassHandler<TaskSequencerWindow, TaskSequencerManager?>((s, e) => s.OnTaskSequencerManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }
    
    private void OnTaskSequencerManagerChanged(TaskSequencerManager? oldManager, TaskSequencerManager? newManager) {
        this.PART_SequenceListBox.TaskSequencerManager = newManager;
        if (oldManager != null) {
            oldManager.SelectedSequenceChanged -= this.OnSelectedSequenceChanged;
            if (oldManager.SelectedSequence != null)
                this.OnSelectedSequenceChanged(oldManager, oldManager.SelectedSequence, null);
        }

        if (newManager != null) {
            newManager.SelectedSequenceChanged += this.OnSelectedSequenceChanged;
            if (newManager.SelectedSequence != null)
                this.OnSelectedSequenceChanged(newManager, null, newManager.SelectedSequence);
        }

        this.selectedSequenceBinder1.SwitchModel(newManager);
        this.selectedSequenceBinder2.SwitchModel(newManager);
    }

    private void OnSelectedSequenceChanged(TaskSequencerManager sender, TaskSequence? oldSeq, TaskSequence? newSeq) {
        if (oldSeq != null)
            oldSeq.Progress.TextChanged -= this.OnSequenceProgressTextChanged;
        if (newSeq != null)
            newSeq.Progress.TextChanged += this.OnSequenceProgressTextChanged;
        
        this.OnSequenceProgressTextChanged(newSeq?.Progress ?? EmptyActivityProgress.Instance);
    }

    private void OnSequenceProgressTextChanged(IActivityProgress tracker) {
        this.PART_ActivityStatusText.Text = tracker.Text;
    }

    private void PART_ClearOperationsClick(object? sender, RoutedEventArgs e) {
        if (this.TaskSequencerManager is TaskSequencerManager tsm && tsm.SelectedSequence != null) {
            tsm.SelectedSequence.ClearOperations();
        }
    }

    private void Button_InsertDelay(object? sender, RoutedEventArgs e) {
        if (this.TaskSequencerManager is TaskSequencerManager tsm && tsm.SelectedSequence != null) {
            tsm.SelectedSequence.AddOperation(new DelayOperation(500));
        }
    }

    private void Button_InsertSetMemory(object? sender, RoutedEventArgs e) {
        if (this.TaskSequencerManager is TaskSequencerManager tsm && tsm.SelectedSequence != null) {
            tsm.SelectedSequence.AddOperation(new SetMemoryOperation() {Address = 0x82600000, DataValue = new DataValueInt32(125)});
        }
    }
}