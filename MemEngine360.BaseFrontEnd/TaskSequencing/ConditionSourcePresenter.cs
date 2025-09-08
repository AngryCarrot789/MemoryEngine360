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
using Avalonia.Controls;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.View;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class ConditionSourcePresenter {
    private readonly TaskSequencerWindow window;
    private TaskSequence? sourceSequence;
    private BaseSequenceOperation? sourceOperation;
    private ObservableListBoxSelectionHandler<BaseSequenceCondition>? conditionSelectionHandler;

    public ConditionSourcePresenter(TaskSequencerWindow window) {
        this.window = window;
    }

    public void SetTaskSequenceSource(TaskSequence? sequence) {
        this.ClearOperationSource();
        this.ClearTaskSequenceSource();
        this.window.PART_ConditionsListBox.ConditionsHost = sequence;
        this.sourceSequence = sequence;
        if (sequence != null) {
            sequence.DisplayNameChanged += this.OnSequenceDisplayNameChanged;
            this.UpdateTextForSequence(sequence);

            this.conditionSelectionHandler = new ObservableListBoxSelectionHandler<BaseSequenceCondition>(
                TaskSequenceViewState.GetInstance(sequence).SelectedConditions,
                this.window.PART_ConditionsListBox,
                GetConditionFromControl,
                this.GetControlFromCondition
            );
        }
        else {
            Debug.Assert(this.sourceOperation == null);
            this.UpdateTextForNothing(false);
        }
    }

    public void SetOperationSource(BaseSequenceOperation? operation) {
        this.ClearOperationSource();
        this.ClearTaskSequenceSource();
        this.window.PART_ConditionsListBox.ConditionsHost = operation;

        this.sourceOperation = operation;
        if (operation != null) {
            this.UpdateTextForOperation(this.sourceOperation);
        }
        else {
            Debug.Assert(this.sourceSequence == null);
            this.UpdateTextForNothing(true);
        }
    }

    private void ClearTaskSequenceSource() {
        if (this.sourceSequence != null) {
            Debug.Assert(this.conditionSelectionHandler != null);
            
            this.sourceSequence.DisplayNameChanged -= this.OnSequenceDisplayNameChanged;
            this.conditionSelectionHandler!.Dispose();
            this.conditionSelectionHandler = null;
            
            this.sourceSequence = null;
        }
        
        Debug.Assert(this.conditionSelectionHandler == null);
    }

    private void ClearOperationSource() {
        this.sourceOperation = null;
    }

    private void OnSequenceDisplayNameChanged(TaskSequence sender) => this.UpdateTextForSequence(sender);

    private void UpdateTextForSequence(TaskSequence? sequence) {
        int selectCount = this.window.State.SelectedSequences.Count;
        this.window.PART_ConditionSourceName.Text = sequence?.DisplayName ?? (selectCount < 1 ? "(No sequence selected)" : "(Too many sequences selected)");
    }

    private void UpdateTextForOperation(BaseSequenceOperation? sequence) {
        int selectCount = this.window.State.SelectedSequences.Count;
        this.window.PART_ConditionSourceName.Text = sequence?.DisplayName ?? (selectCount < 1 ? "(No sequence selected)" : "(Too many sequences selected)");
    }

    private void UpdateTextForNothing(bool isCausedByOperationChange) {
        ObservableList<BaseSequenceOperation>? operations = this.window.State.SelectedOperations;

        if (isCausedByOperationChange && operations?.Count > 1) {
            this.window.PART_ConditionSourceName.Text = "(Too many operations selected)";
        }
        else if (!isCausedByOperationChange && this.window.State.SelectedSequences.Count > 1) {
            this.window.PART_ConditionSourceName.Text = "(Too many sequences selected)";
        }
        else {
            this.window.PART_ConditionSourceName.Text = "(No sequences or operations selected)";
        }
    }

    private static BaseSequenceCondition GetConditionFromControl(ListBoxItem item) => ((ModelBasedListBoxItem<BaseSequenceCondition>) item).Model!;
    private ListBoxItem GetControlFromCondition(BaseSequenceCondition seq) => this.window.PART_ConditionsListBox.ItemMap.GetControl(seq);
}