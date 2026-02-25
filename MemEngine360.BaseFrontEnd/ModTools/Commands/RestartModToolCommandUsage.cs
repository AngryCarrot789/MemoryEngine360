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

using Avalonia.Controls;
using MemEngine360.ModTools;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.BaseFrontEnd.ModTools.Commands;

public class RestartModToolCommandUsage : BaseModToolCommandUsage {
    private ModTool? myModTool;

    public RestartModToolCommandUsage() : base("commands.modtools.RestartModToolCommand") {
    }

    protected override void OnModToolChanged(ModTool? oldTool, ModTool? newTool) {
        base.OnModToolChanged(oldTool, newTool);
        if (oldTool != null)
            oldTool.IsRunningChanged -= this.OnIsRunningChanged;
        if (newTool != null)
            newTool.IsRunningChanged += this.OnIsRunningChanged;

        this.myModTool = newTool;
    }

    private void OnIsRunningChanged(object? o, EventArgs e) {
        this.UpdateCanExecuteLater();
    }

    protected override void OnUpdateForCanExecuteState(Executability state) {
        base.OnUpdateForCanExecuteState(state);
        if (this.Button.Control is ContentControl cc) {
            cc.Content = this.myModTool == null || !this.myModTool.IsRunning ? "Run Tool" : "Restart";
        }
    }
}