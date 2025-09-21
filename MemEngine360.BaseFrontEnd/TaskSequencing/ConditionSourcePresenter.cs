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
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Conditions;
using MemEngine360.Sequencing.View;
using PFXToolKitUI.Avalonia.Interactivity.SelectingEx2;
using PFXToolKitUI.Interactivity.Selections;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class ConditionSourcePresenter {
    private readonly TaskSequencerView window;
    private TaskSequence? sourceSequence;
    private BaseSequenceOperation? sourceOperation;
    private SelectionModelBinder<BaseSequenceCondition>? conditionSelectionHandler;

    public ConditionSourcePresenter(TaskSequencerView window) {
        this.window = window;
        this.window.WindowClosed += this.WindowOnClosed;
        this.window.State.ConditionHostChanged += this.OnConditionHostChanged;
    }

    private void OnConditionHostChanged(TaskSequenceManagerViewState sender, IConditionsHost? oldConditionHost, IConditionsHost? newConditionHost) {
        if (newConditionHost is TaskSequence sequence) {
            this.SetTaskSequenceSource(sequence);
        }
        else if (newConditionHost is BaseSequenceOperation operation) {
            this.SetOperationSource(operation);
        }
        else {
            this.ClearAll();
        }
    }

    private void WindowOnClosed(object? sender, EventArgs e) {
        this.ClearAll();
    }

    private void SetTaskSequenceSource(TaskSequence? sequence) {
        this.ClearOperationSource();
        this.ClearTaskSequenceSource();
        this.window.PART_ConditionsListBox.ConditionsHost = sequence;
        this.window.State.ConditionHost = sequence;

        this.sourceSequence = sequence;
        if (sequence != null) {
            sequence.DisplayNameChanged += this.OnSequenceDisplayNameChanged;
            this.UpdateTextForSequence(sequence);
            this.conditionSelectionHandler = new SelectionModelBinder<BaseSequenceCondition>(this.window.PART_ConditionsListBox.Selection, TaskSequenceViewState.GetInstance(sequence).SelectedConditions);
        }
        else {
            Debug.Assert(this.sourceOperation == null);
            this.UpdateTextForNothing(false);
        }
    }

    private void SetOperationSource(BaseSequenceOperation? operation) {
        if (operation != null && operation.TaskSequence == null)
            throw new InvalidOperationException("Attempt to set source as operation that has no task sequence");

        this.ClearOperationSource();
        this.ClearTaskSequenceSource();
        this.window.PART_ConditionsListBox.ConditionsHost = operation;
        this.window.State.ConditionHost = operation;

        this.sourceOperation = operation;
        if (operation != null) {
            this.UpdateTextForOperation(this.sourceOperation);
            this.conditionSelectionHandler = new SelectionModelBinder<BaseSequenceCondition>(this.window.PART_ConditionsListBox.Selection, SequenceOperationViewState.GetInstance(operation).SelectedConditions);
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

        Debug.Assert(this.conditionSelectionHandler == null || this.sourceOperation != null);
    }

    private void ClearOperationSource() {
        if (this.sourceOperation != null) {
            Debug.Assert(this.conditionSelectionHandler != null);
            this.conditionSelectionHandler!.Dispose();
            this.conditionSelectionHandler = null;

            this.sourceOperation = null;
        }

        Debug.Assert(this.conditionSelectionHandler == null || this.sourceSequence != null);
    }

    private void ClearAll() {
        this.ClearOperationSource();
        this.ClearTaskSequenceSource();
        this.window.PART_ConditionsListBox.ConditionsHost = null;
        this.window.State.ConditionHost = null;
        this.sourceSequence = null;
        this.UpdateTextForNothing(false);
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
        ListSelectionModel<BaseSequenceOperation>? primarySeqOperationSelection = this.window.State.SelectedOperations;
        if (isCausedByOperationChange && primarySeqOperationSelection?.Count > 1) {
            this.window.PART_ConditionSourceName.Text = "(Too many operations selected)";
        }
        else if (!isCausedByOperationChange && this.window.State.SelectedSequences.Count > 1) {
            this.window.PART_ConditionSourceName.Text = "(Too many sequences selected)";
        }
        else {
            this.window.PART_ConditionSourceName.Text = "(No sequences or operations selected)";
        }
    }
}