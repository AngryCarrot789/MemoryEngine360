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

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MemEngine360.BaseFrontEnd.TaskSequencing.EditorContent;
using MemEngine360.Engine;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Operations;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Selecting;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Tasks;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public partial class TaskSequencerWindow : DesktopWindow, ITaskSequencerUI {
    public static readonly StyledProperty<TaskSequencerManager?> TaskSequencerManagerProperty = AvaloniaProperty.Register<TaskSequencerWindow, TaskSequencerManager?>(nameof(TaskSequencerManager));

    public TaskSequencerManager? TaskSequencerManager {
        get => this.GetValue(TaskSequencerManagerProperty);
        set => this.SetValue(TaskSequencerManagerProperty, value);
    }
    
    public TaskSequencerManager Manager => this.TaskSequencerManager ?? throw new Exception("Window Closed");

    public IListSelectionManager<ITaskSequenceEntryUI> SequenceSelectionManager => this.PART_SequenceListBox.ControlSelectionManager;

    public IListSelectionManager<IOperationItemUI> OperationSelectionManager => this.PART_OperationListBox.ControlSelectionManager;
    
    public ITaskSequenceEntryUI? PrimarySelectedSequence { get; private set; }

    public IOperationItemUI? PrimarySelectedOperation { get; private set; }

    private readonly Dictionary<Type, BaseOperationEditorContent> itemEditorCacheMap;
    
    public TaskSequencerWindow() {
        this.InitializeComponent();
        this.itemEditorCacheMap = new Dictionary<Type, BaseOperationEditorContent>();
        
        this.PART_OperationListBox.TaskSequencerUI = this;
        
        this.SequenceSelectionManager.LightSelectionChanged += this.OnSelectionChanged;
        this.OperationSelectionManager.LightSelectionChanged += this.OnOperationSelectionChanged;
        
        DataManager.GetContextData(this).Set(ITaskSequencerUI.TaskSequencerUIDataKey, this);

        if (Design.IsDesignMode) {
            this.TaskSequencerManager = new MemoryEngine360().TaskSequencerManager;
        }
    }

    public ITaskSequenceEntryUI GetControl(TaskSequence sequence) {
        return (ITaskSequenceEntryUI) this.PART_SequenceListBox.ItemMap.GetControl(sequence);
    }
    
    private void OnSelectionChanged(ILightSelectionManager<ITaskSequenceEntryUI> sender) {
        ITaskSequenceEntryUI? newPrimary = sender.Count == 1 ? ((IListSelectionManager<ITaskSequenceEntryUI>) sender).SelectedItemList[0] : null;
        this.PART_OperationListBox.TaskSequence = newPrimary?.TaskSequence;
        this.PART_SelectedSequenceTextBlock.Text = newPrimary?.TaskSequence.DisplayName ?? (sender.Count == 0 ? "(No Sequence Selected)" : "(Too many sequences selected)");
        
        if (this.PrimarySelectedSequence != null)
            this.PrimarySelectedSequence.TaskSequence.Progress.TextChanged -= this.OnSequenceProgressTextChanged;
        if (newPrimary != null)
            newPrimary.TaskSequence.Progress.TextChanged += this.OnSequenceProgressTextChanged;
        
        this.PrimarySelectedSequence = newPrimary;
        this.OnSequenceProgressTextChanged(newPrimary?.TaskSequence.Progress ?? EmptyActivityProgress.Instance);
    }
    
    private void OnOperationSelectionChanged(ILightSelectionManager<IOperationItemUI> sender) {
        IOperationItemUI? newPrimary = sender.Count == 1 ? ((IListSelectionManager<IOperationItemUI>) sender).SelectedItemList[0] : null;
        this.PART_PrimarySelectedOperationText.Text = newPrimary?.Operation.DisplayName ?? (sender.Count == 0 ? "(No operation Selected)" : "(Too many operations selected)");

        if (this.PrimarySelectedOperation != null) {
            BaseOperationEditorContent content = (BaseOperationEditorContent) this.PART_SelectedOperationEditorControl.Content!;
            BaseSequenceOperation operation = content.Operation!;
            Debug.Assert(ReferenceEquals(this.PrimarySelectedOperation.Operation, operation));
            
            content.Operation = null;
            this.PART_SelectedOperationEditorControl.Content = null;
            this.ReleaseOperationEditorObject(operation, content);
        }

        if (newPrimary != null) {
            BaseOperationEditorContent content = this.GetOperationEditorObject(newPrimary.Operation);
            this.PART_SelectedOperationEditorControl.Content = content;
            content.Operation = newPrimary.Operation;
        }

        this.PrimarySelectedOperation = newPrimary;
    }

    static TaskSequencerWindow() {
        TaskSequencerManagerProperty.Changed.AddClassHandler<TaskSequencerWindow, TaskSequencerManager?>((s, e) => s.OnTaskSequencerManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }
    
    public BaseOperationEditorContent GetOperationEditorObject(BaseSequenceOperation operation) {
        return this.itemEditorCacheMap.Remove(operation.GetType(), out BaseOperationEditorContent? cachedEditor) 
            ? cachedEditor 
            : BaseOperationEditorContent.Registry.NewInstance(operation.GetType());
    }

    public bool ReleaseOperationEditorObject(BaseSequenceOperation operation, BaseOperationEditorContent editor) {
        return this.itemEditorCacheMap.TryAdd(operation.GetType(), editor);
    }
    
    private void OnTaskSequencerManagerChanged(TaskSequencerManager? oldManager, TaskSequencerManager? newManager) {
        this.PART_SequenceListBox.TaskSequencerManager = newManager;
        if (newManager != null && newManager.Sequences.Count > 0)
            this.SequenceSelectionManager.Select((ITaskSequenceEntryUI) this.PART_SequenceListBox.ItemMap.GetControl(newManager.Sequences[0]));
    }

    private void OnSequenceProgressTextChanged(IActivityProgress tracker) {
        this.PART_ActivityStatusText.Text = tracker.Text;
    }

    private void PART_ClearOperationsClick(object? sender, RoutedEventArgs e) {
        this.PrimarySelectedSequence?.TaskSequence.ClearOperations();
    }

    private void Button_InsertDelay(object? sender, RoutedEventArgs e) {
        this.PrimarySelectedSequence?.TaskSequence.AddOperation(new DelayOperation(500));
    }

    private void Button_InsertSetMemory(object? sender, RoutedEventArgs e) {
        this.PrimarySelectedSequence?.TaskSequence.AddOperation(new SetMemoryOperation() {Address = 0x82600000, DataValue = new DataValueInt32(125)});
    }
}