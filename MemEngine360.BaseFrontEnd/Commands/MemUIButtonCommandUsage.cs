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

public class MemUIButtonCommandUsage : SimpleButtonCommandUsage {
    public IMemEngineUI? UI { get; private set; }

    protected MemUIButtonCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnContextChanged() {
        base.OnContextChanged();
        IMemEngineUI? oldEngine = this.UI;
        IMemEngineUI? newEngine = null;
        bool hasEngine = this.GetContextData() is IContextData data && IMemEngineUI.MemUIDataKey.TryGetContext(data, out newEngine);
        if (hasEngine && oldEngine == newEngine) {
            return;
        }

        this.UI = newEngine;
        this.OnEngineChanged(oldEngine, newEngine);
    }

    protected virtual void OnEngineChanged(IMemEngineUI? oldUI, IMemEngineUI? newUI) {
    }
}