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

using System.Collections.ObjectModel;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.ModTools;

public class ModToolManagerViewState : IDisposable {
    /// <summary>
    /// Gets the task sequence manager for this state
    /// </summary>
    public ModToolManager ModToolManager { get; }

    /// <summary>
    /// Gets or sets the script being viewed in the editor
    /// </summary>
    public ModTool? SelectedModTool {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.SelectedModToolChanged);
    }

    public event EventHandler<ValueChangedEventArgs<ModTool?>>? SelectedModToolChanged;

    private ModToolManagerViewState(ModToolManager modToolManager) {
        this.ModToolManager = modToolManager;
        this.ModToolManager.ToolRemoved += this.ModToolManagerOnToolRemoved;
        if (this.ModToolManager.ModTools.Count > 0) {
            this.SelectedModTool = this.ModToolManager.ModTools[0];
        }
    }

    private void ModToolManagerOnToolRemoved(object? sender, ItemIndexEventArgs<ModTool> e) {
        ReadOnlyCollection<ModTool> list = this.ModToolManager.ModTools;
        if (list.Count < 1) {
            this.SelectedModTool = null;
        }
        else if (list[e.Index] == this.SelectedModTool) {
            this.SelectedModTool = e.Index > 0 ? list[e.Index - 1] : list[e.Index];
        }
    }

    public static ModToolManagerViewState GetInstance(ModToolManager manager) {
        return ((IComponentManager) manager).GetOrCreateComponent((t) => new ModToolManagerViewState((ModToolManager) t));
    }

    public void Dispose() {
        this.ModToolManager.ToolRemoved -= this.ModToolManagerOnToolRemoved;
    }
}