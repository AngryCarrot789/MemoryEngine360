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
using Avalonia.Controls.Primitives;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Contexts;
using PFXToolKitUI.Avalonia.AdvancedMenuService;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Conditions;

public class ConditionsListBoxItem : ModelBasedListBoxItem<BaseSequenceCondition> {
    public BaseSequenceCondition Condition => this.Model ?? throw new Exception("Not connected to a model");
    
    public ConditionsListBoxItem() {
    }

    // replaced by ToggleConditionEnabledCommand
    // protected override void OnKeyDown(KeyEventArgs e) {
    //     base.OnKeyDown(e);
    //     if (!e.Handled && e.Key == Key.Space && this.IsFocused && this.Model != null) {
    //         this.Model.IsEnabled = !this.Model.IsEnabled;
    //     }
    // }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.SetDragSourceControl(e.NameScope.GetTemplateChild<Border>("PART_DragGrip"));
    }

    protected override void OnAddingToList() {
        this.Content = ((ConditionsListBox) this.ListBox!).GetContentObject(this.Model!);
    }

    protected override void OnAddedToList() {
        BaseConditionListContent content = (BaseConditionListContent) this.Content!;
        content.Condition = this.Model!;
        AdvancedContextMenu.SetContextRegistry(this, ConditionsContextRegistry.Registry);
        DataManager.GetContextData(this).Set(BaseSequenceCondition.DataKey, this.Condition);
    }

    protected override void OnRemovingFromList() {
        AdvancedContextMenu.SetContextRegistry(this, null);
        DataManager.GetContextData(this).Set(BaseSequenceCondition.DataKey, null);
        
        BaseConditionListContent content = (BaseConditionListContent) this.Content!;
        BaseSequenceCondition condition = content.Condition!;
        content.Condition = null;
        this.Content = null;
        ((ConditionsListBox) this.ListBox!).ReleaseContentObject(condition, content);
    }

    protected override void OnRemovedFromList() {
    }
}