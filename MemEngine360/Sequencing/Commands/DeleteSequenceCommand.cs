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

using System.Diagnostics;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Sequencing.Commands;

public class DeleteSequenceCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        return ITaskSequencerUI.TaskSequenceDataKey.GetExecutabilityForPresence(e.ContextData);
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ITaskSequencerUI.TaskSequenceDataKey.TryGetContext(e.ContextData, out TaskSequence? taskSequence)) {
            return;
        }

        if (taskSequence.Manager != null) {
            if (taskSequence.IsRunning) {
                MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Sequence is running", "The sequence is still running and cannot be deleted. Do you want to stop it to delete?", MessageBoxButton.OKCancel, MessageBoxResult.OK);
                if (result != MessageBoxResult.OK) {
                    return;
                }

                taskSequence.RequestCancellation();
                await taskSequence.WaitForCompletion();
                Debug.Assert(!taskSequence.IsRunning);
            }
            
            taskSequence.Manager.RemoveSequence(taskSequence);
        }
    }
}