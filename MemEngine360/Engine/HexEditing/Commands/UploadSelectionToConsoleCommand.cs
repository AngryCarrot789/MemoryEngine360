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

namespace MemEngine360.Engine.HexEditing.Commands;

public class UploadSelectionToConsoleCommand : BaseHexEditorCommand {
    protected override Task ExecuteCommandAsync(IHexEditorUI view, MemoryViewer info, CommandEventArgs e) {
        return view.UploadSelectionToConsoleCommand();
    }
    
    protected override Task OnAlreadyExecuting(CommandEventArgs args) {
        // User can hold down CTRL+R, and there's a change it takes just long
        // enough to execute as to try to run it while already running.
        // So we don't want to show a dialog saying it's running, just ignore it
        if (args.Shortcut != null)
            return Task.CompletedTask;
        
        return base.OnAlreadyExecuting(args);
    }
}