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
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Sequencing.Commands;

public class DeleteSequenceSelectionCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        return ITaskSequenceManagerUI.DataKey.GetExecutabilityForPresence(e.ContextData);
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ITaskSequenceManagerUI.DataKey.TryGetContext(e.ContextData, out ITaskSequenceManagerUI? ui)) {
            return;
        }

        List<TaskSequence> items = ui.SequenceSelectionManager.SelectedItems.Select(x => x.TaskSequence).ToList();
        if (items.Count < 1) {
            return;
        }
        
        if (await TryStopSequences(items.Where(x => x.IsRunning), "Sequence(s) still running", items.Count == 1 ? "This sequence is running. Do you want to stop it and then delete?" : "Some of these sequences are still running. Do you want to stop them and then delete?")) {
            ui.SequenceSelectionManager.Clear();
            foreach (TaskSequence seq in items) {
                ui.Manager.Sequences.Remove(seq);
            }
        }
    }

    public static async Task<bool> TryStopActiveSequences(ITaskSequenceManagerUI ui) {
        if (ui.Manager.ActiveSequences.Count > 0) {
            bool result = await TryStopSequences(ui.Manager.ActiveSequences.ToList(), "Sequences still running", "One or more sequences are running and cannot be deleted. Do you want to stop them and then delete?");
            Debug.Assert(result == ui.Manager.ActiveSequences.Count < 1);
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