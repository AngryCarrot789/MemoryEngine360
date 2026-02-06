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
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Scripting.Commands;

public class AddNewScriptCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ScriptingManagerViewState.DataKey.TryGetContext(e.ContextData, out ScriptingManagerViewState? manager)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }
    
    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ScriptingManagerViewState.DataKey.TryGetContext(e.ContextData, out ScriptingManagerViewState? manager)) {
            return;
        }

        ObservableList<Script> scripts = manager.ScriptingManager.Scripts;
        Script script = new Script();
        script.SetCustomNameWithoutPath(TextIncrement.GetIncrementableString(x => scripts.All(y => y.Name != x), "New Script", out string? output, true) ? output : "New Script");
        scripts.Add(script);
        manager.SelectedScript = script;
    }
}