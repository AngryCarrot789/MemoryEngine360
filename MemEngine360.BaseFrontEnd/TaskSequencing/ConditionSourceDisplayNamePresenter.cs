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

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class ConditionSourceDisplayNamePresenter {
    private readonly TaskSequencerWindow window;
    private TaskSequence? sourceSequence;
    private BaseSequenceOperation? sourceOperation;

    public ConditionSourceDisplayNamePresenter(TaskSequencerWindow window) {
        this.window = window;
    }

    public void SetTaskSequenceSource(TaskSequence? sequence) {
        this.ClearOperationSource();
        this.ClearTaskSequenceSource();

        this.sourceSequence = sequence;
        if (sequence != null) {
            sequence.DisplayNameChanged += this.OnSequenceDisplayNameChanged;
            this.UpdateTextForSequence(sequence);
        }
        else if (this.sourceOperation != null) {
            this.UpdateTextForOperation(this.sourceOperation);
        }
        else {
            this.UpdateTextForNothing(false);
        }
    }

    public void SetOperationSource(BaseSequenceOperation? operation) {
        this.ClearOperationSource();
        this.ClearTaskSequenceSource();

        this.sourceOperation = operation;
        if (operation != null) {
            this.UpdateTextForOperation(this.sourceOperation);
        }
        else if (this.sourceSequence != null) {
            this.UpdateTextForSequence(this.sourceSequence);
        }
        else {
            this.UpdateTextForNothing(true);
        }
    }

    private void ClearTaskSequenceSource() {
        if (this.sourceSequence != null) {
            this.sourceSequence.DisplayNameChanged -= this.OnSequenceDisplayNameChanged;
            this.sourceSequence = null;
        }
    }

    private void ClearOperationSource() {
        this.sourceOperation = null;
    }

    private void OnSequenceDisplayNameChanged(TaskSequence sender) => this.UpdateTextForSequence(sender);

    private void UpdateTextForSequence(TaskSequence? sequence) {
        int selectCount = this.window.SequenceSelectionManager.Count;
        this.window.PART_ConditionSourceName.Text = sequence?.DisplayName ?? (selectCount < 1 ? "(No sequence selected)" : "(Too many sequences selected)");
    }

    private void UpdateTextForOperation(BaseSequenceOperation? sequence) {
        int selectCount = this.window.SequenceSelectionManager.Count;
        this.window.PART_ConditionSourceName.Text = sequence?.DisplayName ?? (selectCount < 1 ? "(No sequence selected)" : "(Too many sequences selected)");
    }

    private void UpdateTextForNothing(bool isCausedByOperationChange) {
        if (isCausedByOperationChange && this.window.OperationSelectionManager.Count > 1) {
            this.window.PART_ConditionSourceName.Text = "(Too many operations selected)";
        }
        else if (!isCausedByOperationChange && this.window.SequenceSelectionManager.Count > 1) {
            this.window.PART_ConditionSourceName.Text = "(Too many sequences selected)";
        }
        else {
            this.window.PART_ConditionSourceName.Text = "(No sequences or operations selected)";
        }
    }
}