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
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands.ATM;

/// <summary>
/// Base class for a command that uses the selected saved address entry or entries
/// </summary>
public abstract class BaseSavedAddressSelectionCommand : Command {
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
        if (!IEngineUI.DataKey.TryGetContext(e.ContextData, out IEngineUI? engine)) {
            return Executability.Invalid;
        }

        int count = GetSelectionCount(engine, e);
        if (count < this.MinimumSelection || count > this.MaximumSelection) {
            return Executability.ValidButCannotExecute;
        }

        List<IAddressTableEntryUI> selection = GetSelection(engine, e);
        Debug.Assert(selection.Count == count);
        
        return this.CanExecuteOverride(selection, engine, e);
    }
    
    protected sealed override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IEngineUI.DataKey.TryGetContext(e.ContextData, out IEngineUI? engine)) {
            return Task.CompletedTask;
        }
        
        List<IAddressTableEntryUI> selection = GetSelection(engine, e);
        if (selection.Count < this.MinimumSelection || selection.Count > this.MaximumSelection) {
            return Task.CompletedTask;
        }

        return this.ExecuteCommandAsync(selection, engine, e);
    }

    private static List<IAddressTableEntryUI> GetSelection(IEngineUI engine, CommandEventArgs e) {
        List<IAddressTableEntryUI> selection = engine.AddressTableSelectionManager.SelectedItemList.ToList();
        
        // When selection is empty, we check if the command is running through a context menu,
        // since right-clicking an item might not select it, so we use only that item.
        if (selection.Count < 1 && e.SourceContextMenu != null) {
            if (IAddressTableEntryUI.DataKey.TryGetContext(e.ContextData, out IAddressTableEntryUI? entry)) {
                selection.Add(entry);
            }
        }
        
        return selection;
    }
    
    private static int GetSelectionCount(IEngineUI engine, CommandEventArgs e) {
        int count = engine.AddressTableSelectionManager.Count;
        if (count < 1 && e.SourceContextMenu != null) {
            if (IAddressTableEntryUI.DataKey.TryGetContext(e.ContextData, out IAddressTableEntryUI? entry)) {
                count++;
            }
        }
        
        return count;
    }
    
    protected virtual Executability CanExecuteOverride(List<IAddressTableEntryUI> entries, IEngineUI engine, CommandEventArgs e) {
        return Executability.Valid;
    }
    
    /// <summary>
    /// Executes this command with the selected saved addresses (may be entries or groups)
    /// </summary>
    /// <param name="entries">
    /// A list of the selected items. This list does not affect and is not
    /// affected by the <see cref="IEngineUI.AddressTableSelectionManager"/>
    /// </param>
    /// <param name="engine">The engine available via the command context</param>
    /// <param name="e">The command args</param>
    protected abstract Task ExecuteCommandAsync(List<IAddressTableEntryUI> entries, IEngineUI engine, CommandEventArgs e);
}