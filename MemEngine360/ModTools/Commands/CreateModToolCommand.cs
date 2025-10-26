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

namespace MemEngine360.ModTools.Commands;

public class CreateModToolCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ModToolManager.DataKey.TryGetContext(e.ContextData, out ModToolManager? manager)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }
    
    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!ModToolManager.DataKey.TryGetContext(e.ContextData, out ModToolManager? manager)) {
            return;
        }

        ReadOnlyObservableList<ModTool> tools = manager.ModTools;
        ModTool tool = new ModTool();
        tool.SetCustomNameWithoutPath(TextIncrement.GetIncrementableString(x => tools.All(y => y.Name != x), "New tool", out string? output, true) ? output : "New tool");
        
        ModToolManagerViewState state = ModToolManagerViewState.GetInstance(manager);
        manager.AddModTool(tool);
        state.SelectedModTool = tool;
    }
}