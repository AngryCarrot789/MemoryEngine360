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

using System.Diagnostics;
using MemEngine360.Engine;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.Engine.View;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Commands.ATM;

/// <summary>
/// Base class for a command that uses the selected saved address entry or entries
/// </summary>
public abstract class BaseSavedAddressSelectionCommand : Command, IDisabledHintProvider {
    /// <summary>
    /// The minimum number of selected items required. Default is 1.
    /// </summary>
    public int MinimumSelection { get; protected init; } = 1;
    
    /// <summary>
    /// The maximum number of selected items allowed. Default is <see cref="int.MaxValue"/>.
    /// Set to 1 to force as single-selection only
    /// </summary>
    public int MaximumSelection { get; protected init; } = int.MaxValue;

    protected sealed override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngineViewState.DataKey.TryGetContext(e.ContextData, out MemoryEngineViewState? engineVs)) {
            return Executability.Invalid;
        }

        int count = GetSelectionCount(engineVs, e);
        if (count < this.MinimumSelection || count > this.MaximumSelection) {
            return Executability.ValidButCannotExecute;
        }

        List<BaseAddressTableEntry> selection = GetSelection(engineVs, e);
        Debug.Assert(selection.Count == count);
        
        return this.CanExecuteOverride(selection, engineVs.Engine, e);
    }
    
    protected sealed override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngineViewState.DataKey.TryGetContext(e.ContextData, out MemoryEngineViewState? engineVs)) {
            return Task.CompletedTask;
        }
        
        List<BaseAddressTableEntry> selection = GetSelection(engineVs, e);
        if (selection.Count < this.MinimumSelection || selection.Count > this.MaximumSelection) {
            return Task.CompletedTask;
        }

        return this.ExecuteCommandAsync(selection, engineVs, e);
    }

    private static List<BaseAddressTableEntry> GetSelection(MemoryEngineViewState engineVs, CommandEventArgs e) {
        List<BaseAddressTableEntry> selection = engineVs.AddressTableSelectionManager.SelectedItems.ToList();
        
        // When selection is empty, we check if the command is running through a context menu,
        // since right-clicking an item might not select it, so we use only that item.
        if (selection.Count < 1 && e.SourceContextMenu != null) {
            if (BaseAddressTableEntry.DataKey.TryGetContext(e.ContextData, out BaseAddressTableEntry? entry)) {
                selection.Add(entry);
            }
        }
        
        return selection;
    }
    
    private static int GetSelectionCount(MemoryEngineViewState engineVs, CommandEventArgs e) {
        int count = engineVs.AddressTableSelectionManager.Count;
        if (count < 1 && e.SourceContextMenu != null) {
            if (BaseAddressTableEntry.DataKey.TryGetContext(e.ContextData, out _)) {
                count++;
            }
        }
        
        return count;
    }
    
    protected virtual Executability CanExecuteOverride(List<BaseAddressTableEntry> entries, MemoryEngine engine, CommandEventArgs e) {
        return Executability.Valid;
    }

    /// <summary>
    /// Executes this command with the selected saved addresses (may be entries or groups)
    /// </summary>
    /// <param name="entries">
    ///     A list of the selected items. This list does not affect and is not
    ///     affected by the <see cref="MemoryEngine.AddressTableSelectionManager"/>
    /// </param>
    /// <param name="engineVs"></param>
    /// <param name="e">The command args</param>
    protected abstract Task ExecuteCommandAsync(List<BaseAddressTableEntry> entries, MemoryEngineViewState engineVs, CommandEventArgs e);

    public virtual DisabledHintInfo? ProvideDisabledHint(IContextData context, ContextRegistry? sourceContextMenu) {
        if (MemoryEngineViewState.DataKey.TryGetContext(context, out MemoryEngineViewState? engineVs)) {
            return this.ProvideDisabledHintOverride(engineVs.Engine, context, sourceContextMenu);
        }

        return null;
    }

    /// <summary>
    /// Provide a disabled hint with the memory engine reference too
    /// </summary>
    /// <param name="engine">The engine</param>
    /// <param name="context">The available context</param>
    /// <param name="sourceContextMenu">The context menu this method was invoked from</param>
    /// <returns>The disabled hint info</returns>
    protected virtual DisabledHintInfo? ProvideDisabledHintOverride(MemoryEngine engine, IContextData context, ContextRegistry? sourceContextMenu) {
        return null;
    }
}