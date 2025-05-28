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

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.EditorContent.Containers;

public class OperationEditorContentContainerListBoxItem : ModelBasedListBoxItem<OperationEditorContentModel> {
    private TextBlock? PART_CaptionTextBlock;

    public OperationEditorContentContainerListBoxItem() {
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_CaptionTextBlock = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_CaptionTextBlock));
        this.PART_CaptionTextBlock.Text = this.Model?.Content.Caption;
    }

    protected override void OnAddingToList() {
        
    }

    protected override void OnAddedToList() {
        this.Content = this.Model!.Content;
        this.Model!.Content.Operation = this.Model.Operation;
        if (this.PART_CaptionTextBlock != null)
            this.PART_CaptionTextBlock.Text = this.Model!.Content.Caption;
    }

    protected override void OnRemovingFromList() {
        this.Model!.Content.Operation = null;
        this.Content = null;
    }

    protected override void OnRemovedFromList() {
        
    }
}