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
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Conditions;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Conditions;

public class ConditionsListBox : ModelBasedListBox<BaseSequenceCondition> {
    public static readonly StyledProperty<IConditionsHost?> ConditionsHostProperty = AvaloniaProperty.Register<ConditionsListBox, IConditionsHost?>(nameof(ConditionsHost));

    private readonly Dictionary<Type, Stack<BaseConditionListContent>> itemContentCacheMap;

    public IConditionsHost? ConditionsHost {
        get => this.GetValue(ConditionsHostProperty);
        set => this.SetValue(ConditionsHostProperty, value);
    }

    protected override bool CanDragItemPositionCore {
        get {
            IConditionsHost? host = this.ConditionsHost;
            return host != null && !host.TaskSequence!.IsRunning;
        }
    }

    public ConditionsListBox() : base(2) {
        this.itemContentCacheMap = new Dictionary<Type, Stack<BaseConditionListContent>>();
        this.CanDragItemPosition = true;
    }

    static ConditionsListBox() {
        ConditionsHostProperty.Changed.AddClassHandler<ConditionsListBox, IConditionsHost?>((s, e) => s.OnConditionsHostChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    protected override ModelBasedListBoxItem<BaseSequenceCondition> CreateItem() {
        return new ConditionsListBoxItem();
    }

    protected override void MoveItemIndexOverride(int oldIndex, int newIndex) {
        IConditionsHost? owner = this.ConditionsHost;
        switch (owner) {
            case TaskSequence task when !task.IsRunning: {
                task.Conditions.Move(oldIndex, newIndex);
                break;
            }
            case BaseSequenceOperation op when op.TaskSequence == null || !op.TaskSequence.IsRunning: {
                op.Conditions.Move(oldIndex, newIndex);
                break;
            }
        }
    }

    private void OnConditionsHostChanged(IConditionsHost? oldHost, IConditionsHost? newHost) {
        this.SetItemsSource(newHost?.Conditions);
    }

    public BaseConditionListContent GetContentObject(BaseSequenceCondition condition) {
        BaseConditionListContent content;
        if (this.itemContentCacheMap.TryGetValue(condition.GetType(), out Stack<BaseConditionListContent>? stack) && stack.Count > 0) {
            content = stack.Pop();
        }
        else {
            content = BaseConditionListContent.Registry.NewInstance(condition.GetType());
        }

        return content;
    }

    public bool ReleaseContentObject(BaseSequenceCondition condition, BaseConditionListContent content) {
        const int MaxItemContentCacheSize = 64;

        Type resourceType = condition.GetType();
        if (!this.itemContentCacheMap.TryGetValue(resourceType, out Stack<BaseConditionListContent>? stack)) {
            this.itemContentCacheMap[resourceType] = stack = new Stack<BaseConditionListContent>();
        }
        else if (stack.Count == MaxItemContentCacheSize) {
            return false;
        }

        stack.Push(content);
        return true;
    }
}