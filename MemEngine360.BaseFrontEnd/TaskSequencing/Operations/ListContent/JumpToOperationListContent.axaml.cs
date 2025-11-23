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
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Operations.ListContent;

public partial class JumpToOperationListContent : BaseOperationListContent {
    private readonly IBinder<JumpToLabelOperation> labelNameBinder =
        new TextBoxToEventPropertyBinder<JumpToLabelOperation>(
            nameof(JumpToLabelOperation.CurrentTargetChanged),
            getText: (b) => {
                if (b.Model.CurrentTarget == null) {
                    return "";
                }
                else {
                    string? name = b.Model.CurrentTarget.LabelName;
                    return string.IsNullOrWhiteSpace(name) ? "<unnammed label>" : name;
                }
            },
            parseAndUpdate: async (b, text) => {
                if (string.IsNullOrWhiteSpace(text)) {
                    b.Model.SetTarget(null, null);
                    return true;
                }

                ObservableList<BaseSequenceOperation> operations = b.Model.TaskSequence!.Operations;
                foreach (BaseSequenceOperation op in operations) {
                    if (op is LabelOperation label && label.LabelName != null) {
                        if (text.Equals(label.LabelName, StringComparison.OrdinalIgnoreCase)) {
                            b.Model.SetTarget(text, label);
                            return true;
                        }
                    }
                }

                await IMessageDialogService.Instance.ShowMessage("No such label", $"No label exists with the name '{text}'", defaultButton: MessageBoxResult.OK);
                return false;
            });

    public JumpToOperationListContent() {
        this.InitializeComponent();
        this.labelNameBinder.AttachControl(this.PART_LabelNameTextBox);
    }

    protected override void OnOperationChanged(BaseSequenceOperation? oldOperation, BaseSequenceOperation? newOperation) {
        base.OnOperationChanged(oldOperation, newOperation);
        this.labelNameBinder.SwitchModel((JumpToLabelOperation?) newOperation);

        if (oldOperation != null)
            ((JumpToLabelOperation) oldOperation).CurrentTargetChanged -= this.OnCurrentTargetLabelChanged;
        
        if (newOperation != null) {
            JumpToLabelOperation jump = (JumpToLabelOperation) newOperation;
            jump.CurrentTargetChanged += this.OnCurrentTargetLabelChanged;
            if (jump.CurrentTarget != null)
                this.OnCurrentTargetLabelChanged(jump, new ValueChangedEventArgs<LabelOperation?>(null, jump.CurrentTarget));
        }
    }

    private void OnCurrentTargetLabelChanged(object? o, ValueChangedEventArgs<LabelOperation?> e) {
        JumpToLabelOperation sender = (JumpToLabelOperation) o!;
        if (e.OldValue != null)
            e.OldValue.LabelNameChanged -= this.OnTargetLabelNameChanged;
        if (e.NewValue != null)
            e.NewValue.LabelNameChanged += this.OnTargetLabelNameChanged;
    }

    private void OnTargetLabelNameChanged(object? o, EventArgs e) {
        this.labelNameBinder.UpdateControl();
    }
}