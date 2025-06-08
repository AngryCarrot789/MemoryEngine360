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

using System.Diagnostics;
using Avalonia.Input;
using MemEngine360.Sequencing;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.EditorContent.Containers;

public delegate void OperationEditorContentContainerListBoxCurrentOperationChangedEventHandler(OperationEditorContentContainerListBox sender);

public class OperationEditorContentContainerListBox : ModelBasedListBox<OperationEditorContentModel> {
    private bool isListLoaded;
    private ModelTypeMultiControlList<BaseOperationEditorContent>? currentSelectionList;

    public BaseSequenceOperation? CurrentOperation { get; private set; }

    public event OperationEditorContentContainerListBoxCurrentOperationChangedEventHandler? CurrentOperationChanged;

    public OperationEditorContentContainerListBox() : base(32) {
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);
        if (!e.Handled && this.IsPointerOver) {
            this.UnselectAll();
        }
    }

    protected override ModelBasedListBoxItem<OperationEditorContentModel> CreateItem() {
        return new OperationEditorContentContainerListBoxItem();
    }

    public void SetOperation(BaseSequenceOperation? newOperation) {
        BaseSequenceOperation? oldOperation = this.CurrentOperation;
        if (ReferenceEquals(oldOperation, newOperation)) {
            return;
        }

        if (oldOperation != null) {
            this.ClearModels();
            if (this.currentSelectionList != null) {
                BaseOperationEditorContent.Registry.AddItemsToCache(this.currentSelectionList);
                this.currentSelectionList = null;
            }

            this.CurrentOperation = null;
        }

        if (newOperation != null) {
            this.CurrentOperation = newOperation;

            Debug.Assert(this.currentSelectionList == null);
            this.currentSelectionList = BaseOperationEditorContent.Registry.GetControlInstances(newOperation);
            this.AddModels(this.currentSelectionList.Controls.Select(x => new OperationEditorContentModel(x, newOperation)));
        }

        this.CurrentOperationChanged?.Invoke(this);
    }
}