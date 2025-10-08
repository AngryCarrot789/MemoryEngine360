﻿// 
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
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Commands;

public abstract class BaseMemoryEngineCommand : Command, IDisabledHintProvider {
    protected sealed override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngine.EngineDataKey.TryGetContext(e.ContextData, out MemoryEngine? engine)) {
            return Executability.Invalid;
        }

        return this.CanExecuteCore(engine, e);
    }

    protected sealed override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine.EngineDataKey.TryGetContext(e.ContextData, out MemoryEngine? engine))
            return Task.CompletedTask;

        return this.ExecuteCommandAsync(engine, e);
    }

    /// <returns></returns>
    protected abstract Executability CanExecuteCore(MemoryEngine engine, CommandEventArgs e);

    protected abstract Task ExecuteCommandAsync(MemoryEngine engine, CommandEventArgs e);

    public virtual DisabledHintInfo? ProvideDisabledHint(IContextData context, ContextRegistry? sourceContextMenu) {
        if (MemoryEngine.EngineDataKey.TryGetContext(context, out MemoryEngine? engine)) {
            return this.ProvideDisabledHintOverride(engine, context, sourceContextMenu);
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