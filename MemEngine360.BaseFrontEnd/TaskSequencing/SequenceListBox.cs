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

using Avalonia;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.View;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Interactivity.Selections;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class SequenceListBox : ModelBasedListBox<TaskSequence> {
    public static readonly StyledProperty<TaskSequenceManager?> TaskSequencerManagerProperty = AvaloniaProperty.Register<SequenceListBox, TaskSequenceManager?>(nameof(TaskSequencerManager));

    public TaskSequenceManager? TaskSequencerManager {
        get => this.GetValue(TaskSequencerManagerProperty);
        set => this.SetValue(TaskSequencerManagerProperty, value);
    }

    protected override bool CanDragItemPositionCore => this.TaskSequencerManager != null;

    // For some reason, to make dragging items work properly, we need to
    // used cached items. Not entirely sure why, maybe some internal avalonia
    // states aren't set in new objects but are once they become loaded
    public SequenceListBox() : base(8) {
    }

    static SequenceListBox() {
        TaskSequencerManagerProperty.Changed.AddClassHandler<SequenceListBox, TaskSequenceManager?>((s, e) => s.OnTaskSequencerManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    protected override void MoveItemIndexOverride(int oldIndex, int newIndex) {
        TaskSequenceManager? tsm = this.TaskSequencerManager;
        if (tsm != null && !tsm.Sequences[oldIndex].IsRunning) {
            TaskSequenceManagerViewState.GetInstance(tsm).SelectedSequences.MoveItemHelper(oldIndex, newIndex);
        }
    }

    private void OnTaskSequencerManagerChanged(TaskSequenceManager? oldManager, TaskSequenceManager? newManager) {
        this.SetItemsSource(newManager?.Sequences);
    }

    protected override ModelBasedListBoxItem<TaskSequence> CreateItem() => new SequenceListBoxItem();
}