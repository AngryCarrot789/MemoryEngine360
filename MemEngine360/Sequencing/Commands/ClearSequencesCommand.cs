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

using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Sequencing.Commands;

public class ClearSequencesCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        return ITaskSequencerUI.TaskSequencerUIDataKey.GetExecutabilityForPresence(e.ContextData);
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ITaskSequencerUI.TaskSequencerUIDataKey.TryGetContext(e.ContextData, out ITaskSequencerUI? ui)) {
            return;
        }

        if (ui.Manager.ActiveSequences.Count > 0) {
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Sequences still running", "One or more sequences are running and cannot be deleted. Do you want to stop them and then delete?", MessageBoxButton.OKCancel, MessageBoxResult.OK);
            if (result != MessageBoxResult.OK) {
                return;
            }

            foreach (TaskSequence seq in ui.Manager.ActiveSequences) {
                seq.RequestCancellation();
            }
            
            await Task.WhenAll(ui.Manager.ActiveSequences.ToList().Select(x => x.WaitForCompletion()));
        }
        
        ui.Manager.ClearSequences();
    }
}