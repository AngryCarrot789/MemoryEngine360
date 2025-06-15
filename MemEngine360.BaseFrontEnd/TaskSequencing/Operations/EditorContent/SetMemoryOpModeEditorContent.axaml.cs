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

using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Operations;
using PFXToolKitUI.Avalonia.Bindings.Enums;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Operations.EditorContent;

public partial class SetMemoryOpModeEditorContent : BaseOperationEditorContent {
    private readonly EventPropertyEnumBinder<SetMemoryWriteMode> binder = new EventPropertyEnumBinder<SetMemoryWriteMode>(typeof(SetMemoryOperation), nameof(SetMemoryOperation.WriteModeChanged), o => ((SetMemoryOperation) o).WriteMode, (o, v) => ((SetMemoryOperation) o).WriteMode = v);

    public override string Caption => "Write Mode";

    public SetMemoryOpModeEditorContent() {
        this.InitializeComponent();
        this.binder.Assign(this.PART_Set, SetMemoryWriteMode.Set);
        this.binder.Assign(this.PART_Add, SetMemoryWriteMode.Add);
        this.binder.Assign(this.PART_Multiply, SetMemoryWriteMode.Multiply);
        this.binder.Assign(this.PART_Divide, SetMemoryWriteMode.Divide);
    }

    protected override void OnOperationChanged(BaseSequenceOperation? oldOperation, BaseSequenceOperation? newOperation) {
        base.OnOperationChanged(oldOperation, newOperation);
        if (oldOperation != null)
            this.binder.Detach();
        if (newOperation != null)
            this.binder.Attach(newOperation);
    }
}