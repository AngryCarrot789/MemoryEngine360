// 
// Copyright (c) 2025-2025 REghZy
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
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Scripting.Commands;

public class CopyScriptFilePathCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!Script.DataKey.TryGetContext(e.ContextData, out Script? mt))
            return Executability.Invalid;
        if (!ITopLevel.TryGetFromContext(e.ContextData, out ITopLevel? topLevel))
            return Executability.Invalid;

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!Script.DataKey.TryGetContext(e.ContextData, out Script? mt))
            return;
        if (!ITopLevel.TryGetFromContext(e.ContextData, out ITopLevel? topLevel))
            return;

        if (topLevel.TryGetClipboard(out IClipboardService? clipboard)) {
            try {
                await clipboard.SetTextAsync(mt.FilePath);
            }
            catch (Exception ex) {
                await IMessageDialogService.Instance.ShowMessage("Clipboard unavailable", "Clipboard is in use. Path = " + mt.FilePath);
            }
        }
    }
}