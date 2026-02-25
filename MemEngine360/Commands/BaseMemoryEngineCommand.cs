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

using System.Diagnostics.CodeAnalysis;
using MemEngine360.Engine;
using MemEngine360.Engine.View;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Commands;

/// <summary>
/// Base class for a command that runs in the context of the memory engine/scanner window
/// </summary>
public abstract class BaseMemoryEngineCommand : Command, IDisabledHintProvider {
    protected sealed override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngineViewState.DataKey.TryGetContext(e.ContextData, out MemoryEngineViewState? engineVs)) {
            return Executability.Invalid;
        }

        return this.CanExecuteCore(engineVs.Engine, engineVs, e);
    }

    protected sealed override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngineViewState.DataKey.TryGetContext(e.ContextData, out MemoryEngineViewState? engineVs))
            return Task.CompletedTask;

        return this.ExecuteCommandAsync(engineVs, engineVs.Engine, e);
    }

    /// <returns></returns>
    protected abstract Executability CanExecuteCore(MemoryEngine engine, MemoryEngineViewState engineVs, CommandEventArgs e);

    protected abstract Task ExecuteCommandAsync(MemoryEngineViewState engineVs, MemoryEngine engine, CommandEventArgs e);

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
    
    public static bool TryProvideNotConnectedDisabledHintInfo(MemoryEngine engine, [NotNullWhen(true)] out DisabledHintInfo? hintInfo) {
        if (engine.Connection == null)
            hintInfo = new SimpleDisabledHintInfo(StandardEngineMessages.Caption_NoConnection, StandardEngineMessages.Message_NoConnection);
        else if (engine.Connection.IsClosed)
            hintInfo = new SimpleDisabledHintInfo(StandardEngineMessages.Caption_ConnectionClosed, StandardEngineMessages.Message_ConnectionClosed);
        else
            hintInfo = null;
        return hintInfo != null;
    }
}