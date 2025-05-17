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
using MemEngine360.BaseFrontEnd.TaskSequencing.ListContent;
using MemEngine360.Sequencing;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class OperationListBox : ModelBasedListBox<BaseSequenceOperation> {
    public static readonly StyledProperty<TaskSequence?> TaskSequenceProperty = AvaloniaProperty.Register<OperationListBox, TaskSequence?>(nameof(TaskSequence));

    public TaskSequence? TaskSequence {
        get => this.GetValue(TaskSequenceProperty);
        set => this.SetValue(TaskSequenceProperty, value);
    }

    public ITaskSequencerUI TaskSequencerUI { get; internal set; }

    private readonly Dictionary<Type, Stack<BaseOperationListContent>> itemContentCacheMap;

    public OperationListBox() : base(32) {
        this.itemContentCacheMap = new Dictionary<Type, Stack<BaseOperationListContent>>();
    }

    static OperationListBox() {
        TaskSequenceProperty.Changed.AddClassHandler<OperationListBox, TaskSequence?>((s, e) => s.OnTaskSequenceChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnTaskSequenceChanged(TaskSequence? oldSeq, TaskSequence? newSeq) {
        this.SetItemsSource(newSeq?.Operations);
    }

    protected override ModelBasedListBoxItem<BaseSequenceOperation> CreateItem() => new OperationListBoxItem();

    public BaseOperationListContent GetContentObject(BaseSequenceOperation operation) {
        BaseOperationListContent content;
        if (this.itemContentCacheMap.TryGetValue(operation.GetType(), out Stack<BaseOperationListContent>? stack) && stack.Count > 0) {
            content = stack.Pop();
        }
        else {
            content = BaseOperationListContent.Registry.NewInstance(operation.GetType());
        }

        return content;
    }

    public bool ReleaseContentObject(BaseSequenceOperation operation, BaseOperationListContent content) {
        const int MaxItemContentCacheSize = 64;
        
        Type resourceType = operation.GetType();
        if (!this.itemContentCacheMap.TryGetValue(resourceType, out Stack<BaseOperationListContent>? stack)) {
            this.itemContentCacheMap[resourceType] = stack = new Stack<BaseOperationListContent>();
        }
        else if (stack.Count == MaxItemContentCacheSize) {
            return false;
        }

        stack.Push(content);
        return true;
    }

    public ITaskSequenceEntryUI GetTaskSequence(OperationListBoxItem item) {
        return this.TaskSequencerUI.GetControl(item.Model!.Sequence!);
    }
}