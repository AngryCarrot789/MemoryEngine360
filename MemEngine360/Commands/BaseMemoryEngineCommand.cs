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

using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public abstract class BaseMemoryEngineCommand : Command {
    protected sealed override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine))
            return Executability.Invalid;
        
        return this.CanExecuteCore(engine, e);
    }
    
    protected sealed override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine))
            return Task.CompletedTask;

        return this.ExecuteCommandAsync(engine, e);
    }
    
    protected abstract Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e);
    protected abstract Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e);
}