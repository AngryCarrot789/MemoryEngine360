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
using Avalonia.Input;
using MemEngine360.BaseFrontEnd.TaskSequencing.Operations.ListContent;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.View;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Operations;

public class OperationListBox : ModelBasedListBox<BaseSequenceOperation> {
    public static readonly StyledProperty<TaskSequence?> TaskSequenceProperty = AvaloniaProperty.Register<OperationListBox, TaskSequence?>(nameof(TaskSequence));

    private readonly Dictionary<Type, Stack<BaseOperationListContent>> itemContentCacheMap;
    
    public TaskSequence? TaskSequence {
        get => this.GetValue(TaskSequenceProperty);
        set => this.SetValue(TaskSequenceProperty, value);
    }

    protected override bool CanDragItemPositionCore => this.TaskSequence != null && !this.TaskSequence.IsRunning;

    public OperationListBox() : base(32) {
        this.itemContentCacheMap = new Dictionary<Type, Stack<BaseOperationListContent>>();
    }

    static OperationListBox() {
        TaskSequenceProperty.Changed.AddClassHandler<OperationListBox, TaskSequence?>((s, e) => s.OnTaskSequenceChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        // Fix toggle enabled command. For some reason pressing space deselects all
        // but the anchored item when we have multiple selections
        if (e.Key != Key.Space) {
            base.OnKeyDown(e);
        }
    }

    protected override void MoveItemIndexOverride(int oldIndex, int newIndex) {
        this.TaskSequence?.Operations.Move(oldIndex, newIndex);
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
}