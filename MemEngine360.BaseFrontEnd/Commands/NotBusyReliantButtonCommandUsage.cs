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
using MemEngine360.Engine.View;

namespace MemEngine360.BaseFrontEnd.Commands;

public abstract class NotBusyReliantButtonCommandUsage : EngineButtonCommandUsage {
    protected NotBusyReliantButtonCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnEngineChanged(MemoryEngine? oldEngine, MemoryEngine? newEngine, MemoryEngineViewState? oldEngineVs, MemoryEngineViewState? newEngineVs) {
        base.OnEngineChanged(oldEngine, newEngine, oldEngineVs, newEngineVs);
        if (oldEngine != null)
            oldEngine.IsBusyChanged -= this.OnIsBusyChanged;
        if (newEngine != null)
            newEngine.IsBusyChanged += this.OnIsBusyChanged;
    }

    private void OnIsBusyChanged(object? o, EventArgs e) {
        this.UpdateCanExecuteLater();
    }
}