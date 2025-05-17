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
using MemEngine360.BaseFrontEnd.TaskSequencing.OperationControls;
using MemEngine360.Sequencing;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class OperationListBox : ModelBasedListBox<BaseSequenceOperation> {
    public static readonly StyledProperty<TaskSequence?> TaskSequenceProperty = AvaloniaProperty.Register<OperationListBox, TaskSequence?>(nameof(TaskSequence));

    public TaskSequence? TaskSequence {
        get => this.GetValue(TaskSequenceProperty);
        set => this.SetValue(TaskSequenceProperty, value);
    }

    private readonly Dictionary<Type, Stack<BaseOperationControl>> itemContentCacheMap;

    public OperationListBox() : base(32) {
        this.itemContentCacheMap = new Dictionary<Type, Stack<BaseOperationControl>>();
    }

    static OperationListBox() {
        TaskSequenceProperty.Changed.AddClassHandler<OperationListBox, TaskSequence?>((s, e) => s.OnTaskSequenceChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnTaskSequenceChanged(TaskSequence? oldSeq, TaskSequence? newSeq) {
        this.SetItemsSource(newSeq?.Operations);
    }

    protected override ModelBasedListBoxItem<BaseSequenceOperation> CreateItem() => new OperationListBoxItem();

    public BaseOperationControl GetContentObject(BaseSequenceOperation resource) {
        BaseOperationControl content;
        if (this.itemContentCacheMap.TryGetValue(resource.GetType(), out Stack<BaseOperationControl>? stack) && stack.Count > 0) {
            content = stack.Pop();
        }
        else {
            content = BaseOperationControl.Registry.NewInstance(resource.GetType());
        }

        return content;
    }

    public bool ReleaseContentObject(BaseSequenceOperation resource, BaseOperationControl content) {
        const int MaxItemContentCacheSize = 64;
        
        Type resourceType = resource.GetType();
        if (!this.itemContentCacheMap.TryGetValue(resourceType, out Stack<BaseOperationControl>? stack)) {
            this.itemContentCacheMap[resourceType] = stack = new Stack<BaseOperationControl>();
        }
        else if (stack.Count == MaxItemContentCacheSize) {
            return false;
        }

        stack.Push(content);
        return true;
    }
}