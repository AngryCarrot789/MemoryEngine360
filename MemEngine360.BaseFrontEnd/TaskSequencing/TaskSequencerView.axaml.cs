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
using Avalonia.Interactivity;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.Addressing;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Conditions;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.Sequencing.Operations;
using MemEngine360.Sequencing.View;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Contexts;
using PFXToolKitUI.Avalonia.Interactivity.SelectingEx2;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Utils.Commands;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public partial class TaskSequencerView : UserControl {
    private readonly IBinder<TaskSequence> useDedicatedConnectionBinder = 
        new EventUpdateBinder<TaskSequence>(
            [
                nameof(TaskSequence.UseEngineConnectionChanged),
                nameof(TaskSequence.IsRunningChanged),
            ], 
            (b) => {
                ((CheckBox) b.Control).IsEnabled = !b.Model.IsRunning;
                ((CheckBox) b.Control).IsChecked = !b.Model.UseEngineConnection;
            });

    private readonly IBinder<TaskSequence> currentConnectionTypeBinder =
        new EventUpdateBinder<TaskSequence>(
            [
                nameof(TaskSequence.UseEngineConnectionChanged),
                nameof(TaskSequence.DedicatedConnectionChanged)
            ],
            (b) => {
                ((TextBlock) b.Control).Opacity = b.Model.UseEngineConnection ? 0.6 : 1.0;
                if (b.Model.UseEngineConnection) {
                    ((TextBlock) b.Control).Text = (b.Model.Manager?.MemoryEngine.Connection?.ConnectionType.DisplayName ?? "No Engine Connection");
                }
                else {
                    ((TextBlock) b.Control).Text = (b.Model.DedicatedConnection?.ConnectionType.DisplayName ?? "Not Connected");
                }
            });

    private readonly IBinder<TaskSequence> busyLockPriorityBinder =
        new AvaloniaPropertyToMultiEventPropertyBinder<TaskSequence>(
            CheckBox.IsCheckedProperty,
            [
                nameof(TaskSequence.HasEngineConnectionPriorityChanged),
                nameof(TaskSequence.UseEngineConnectionChanged),
                nameof(TaskSequence.IsRunningChanged)
            ],
            (b) => {
                ((CheckBox) b.Control).IsEnabled = b.Model.UseEngineConnection && !b.Model.IsRunning;
                ((CheckBox) b.Control).IsChecked = b.Model.HasEngineConnectionPriority && b.Model.UseEngineConnection;
            }, (b) => {
                if (!b.Model.IsRunning && b.Model.UseEngineConnection)
                    b.Model.HasEngineConnectionPriority = ((CheckBox) b.Control).IsChecked == true;
            });

    public TaskSequenceManagerViewState State { get; }

    public IDesktopWindow? Window { get; private set; }

    public TaskSequenceManager TaskSequenceManager { get; }

    private readonly ConditionSourcePresenter conditionSourcePresenter;
    private readonly OperationListPresenter operationListPresenter;
    private SelectionModelBinder<TaskSequence>? taskSequenceSelectionHandler;

    public event EventHandler? WindowOpened, WindowClosed;

    public TaskSequencerView() : this(new TaskSequenceManager(new MemoryEngine())) {
    }

    public TaskSequencerView(TaskSequenceManager manager) {
        this.InitializeComponent();
        this.TaskSequenceManager = manager;
        this.State = TaskSequenceManagerViewState.GetInstance(manager);
        this.PART_UseDedicatedConnection.Command = new RelayCommand(this.OnToggleUseDedicatedConnection, () => {
            TaskSequence? seqUI = this.State.PrimarySelectedSequence;
            return seqUI != null && !seqUI.IsRunning;
        });

        this.useDedicatedConnectionBinder.AttachControl(this.PART_UseDedicatedConnection);
        this.currentConnectionTypeBinder.AttachControl(this.PART_ActiveConnectionTextBoxRO);
        this.busyLockPriorityBinder.AttachControl(this.PART_ToggleBusyExclusive);

        this.conditionSourcePresenter = new ConditionSourcePresenter(this);
        this.operationListPresenter = new OperationListPresenter(this);
    }

    private void OnToggleUseDedicatedConnection() {
        TaskSequence? sequence = this.State.PrimarySelectedSequence;
        if (sequence == null || sequence.IsRunning) {
            return;
        }

        if (sequence.UseEngineConnection) {
            sequence.UseEngineConnection = false;
        }
        else {
            IConsoleConnection? oldConnection = sequence.DedicatedConnection;
            if (oldConnection != null) {
                sequence.DedicatedConnection = null;
                oldConnection.Close();
            }

            sequence.UseEngineConnection = true;
        }
    }

    private void OnEngineConnectionChanged(object? o, ConnectionChangedEventArgs connectionChangedEventArgs) {
        TaskSequence? seq = this.State.PrimarySelectedSequence;
        if (seq != null && seq.UseEngineConnection) {
            this.currentConnectionTypeBinder.UpdateControl();
        }
    }

    private void OnPrimarySelectedSequenceChanged(object? o, ValueChangedEventArgs<TaskSequence?> e) {
        this.OnPrimarySequenceChanged(e.OldValue, e.NewValue);
    }

    private void OnPrimarySequenceChanged(TaskSequence? oldSeq, TaskSequence? newSeq) {
        if (oldSeq != null) {
            oldSeq.Progress.TextChanged -= this.OnSequenceProgressTextChanged;
            oldSeq.IsRunningChanged -= this.OnPrimarySequenceIsRunningChanged;
        }

        IControlContextData context = DataManager.GetContextData(this.PART_CurrentSequenceGroupBox);
        if (newSeq != null) {
            newSeq.Progress.TextChanged += this.OnSequenceProgressTextChanged;
            newSeq.IsRunningChanged += this.OnPrimarySequenceIsRunningChanged;
            context.Set(TaskSequence.DataKey, newSeq);
        }
        else {
            context.Remove(TaskSequence.DataKey);
        }

        this.PART_CurrentSequenceGroupBox.IsEnabled = newSeq != null;

        this.useDedicatedConnectionBinder.SwitchModel(newSeq);
        this.currentConnectionTypeBinder.SwitchModel(newSeq);
        this.busyLockPriorityBinder.SwitchModel(newSeq);
        this.OnSequenceProgressTextChanged(newSeq?.Progress, EventArgs.Empty);
        ((BaseRelayCommand) this.PART_UseDedicatedConnection.Command!).RaiseCanExecuteChanged();
    }

    private void OnPrimarySequenceIsRunningChanged(object? o, EventArgs e) {
        ((BaseRelayCommand) this.PART_UseDedicatedConnection.Command!).RaiseCanExecuteChanged();
    }

    private void OnSequenceProgressTextChanged(object? sender, EventArgs e) {
        this.PART_ActivityStatusText.Text = ((IActivityProgress?) sender)?.Text ?? "";
    }
    
    private void PART_ClearOperationsClick(object? sender, RoutedEventArgs e) {
        TaskSequence? sequence = this.State.PrimarySelectedSequence;
        if (sequence != null && !sequence.IsRunning) {
            sequence.Operations.Clear();
        }
    }

    private void Button_InsertDelay(object? sender, RoutedEventArgs e) => this.AddOperationAndSelect(() => new DelayOperation(500));

    private void Button_InsertSetMemory(object? sender, RoutedEventArgs e) {
        this.AddOperationAndSelect(() => new SetMemoryOperation() {
            Address = new StaticAddress(0x82600000),
            DataValueProvider = new ConstantDataProvider(new DataValueInt32(125))
        });
    }

    private void Button_InsertJumpTo(object? sender, RoutedEventArgs e) => this.AddOperationAndSelect(() => new JumpToLabelOperation());

    private void Button_InsertLabel(object? sender, RoutedEventArgs e) => this.AddOperationAndSelect(() => new LabelOperation());

    private void Button_InsertStopSequence(object? sender, RoutedEventArgs e) => this.AddOperationAndSelect(() => new StopSequenceOperation());

    private void AddOperationAndSelect(Func<BaseSequenceOperation> factory) {
        TaskSequenceManagerViewState state = this.State;
        if (state.PrimarySelectedSequence != null && !state.PrimarySelectedSequence.IsRunning) {
            state.SelectedOperations!.DeselectAll();

            BaseSequenceOperation operation = factory();
            state.PrimarySelectedSequence.Operations.Add(operation);
            state.SelectedOperations!.SelectItem(operation);
        }
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e) {
        IConditionsHost? target = this.PART_ConditionsListBox.ConditionsHost;
        if (target != null && !target.TaskSequence!.IsRunning) {
            target.Conditions.Add(new CompareMemoryCondition() {
                CompareTo = new DataValueInt32(0), CompareType = CompareType.Equals
            });
        }
    }

    internal void OnWindowOpened(IDesktopWindow sender) {
        this.Window = sender;
        DataManager.GetContextData(this).Set(TaskSequenceManager.DataKey, this.TaskSequenceManager);

        this.PART_SequenceListBox.TaskSequencerManager = this.TaskSequenceManager;
        this.taskSequenceSelectionHandler = new SelectionModelBinder<TaskSequence>(this.PART_SequenceListBox.Selection, this.State.SelectedSequences);

        this.TaskSequenceManager.MemoryEngine.ConnectionChanged += this.OnEngineConnectionChanged;
        this.State.PrimarySelectedSequenceChanged += this.OnPrimarySelectedSequenceChanged;

        if (this.State.PrimarySelectedSequence != null) {
            this.OnPrimarySequenceChanged(null, this.State.PrimarySelectedSequence);
        }

        if (this.State.SelectedSequences.Count < 1 && this.TaskSequenceManager.Sequences.Count > 0) {
            this.State.SelectedSequences.SelectItem(this.TaskSequenceManager.Sequences[0]);
        }

        this.WindowOpened?.Invoke(this, EventArgs.Empty);
    }

    internal void OnWindowClosed() {
        this.WindowClosed?.Invoke(this, EventArgs.Empty);

        DataManager.GetContextData(this).Remove(TaskSequenceManager.DataKey);
        this.taskSequenceSelectionHandler!.Dispose();
        this.PART_SequenceListBox.TaskSequencerManager = null;

        this.State.PrimarySelectedSequenceChanged -= this.OnPrimarySelectedSequenceChanged;
        if (this.State.PrimarySelectedSequence != null) {
            this.OnPrimarySequenceChanged(this.State.PrimarySelectedSequence, null);
        }

        this.TaskSequenceManager.MemoryEngine.ConnectionChanged -= this.OnEngineConnectionChanged;
        this.Window = null;
    }
}