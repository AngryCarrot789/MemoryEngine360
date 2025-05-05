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
using PFXToolKitUI.Avalonia.CommandUsages;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.BaseFrontEnd.Commands;

/// <summary>
/// Base class for a button command usage that needs to hook into mem engine events
/// </summary>
public abstract class MemEngineButtonCommandUsage : SimpleButtonCommandUsage {
    public MemoryEngine360? Engine { get; private set; }

    protected MemEngineButtonCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnContextChanged() {
        base.OnContextChanged();
        MemoryEngine360? oldEngine = this.Engine;
        MemoryEngine360? newEngine = null;
        bool hasEngine = this.GetContextData() is IContextData data && MemoryEngine360.DataKey.TryGetContext(data, out newEngine);
        if (hasEngine && oldEngine == newEngine) {
            return;
        }

        this.Engine = newEngine;
        this.OnEngineChanged(oldEngine, newEngine);
    }

    protected virtual void OnEngineChanged(MemoryEngine360? oldEngine, MemoryEngine360? newEngine) {
    }
}