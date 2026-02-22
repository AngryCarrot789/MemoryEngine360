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
using MemEngine360.Engine.View;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Windowing;

namespace MemEngine360.Commands.ATM;

public abstract class BaseCopyAddressTableEntryCommand : BaseSavedAddressSelectionCommand {
    protected BaseCopyAddressTableEntryCommand() {
        this.MaximumSelection = 1;
    }

    protected override Executability CanExecuteOverride(List<BaseAddressTableEntry> entries, MemoryEngine engine, CommandEventArgs e) {
        Executability exec = base.CanExecuteOverride(entries, engine, e);
        if (exec != Executability.Valid)
            return exec;
        
        if (!ITopLevel.TopLevelDataKey.TryGetContext(e.ContextData, out ITopLevel? topLevel))
            return Executability.Invalid;
        if (!IClipboardService.TryGet(topLevel, out _))
            return Executability.ValidButCannotExecute;
        
        return this.CanExecute(entries[0], engine, e);
    }

    protected override async Task ExecuteCommandAsync(List<BaseAddressTableEntry> entries, MemoryEngineViewState engineVs, CommandEventArgs e) {
        if (!ITopLevel.TopLevelDataKey.TryGetContext(e.ContextData, out ITopLevel? topLevel))
            return;
        if (!IClipboardService.TryGet(topLevel, out IClipboardService? clipboard))
            return;

        await this.Copy(entries[0], engineVs.Engine, clipboard);
    }

    protected virtual Executability CanExecute(BaseAddressTableEntry entry, MemoryEngine engine, CommandEventArgs e) {
        return Executability.Valid;
    }

    protected abstract Task Copy(BaseAddressTableEntry entry, MemoryEngine engine, IClipboardService clipboard);
}