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

using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Scripting.Commands;

public class RenameScriptCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ScriptingManager.DataKey.TryGetContext(e.ContextData, out ScriptingManager? manager)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ScriptingManager.DataKey.TryGetContext(e.ContextData, out ScriptingManager? manager)) {
            return;
        }

        ScriptingManagerViewState state = ScriptingManagerViewState.GetInstance(manager);
        Script? script = state.SelectedScript;
        if (script != null) {
            SingleUserInputInfo info = new SingleUserInputInfo("Rename script", "What do you want to call it?", script.Name);
            if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
                script.Name = info.Text;
            }
        }
    }
}