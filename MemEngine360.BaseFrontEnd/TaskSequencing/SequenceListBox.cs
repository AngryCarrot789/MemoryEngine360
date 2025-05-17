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
using Avalonia.Controls;
using MemEngine360.Sequencing;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Bindings;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class SequenceListBox : ModelBasedListBox<TaskSequence> {
    public static readonly StyledProperty<TaskSequencerManager?> TaskSequencerManagerProperty = AvaloniaProperty.Register<SequenceListBox, TaskSequencerManager?>(nameof(TaskSequencerManager));

    public TaskSequencerManager? TaskSequencerManager {
        get => this.GetValue(TaskSequencerManagerProperty);
        set => this.SetValue(TaskSequencerManagerProperty, value);
    }

    private readonly IBinder<TaskSequencerManager> selectedSequenceBinder = new AvaloniaPropertyToEventPropertyBinder<TaskSequencerManager>(ListBox.SelectedItemProperty, nameof(TaskSequencerManager.SelectedSequenceChanged), (b) => ((SequenceListBox) b.Control).SelectedModel = b.Model.SelectedSequence, (b) => b.Model.SelectedSequence = ((SequenceListBox) b.Control).SelectedModel);

    public SequenceListBox() {
        this.selectedSequenceBinder.AttachControl(this);
    }

    static SequenceListBox() {
        TaskSequencerManagerProperty.Changed.AddClassHandler<SequenceListBox, TaskSequencerManager?>((s, e) => s.OnTaskSequencerManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnTaskSequencerManagerChanged(TaskSequencerManager? oldManager, TaskSequencerManager? newManager) {
        this.SetItemsSource(newManager?.Sequences);
        this.selectedSequenceBinder.SwitchModel(newManager);
    }

    protected override ModelBasedListBoxItem<TaskSequence> CreateItem() => new SequenceListBoxItem();
}