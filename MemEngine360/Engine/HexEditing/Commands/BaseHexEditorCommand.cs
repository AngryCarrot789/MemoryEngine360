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

namespace MemEngine360.Engine.HexEditing.Commands;

public abstract class BaseHexEditorCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IHexEditorUI.DataKey.TryGetContext(e.ContextData, out var view)) {
            return Executability.Invalid;
        }

        return this.CanExecuteCore(view, e);
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IHexEditorUI.DataKey.TryGetContext(e.ContextData, out var view)) {
            return;
        }

        HexEditorInfo? info = view.HexDisplayInfo;
        if (info == null) {
            return;
        }

        await this.ExecuteCommandAsync(view, info, e);
    }

    protected virtual Executability CanExecuteCore(IHexEditorUI view, CommandEventArgs e) => Executability.Valid;
    
    protected abstract Task ExecuteCommandAsync(IHexEditorUI view, HexEditorInfo info, CommandEventArgs e);
}