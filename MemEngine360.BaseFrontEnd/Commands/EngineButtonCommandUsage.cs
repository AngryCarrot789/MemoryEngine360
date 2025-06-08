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
using PFXToolKitUI.Avalonia.CommandUsages;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.BaseFrontEnd.Commands;

/// <summary>
/// Base class for a button command usage that needs to hook into mem engine events
/// </summary>
public abstract class EngineButtonCommandUsage : SimpleButtonCommandUsage {
    public MemoryEngine? Engine { get; private set; }

    protected EngineButtonCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnContextChanged() {
        base.OnContextChanged();
        MemoryEngine? oldEngine = this.Engine;
        MemoryEngine? newEngine = null;
        if (this.GetContextData() is IContextData data) {
            MemoryEngine.EngineDataKey.TryGetContext(data, out newEngine);
        }

        if (oldEngine != newEngine) {
            this.Engine = newEngine;
            this.OnEngineChanged(oldEngine, newEngine);
        }
    }

    protected virtual void OnEngineChanged(MemoryEngine? oldEngine, MemoryEngine? newEngine) {
    }
}