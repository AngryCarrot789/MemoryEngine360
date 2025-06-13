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
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Operations.EditorContent;

public partial class DelayOperationEditorContent : BaseOperationEditorContent {
    private readonly IBinder<DelayOperation> delayBinder = new TextBoxToEventPropertyBinder<DelayOperation>(nameof(DelayOperation.DelayChanged), (b) => TimeSpanUtils.ConvertToString(b.Model.Delay), async (b, text) => {
        if (!TimeSpanUtils.TryParseTime(text, out TimeSpan value, out string? errorMessage)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid time", errorMessage, defaultButton: MessageBoxResult.OK);
            return false;
        }

        if (TimeSpanUtils.IsOutOfRangeForDelay(value, out errorMessage)) {
            await IMessageDialogService.Instance.ShowMessage("Delay out of range", errorMessage, defaultButton: MessageBoxResult.OK);
            return false;
        }
        
        b.Model.Delay = value;
        return true;
    });
    
    public override string Caption => "Delay";
    
    public DelayOperationEditorContent() {
        this.InitializeComponent();
        this.delayBinder.AttachControl(this.PART_DelayTextBox);
    }

    protected override void OnOperationChanged(BaseSequenceOperation? oldOperation, BaseSequenceOperation? newOperation) {
        base.OnOperationChanged(oldOperation, newOperation);
        this.delayBinder.SwitchModel((DelayOperation?) newOperation);
    }
}