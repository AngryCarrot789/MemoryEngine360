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
    }

    protected override Executability CanExecuteOverride(List<BaseAddressTableEntry> entries, MemoryEngineViewState engineVs, CommandEventArgs e) {
        Executability exec = base.CanExecuteOverride(entries, engineVs, e);
        if (exec != Executability.Valid)
            return exec;
        
        if (!ITopLevel.TopLevelDataKey.TryGetContext(e.ContextData, out ITopLevel? topLevel))
            return Executability.Invalid;
        
        if (!topLevel.TryGetClipboard(out _))
            return Executability.ValidButCannotExecute;
        
        return this.CanExecute(entries, engineVs, e);
    }

    protected override async Task ExecuteCommandAsync(List<BaseAddressTableEntry> entries, MemoryEngineViewState engineVs, CommandEventArgs e) {
        if (!ITopLevel.TopLevelDataKey.TryGetContext(e.ContextData, out ITopLevel? topLevel))
            return;
        
        if (!topLevel.TryGetClipboard(out IClipboardService? clipboard))
            return;

        await this.Copy(entries, engineVs, clipboard);
    }

    protected virtual Executability CanExecute(List<BaseAddressTableEntry> entries, MemoryEngineViewState engineVs, CommandEventArgs e) {
        return Executability.Valid;
    }

    protected abstract Task Copy(List<BaseAddressTableEntry> entries, MemoryEngineViewState engineVs, IClipboardService clipboard);
}