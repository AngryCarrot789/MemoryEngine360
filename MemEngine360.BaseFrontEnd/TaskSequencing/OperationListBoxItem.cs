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
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class OperationListBoxItem : ModelBasedListBoxItem<BaseSequenceOperation> {
    public static readonly StyledProperty<bool> IsRunningProperty = AvaloniaProperty.Register<OperationListBoxItem, bool>(nameof(IsRunning));
    
    public bool IsRunning {
        get => this.GetValue(IsRunningProperty);
        set => this.SetValue(IsRunningProperty, value);
    }
    
    private readonly IBinder<BaseSequenceOperation> isRunningBinder = new EventPropertyBinder<BaseSequenceOperation>(nameof(BaseSequenceOperation.IsRunningChanged), (b) => ((OperationListBoxItem) b.Control).IsRunning = b.Model.IsRunning, null);
    
    public OperationListBoxItem() {
    }

    protected override void OnAddingToList() {
        this.Content = ((OperationListBox) this.ListBox!).GetContentObject(this.Model!);
    }

    protected override void OnAddedToList() {
        BaseOperationControl content = (BaseOperationControl) this.Content!;
        TemplateUtils.Apply(content);
        content.Operation = this.Model;
        
        this.isRunningBinder.Attach(this, this.Model!);
    }

    protected override void OnRemovingFromList() {
        this.isRunningBinder.Detach();
        
        BaseOperationControl content = (BaseOperationControl) this.Content!;
        BaseSequenceOperation operation = content.Operation!;
        content.Operation = null;
        this.Content = null;
        ((OperationListBox) this.ListBox!).ReleaseContentObject(operation, content);
    }

    protected override void OnRemovedFromList() {
        
    }
}