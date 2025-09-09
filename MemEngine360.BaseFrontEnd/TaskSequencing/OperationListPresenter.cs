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

using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia.Controls;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.View;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity.SelectingEx;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class OperationListPresenter {
    private readonly TaskSequencerWindow window;
    private readonly IBinder<TaskSequence> selectedSequenceDisplayNameBinder = new EventUpdateBinder<TaskSequence>(nameof(TaskSequence.DisplayNameChanged), (b) => ((TextBlock) b.Control).Text = b.Model.DisplayName);
    private ListBoxSelectionHandler<BaseSequenceOperation>? operationSelectionHandler;

    private TaskSequence? myPrimarySequence;

    public OperationListPresenter(TaskSequencerWindow window) {
        this.window = window;
        this.window.Opened += this.OnWindowOpened;
        this.window.Closed += this.OnWindowClosed;

        this.selectedSequenceDisplayNameBinder.UpdateControlWithoutModel += b => this.UpdateOperationListGroupBoxHeader();
        this.selectedSequenceDisplayNameBinder.AttachControl(this.window.PART_OperationGroupBoxHeaderTextBlock);
    }

    private void UpdateOperationListGroupBoxHeader() {
        int count = this.window.State.SelectedSequences.Count;
        this.window.PART_OperationGroupBoxHeaderTextBlock.Text = count == 0 ? "(No sequence selected)" : "(Too many sequences selected)";
    }

    private void OnWindowOpened(object? sender, EventArgs e) {
        this.window.State.SelectedSequences.CollectionChanged += this.Event_SelectedSequencesCollectionChanged;
        this.SetPrimarySelectedSequence(this.window.State.PrimarySelectedSequence);
    }

    private void OnWindowClosed(object? sender, EventArgs e) {
        this.window.State.SelectedSequences.CollectionChanged -= this.Event_SelectedSequencesCollectionChanged;
        this.SetPrimarySelectedSequence(null);
    }
    
    private void Event_SelectedSequencesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        this.SetPrimarySelectedSequence(this.window.State.PrimarySelectedSequence);
    }

    private void SetPrimarySelectedSequence(TaskSequence? newPrimarySequence) {
        // We track a local copy of the primary selected sequence instead of using the view state's own event,
        // so that we only have to hook into collection changed, because we need to update header text blocks.
        // E.g. select all then delete all, primary sequence won't change on deletion since it is already null on select all
        if (this.myPrimarySequence == newPrimarySequence) {
            if (newPrimarySequence == null) {
                this.UpdateOperationEditorPanelHeader();
                this.UpdateOperationListGroupBoxHeader();
            }

            return;
        }
        
        this.OnPrimarySelectedSequenceChanged(this.myPrimarySequence, newPrimarySequence);
        this.myPrimarySequence = newPrimarySequence;
    }
    
    private void OnPrimarySelectedSequenceChanged(TaskSequence? oldSeq, TaskSequence? newSeq) {
        this.selectedSequenceDisplayNameBinder.SwitchModel(newSeq);

        if (oldSeq != null) {
            Debug.Assert(this.operationSelectionHandler != null);
            this.operationSelectionHandler!.Dispose();
            this.operationSelectionHandler = null;

            TaskSequenceViewState oldState = TaskSequenceViewState.GetInstance(oldSeq);
            oldState.PrimarySelectedOperationChanged -= this.Event_PrimaryOperationChanged;
            if (oldState.PrimarySelectedOperation != null) {
                this.OnPrimaryOperationChanged(oldState.PrimarySelectedOperation, null);
            }
        }

        Debug.Assert(this.operationSelectionHandler == null);
        this.window.PART_OperationListBox.TaskSequence = newSeq;
        if (newSeq != null) {
            TaskSequenceViewState newSeqState = TaskSequenceViewState.GetInstance(newSeq);
            newSeqState.PrimarySelectedOperationChanged += this.Event_PrimaryOperationChanged;

            this.operationSelectionHandler = new ListBoxSelectionHandler<BaseSequenceOperation>(
                newSeq.Operations,
                newSeqState.SelectedOperations, 
                this.window.PART_OperationListBox, 
                item => ((ModelBasedListBoxItem<BaseSequenceOperation>) item).Model!);
            this.OnPrimaryOperationChanged(null, newSeqState.PrimarySelectedOperation);
        }
        else {
            this.UpdateOperationEditorPanelHeader();
        }
    }

    private void Event_PrimaryOperationChanged(TaskSequenceViewState sender, BaseSequenceOperation? oldOp, BaseSequenceOperation? newOp) {
        this.OnPrimaryOperationChanged(oldOp, newOp);
    }

    private void OnPrimaryOperationChanged(BaseSequenceOperation? oldOperation, BaseSequenceOperation? newOperation) {
        this.UpdateOperationEditorPanelHeader();
        this.window.SetConditionSourceAsOperation(newOperation);
        this.window.PART_OperationEditorControlsListBox.SetOperation(newOperation);
    }

    private void UpdateOperationEditorPanelHeader() {
        TextBlock tbHeader = this.window.PART_OperationEditorPanelTextBlock;
        TaskSequence? primarySequence = this.window.State.PrimarySelectedSequence;
        TaskSequenceViewState? sequenceState = primarySequence != null ? TaskSequenceViewState.GetInstance(primarySequence) : null;
        BaseSequenceOperation? primaryOperation = sequenceState?.PrimarySelectedOperation;

        if (primaryOperation != null) {
            tbHeader.Opacity = 1.0;
            tbHeader.Text = $"Editing '{primaryOperation.DisplayName}'";
        }
        else {
            tbHeader.Opacity = 0.7;
            if (primarySequence == null) {
                tbHeader.Text = this.window.State.SelectedSequences.Count > 0 ? "(Too many sequences selected)" : "(No sequence selected)";
            }
            else {
                tbHeader.Text = this.window.State.SelectedOperations!.Count == 0 ? "(No operation selected)" : "(Too many operations selected)";
            }
        }
    }
}