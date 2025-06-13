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
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Conditions;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Conditions;

public abstract class BaseConditionListContent : UserControl {
    public static readonly ModelTypeControlRegistry<BaseConditionListContent> Registry = new ModelTypeControlRegistry<BaseConditionListContent>();
    public static readonly StyledProperty<BaseSequenceCondition?> ConditionProperty = AvaloniaProperty.Register<BaseConditionListContent, BaseSequenceCondition?>(nameof(Condition));

    public BaseSequenceCondition? Condition {
        get => this.GetValue(ConditionProperty);
        set => this.SetValue(ConditionProperty, value);
    }

    protected BaseConditionListContent() {
    }
    
    static BaseConditionListContent() {
        ConditionProperty.Changed.AddClassHandler<BaseConditionListContent, BaseSequenceCondition?>((o, e) => o.OnConditionChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
        Registry.RegisterType(typeof(CompareMemoryCondition), () => new CompareMemoryConditionListContent());
    }

    protected virtual void OnConditionChanged(BaseSequenceCondition? oldCondition, BaseSequenceCondition? newCondition) {
        if (oldCondition != null)
            oldCondition.IsEnabledChanged -= this.OnIsEnabledChanged;
        if (newCondition != null)
            newCondition.IsEnabledChanged += this.OnIsEnabledChanged;
        this.UpdateOpacity();
    }
    
    private void OnIsEnabledChanged(BaseSequenceCondition condition) {
        this.UpdateOpacity();
    }

    private void UpdateOpacity() {
        BaseSequenceCondition? operation = this.Condition;
        this.Opacity = operation == null || operation.IsEnabled ? 1.0 : 0.5;
    }
}