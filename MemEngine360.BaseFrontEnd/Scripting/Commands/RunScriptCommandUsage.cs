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

using MemEngine360.Scripting;

namespace MemEngine360.BaseFrontEnd.Scripting.Commands;

public class RunScriptCommandUsage : BaseScriptIsRunningDependentCommandUsage {
    public RunScriptCommandUsage() : base("commands.scripting.RunScriptCommand") {
    }

    protected override void OnScriptChanged(Script? oldScript, Script? newScript) {
        base.OnScriptChanged(oldScript, newScript);
        if (oldScript != null) {
            oldScript.DedicatedConnectionChanged -= this.OnDedicatedConnectionChanged;
        }

        if (newScript != null) {
            newScript.DedicatedConnectionChanged += this.OnDedicatedConnectionChanged;
        }
    }

    private void OnDedicatedConnectionChanged(object? sender, EventArgs eventArgs) {
        this.UpdateCanExecuteLater();
    }
}