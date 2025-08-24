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

using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Sequencing.Commands;

public class EditConditionOutputModeCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        return ITaskSequencerUI.DataKey.GetExecutabilityForPresence(e.ContextData);
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ITaskSequencerUI.DataKey.TryGetContext(e.ContextData, out ITaskSequencerUI? ui)) {
            return;
        }

        List<IConditionItemUI> selection = ui.ConditionSelectionManager.SelectedItemList.ToList();
        if (selection.Count < 1) {
            return;
        }

        IEditConditionOutputModeService service = ApplicationPFX.Instance.ServiceManager.GetService<IEditConditionOutputModeService>();
        ConditionOutputMode? result = await service.EditTriggerMode(selection[0].Condition.OutputMode);
        if (result.HasValue) {
            foreach (IConditionItemUI condition in selection) {
                condition.Condition.OutputMode = result.Value;
            }
        }
    }
}