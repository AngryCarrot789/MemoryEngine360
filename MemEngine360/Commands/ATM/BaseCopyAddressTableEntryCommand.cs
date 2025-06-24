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

using MemEngine360.Engine;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity;

namespace MemEngine360.Commands.ATM;

public abstract class BaseCopyAddressTableEntryCommand : Command {
    protected sealed override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IDesktopWindow.DataKey.TryGetContext(e.ContextData, out IDesktopWindow? window) || window.ClipboardService == null)
            return Executability.Invalid;
        if (!IEngineUI.DataKey.TryGetContext(e.ContextData, out IEngineUI? ui))
            return Executability.Invalid;

        if (ui.AddressTableSelectionManager.Count != 1)
            return Executability.ValidButCannotExecute;
        
        IAddressTableEntryUI first = ui.AddressTableSelectionManager.SelectedItemList[0];
        return this.CanExecute(first, e);
    }

    protected sealed override async Task ExecuteCommandAsync(CommandEventArgs e) {
        IClipboardService? clipboard;
        if (!IDesktopWindow.DataKey.TryGetContext(e.ContextData, out IDesktopWindow? window) || (clipboard = window.ClipboardService) == null)
            return;
        if (!IEngineUI.DataKey.TryGetContext(e.ContextData, out IEngineUI? ui))
            return;
        if (ui.AddressTableSelectionManager.Count != 1)
            return;

        IAddressTableEntryUI first = ui.AddressTableSelectionManager.SelectedItemList[0];
        await this.Copy(first, clipboard);
    }

    protected virtual Executability CanExecute(IAddressTableEntryUI entry, CommandEventArgs e) {
        return Executability.Valid;
    }

    protected abstract Task Copy(IAddressTableEntryUI entry, IClipboardService clipboard);
}