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

public abstract class BaseCopyAddressTableEntryCommand : BaseSavedAddressSelectionCommand {
    protected BaseCopyAddressTableEntryCommand() {
        this.MaximumSelection = 1;
    }

    protected override Executability CanExecuteOverride(List<IAddressTableEntryUI> entries, IEngineUI engine, CommandEventArgs e) {
        Executability exec = base.CanExecuteOverride(entries, engine, e);
        if (exec != Executability.Valid)
            return exec;
        
        if (!IDesktopWindow.DataKey.TryGetContext(e.ContextData, out IDesktopWindow? window))
            return Executability.Invalid;
        if (window.ClipboardService == null)
            return Executability.ValidButCannotExecute;
        
        return this.CanExecute(entries[0], engine, e);
    }

    protected override async Task ExecuteCommandAsync(List<IAddressTableEntryUI> entries, IEngineUI engine, CommandEventArgs e) {
        IClipboardService? clipboard;
        if (!IDesktopWindow.DataKey.TryGetContext(e.ContextData, out IDesktopWindow? window) || (clipboard = window.ClipboardService) == null)
            return;

        await this.Copy(entries[0], engine, clipboard);
    }

    protected virtual Executability CanExecute(IAddressTableEntryUI entry, IEngineUI engine, CommandEventArgs e) {
        return Executability.Valid;
    }

    protected abstract Task Copy(IAddressTableEntryUI entry, IEngineUI engine, IClipboardService clipboard);
}