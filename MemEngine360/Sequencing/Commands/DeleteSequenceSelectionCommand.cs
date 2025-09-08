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
using MemEngine360.Sequencing.View;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Sequencing.Commands;

public class DeleteSequenceSelectionCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        return TaskSequenceManager.DataKey.IsPresent(e.ContextData) ? Executability.Valid : Executability.Invalid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!TaskSequenceManager.DataKey.TryGetContext(e.ContextData, out TaskSequenceManager? manager)) {
            return;
        }

        TaskSequenceManagerViewState state = TaskSequenceManagerViewState.GetInstance(manager);
        if (state.SelectedSequences.Count < 1) {
            return;
        }

        if (await TryStopSequences(
                state.SelectedSequences.Where(x => x.IsRunning),
                "Sequence(s) still running",
                state.SelectedSequences.Count == 1
                    ? "This sequence is running. Do you want to stop it and then delete?"
                    : "Some of these sequences are still running. Do you want to stop them and then delete?")) {
            List<TaskSequence> selection = state.SelectedSequences.ToList();
            state.SelectedSequences.Clear();

            ObservableList<TaskSequence> sequenceList = state.Manager.Sequences;
            List<(int Index, TaskSequence Sequence)> remove = CollectionUtils.CreateIndexMap(sequenceList, selection);
            for (int i = remove.Count - 1; i >= 0; i--) {
                sequenceList.RemoveAt(remove[i].Item1);
            }
        }
    }

    public static async Task<bool> TryStopActiveSequences(TaskSequenceManager manager) {
        if (manager.ActiveSequences.Count > 0) {
            bool result = await TryStopSequences(manager.ActiveSequences.ToList(), "Sequences still running", "One or more sequences are running and cannot be deleted. Do you want to stop them and then delete?");
            Debug.Assert(result == manager.ActiveSequences.Count < 1);
            return result;
        }

        return true;
    }

    public static async Task<bool> TryStopSequences(IEnumerable<TaskSequence> sequencesToStop, string caption, string message) {
        IList<TaskSequence> theList = sequencesToStop as IList<TaskSequence> ?? sequencesToStop.ToList();
        if (theList.Count < 1) {
            return true;
        }

        MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(caption, message, MessageBoxButton.OKCancel, MessageBoxResult.OK);
        if (result != MessageBoxResult.OK) {
            return false;
        }

        foreach (TaskSequence seq in theList) {
            seq.RequestCancellation();
        }

        await Task.WhenAll(theList.Select(x => x.WaitForCompletion()));
        return theList.All(x => !x.IsRunning);
    }
}