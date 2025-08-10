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
using Avalonia.Interactivity;
using MemEngine360.BaseFrontEnd.TaskSequencing.Operations;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.Addressing;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Conditions;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.Sequencing.Operations;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public partial class TaskSequencerWindow : DesktopWindow, ITaskSequenceManagerUI {
    public static readonly StyledProperty<TaskSequencerManager?> TaskSequencerManagerProperty = AvaloniaProperty.Register<TaskSequencerWindow, TaskSequencerManager?>(nameof(TaskSequencerManager));

    private readonly IBinder<TaskSequence> useDedicatedConnectionBinder = new EventUpdateBinder<TaskSequence>(nameof(TaskSequence.UseEngineConnectionChanged), (b) => {
        ((CheckBox) b.Control).IsChecked = !b.Model.UseEngineConnection;
    });

    private readonly IBinder<TaskSequence> currentConnectionTypeBinder = new MultiEventUpdateBinder<TaskSequence>([nameof(TaskSequence.UseEngineConnectionChanged), nameof(TaskSequence.DedicatedConnectionChanged)], (b) => {
        ((TextBlock) b.Control).Opacity = b.Model.UseEngineConnection ? 0.6 : 1.0;
        if (b.Model.UseEngineConnection) {
            ((TextBlock) b.Control).Text = (b.Model.Manager?.MemoryEngine.Connection?.ConnectionType.DisplayName ?? "No Engine Connection");
        }
        else {
            ((TextBlock) b.Control).Text = (b.Model.DedicatedConnection?.ConnectionType.DisplayName ?? "Not Connected");
        }
    });

    public TaskSequencerManager? TaskSequencerManager {
        get => this.GetValue(TaskSequencerManagerProperty);
        set => this.SetValue(TaskSequencerManagerProperty, value);
    }

    public bool IsValid => this.TaskSequencerManager != null;

    public TaskSequencerManager Manager => this.TaskSequencerManager ?? throw new Exception("Window Closed");

    public IListSelectionManager<ITaskSequenceEntryUI> SequenceSelectionManager => this.PART_SequenceListBox.ControlSelectionManager;

    public IListSelectionManager<IOperationItemUI> OperationSelectionManager => this.PART_OperationListBox.ControlSelectionManager;

    public IListSelectionManager<IConditionItemUI> ConditionSelectionManager => this.PART_ConditionsListBox.ControlSelectionManager;


    public ITaskSequenceEntryUI? PrimarySelectedSequence { get; private set; }

    public IOperationItemUI? PrimarySelectedOperation { get; private set; }

    public TaskSequencerWindow() {
        this.InitializeComponent();
        this.PART_OperationListBox.TaskSequencerUI = this;

        this.SequenceSelectionManager.LightSelectionChanged += this.OnSequenceSelectionChanged;
        this.OperationSelectionManager.LightSelectionChanged += this.OnOperationSelectionChanged;

        this.PART_UseDedicatedConnection.Command = new AsyncRelayCommand(async () => {
            ITaskSequenceEntryUI? seqUI = this.PrimarySelectedSequence;
            if (seqUI == null || seqUI.TaskSequence.IsRunning) {
                return;
            }

            TaskSequence seq = seqUI.TaskSequence;
            if (seq.UseEngineConnection) {
                seq.UseEngineConnection = false;
            }
            else {
                IConsoleConnection? oldConnection = seq.DedicatedConnection;
                if (oldConnection != null) {
                    seq.DedicatedConnection = null;
                    oldConnection.Close();
                }

                seq.UseEngineConnection = true;
            }
        }, () => {
            ITaskSequenceEntryUI? seqUI = this.PrimarySelectedSequence;
            return seqUI != null && !seqUI.TaskSequence.IsRunning;
        });

        this.useDedicatedConnectionBinder.AttachControl(this.PART_UseDedicatedConnection);
        this.currentConnectionTypeBinder.AttachControl(this.PART_ActiveConnectionTextBoxRO);

        DataManager.GetContextData(this).Set(ITaskSequenceManagerUI.DataKey, this);
        if (Design.IsDesignMode) {
            this.TaskSequencerManager = new MemoryEngine().TaskSequencerManager;
        }
    }

    protected override void OnClosed(EventArgs e) {
        // Prevent memory leaks
        this.TaskSequencerManager = null;
        base.OnClosed(e);
    }
    
    public void OnSequenceItemTapped(SequenceListBoxItem item) {
        this.SequenceSelectionManager.SetSelection(item);
        this.OnSequenceItemTappedImpl(item);
    }
    
    public void OnSequenceItemTappedImpl(SequenceListBoxItem? item) {
        this.PART_OperationListBox.TaskSequence = item?.TaskSequence;
        this.PART_ConditionsListBox.TaskSequence = item?.TaskSequence;
        this.PART_ConditionsListBox.ConditionsHost = item?.TaskSequence;
        
        int selectionCount = this.SequenceSelectionManager.Count;
        this.PART_SelectedSequenceTextBlock.Text = item?.TaskSequence.DisplayName ?? (selectionCount == 0 ? "(No sequence selected)" : "(Too many sequences selected)");
        this.PART_ConditionSourceName.Text = item?.TaskSequence.DisplayName ?? (selectionCount == 0 ? "(No sequence selected)" : "(Too many sequences selected)");

        ITaskSequenceEntryUI? oldSequenceUI = this.PrimarySelectedSequence;
        if (!ReferenceEquals(oldSequenceUI, item)) {
            this.PrimarySelectedSequence = item;
            this.OnPrimarySelectedSequenceChanged(oldSequenceUI, item);
        }
    }

    private void OnSequenceSelectionChanged(ILightSelectionManager<ITaskSequenceEntryUI> sender) {
        ITaskSequenceEntryUI? newSequenceUI = sender.Count == 1 ? ((IListSelectionManager<ITaskSequenceEntryUI>) sender).SelectedItemList[0] : null;
        this.OnSequenceItemTappedImpl((SequenceListBoxItem?) newSequenceUI);
    }
    
    private void OnPrimarySelectedSequenceChanged(ITaskSequenceEntryUI? oldSeqUI, ITaskSequenceEntryUI? newSeqUI) {
        if (oldSeqUI != null) {
            oldSeqUI.TaskSequence.Progress.TextChanged -= this.OnSequenceProgressTextChanged;
            oldSeqUI.TaskSequence.IsRunningChanged -= this.OnPrimarySequenceIsRunningChanged;
        }

        if (newSeqUI != null) {
            newSeqUI.TaskSequence.Progress.TextChanged += this.OnSequenceProgressTextChanged;
            newSeqUI.TaskSequence.IsRunningChanged += this.OnPrimarySequenceIsRunningChanged;
        }

        this.PART_CurrentSequenceGroupBox.IsEnabled = newSeqUI != null;

        this.useDedicatedConnectionBinder.SwitchModel(newSeqUI?.TaskSequence);
        this.currentConnectionTypeBinder.SwitchModel(newSeqUI?.TaskSequence);
        this.OnSequenceProgressTextChanged(newSeqUI?.TaskSequence.Progress ?? EmptyActivityProgress.Instance);
        ((AsyncRelayCommand) this.PART_UseDedicatedConnection.Command!).RaiseCanExecuteChanged();

        DataManager.GetContextData(this.PART_CurrentSequenceGroupBox).Set(ITaskSequenceEntryUI.DataKey, newSeqUI);
    }
    
    private void OnOperationSelectionChanged(ILightSelectionManager<IOperationItemUI> sender) {
        IOperationItemUI? newPrimary = sender.Count == 1 ? ((IListSelectionManager<IOperationItemUI>) sender).SelectedItemList[0] : null;

        // I'm too lazy at the moment to replace this rawdogging the properties with Binders.
        // May do it at some point, but it works...
        if (newPrimary != null) {
            this.PART_PrimarySelectedOperationText.Text = $"Editing '{newPrimary.Operation.DisplayName}'";
            
            this.PART_ConditionsListBox.TaskSequence = newPrimary.Operation.TaskSequence;
            this.PART_ConditionsListBox.ConditionsHost = newPrimary.Operation;
        
            // this.PART_SelectedSequenceTextBlock.Text = newPrimary.Operation.DisplayName;
            this.PART_ConditionSourceName.Text = newPrimary.Operation.DisplayName;
        }
        else {
            this.PART_PrimarySelectedOperationText.Text = sender.Count == 0 ? "(No operation selected)" : "(Too many operations selected)";
            
            this.PART_ConditionsListBox.TaskSequence = null;
            this.PART_ConditionsListBox.ConditionsHost = null;
        
            // this.PART_SelectedSequenceTextBlock.Text = sender.Count == 0 ? "(No operation selected)" : "(Too many operations selected)";
            this.PART_ConditionSourceName.Text = sender.Count == 0 ? "(No operation selected)" : "(Too many operations selected)";
        }

        if (this.PrimarySelectedOperation != null) {
            this.PART_OperationEditorControlsListBox.SetOperation(null);
            this.PrimarySelectedOperation = null;
        }

        if (newPrimary != null) {
            this.PrimarySelectedOperation = newPrimary;
            this.PART_OperationEditorControlsListBox.SetOperation(newPrimary.Operation);
        }
    }

    private void OnPrimarySequenceIsRunningChanged(TaskSequence sender) {
        ((AsyncRelayCommand) this.PART_UseDedicatedConnection.Command!).RaiseCanExecuteChanged();
    }

    static TaskSequencerWindow() {
        TaskSequencerManagerProperty.Changed.AddClassHandler<TaskSequencerWindow, TaskSequencerManager?>((s, e) => s.OnTaskSequencerManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnTaskSequencerManagerChanged(TaskSequencerManager? oldManager, TaskSequencerManager? newManager) {
        this.PART_SequenceListBox.TaskSequencerManager = newManager;
        if (newManager != null && newManager.Sequences.Count > 0) {
            this.SequenceSelectionManager.Select((ITaskSequenceEntryUI) this.PART_SequenceListBox.ItemMap.GetControl(newManager.Sequences[0]));
        }

        if (oldManager != null)
            oldManager.MemoryEngine.ConnectionChanged -= this.OnEngineConnectionChanged;
        if (newManager != null)
            newManager.MemoryEngine.ConnectionChanged += this.OnEngineConnectionChanged;
    }

    private void OnEngineConnectionChanged(MemoryEngine sender, ulong frame, IConsoleConnection? oldconnection, IConsoleConnection? newconnection, ConnectionChangeCause cause) {
        ITaskSequenceEntryUI? seq = this.PrimarySelectedSequence;
        if (seq != null && seq.TaskSequence.UseEngineConnection)
            this.currentConnectionTypeBinder.UpdateControl();
    }

    private void OnSequenceProgressTextChanged(IActivityProgress? tracker) {
        this.PART_ActivityStatusText.Text = tracker?.Text ?? "";
    }

    private void PART_ClearOperationsClick(object? sender, RoutedEventArgs e) {
        if (this.PrimarySelectedSequence != null && !this.PrimarySelectedSequence.TaskSequence.IsRunning)
            this.PrimarySelectedSequence.TaskSequence.Operations.Clear();
    }

    private void Button_InsertDelay(object? sender, RoutedEventArgs e) {
        this.AddOperationAndSelect(() => new DelayOperation(500));
    }

    private void Button_InsertSetMemory(object? sender, RoutedEventArgs e) {
        this.AddOperationAndSelect(() => new SetMemoryOperation() { Address = new StaticAddress(0x82600000), DataValueProvider = new ConstantDataProvider(new DataValueInt32(125)) });
    }

    private void Button_InsertJumpTo(object? sender, RoutedEventArgs e) {
        this.AddOperationAndSelect(() => new JumpToLabelOperation());
    }

    private void Button_InsertLabel(object? sender, RoutedEventArgs e) {
        this.AddOperationAndSelect(() => new LabelOperation());
    }
    private void Button_InsertStopSequence(object? sender, RoutedEventArgs e) {
        this.AddOperationAndSelect(() => new StopSequenceOperation());
    }

    private void AddOperationAndSelect(Func<BaseSequenceOperation> factory) {
        if (this.PrimarySelectedSequence != null && !this.PrimarySelectedSequence.TaskSequence.IsRunning) {
            BaseSequenceOperation operation = factory();
            this.PrimarySelectedSequence.TaskSequence.Operations.Add(operation);
            ModelBasedListBoxItem<BaseSequenceOperation> control = this.PART_OperationListBox.ItemMap.GetControl(operation);
            this.OperationSelectionManager.SetSelection((OperationListBoxItem) control);
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
    
    public ITaskSequenceEntryUI GetSequenceControl(TaskSequence sequence) {
        return (ITaskSequenceEntryUI) this.PART_SequenceListBox.ItemMap.GetControl(sequence);
    }

    public IOperationItemUI GetOperationControl(BaseSequenceOperation operation) {
        return (IOperationItemUI) this.PART_OperationListBox.ItemMap.GetControl(operation);
    }

    public IConditionItemUI GetConditionControl(BaseSequenceCondition condition) {
        return (IConditionItemUI) this.PART_ConditionsListBox.ItemMap.GetControl(condition);
    }
}