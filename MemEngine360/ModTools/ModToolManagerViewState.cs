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

using PFXToolKitUI.Composition;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.ModTools;

public delegate void ModToolManagerViewStateSelectedScriptChangedEventHandler(ModToolManagerViewState sender, ModTool? oldSelectedScript, ModTool? newSelectedScript);

public class ModToolManagerViewState {
    private ModTool? selectedModTool;

    /// <summary>
    /// Gets the task sequence manager for this state
    /// </summary>
    public ModToolManager ModToolManager { get; }

    /// <summary>
    /// Gets or sets the script being viewed in the editor
    /// </summary>
    public ModTool? SelectedModTool {
        get => this.selectedModTool;
        set => PropertyHelper.SetAndRaiseINE(ref this.selectedModTool, value, this, static (t, o, n) => t.SelectedModToolChanged?.Invoke(t, o, n));
    }

    public event ModToolManagerViewStateSelectedScriptChangedEventHandler? SelectedModToolChanged;

    private ModToolManagerViewState(ModToolManager modToolManager) {
        this.ModToolManager = modToolManager;
        this.ModToolManager.ModTools.ValidateRemove += this.SourceListValidateRemove;
        this.ModToolManager.ModTools.ValidateReplace += this.SourceListValidateReplaced;
        if (this.ModToolManager.ModTools.Count > 0) {
            this.SelectedModTool = this.ModToolManager.ModTools[0];
        }
    }

    private void SourceListValidateRemove(IObservableList<ModTool> observableList, int index, int count) {
        if (observableList.Count - count == 0) {
            this.SelectedModTool = null;
            return;
        }

        for (int i = 0; i < count; i++) {
            if (observableList[index + i] == this.SelectedModTool) {
                this.SelectedModTool = index > 0
                    ? observableList[index - 1]
                    : observableList[index + count];
                return;
            }
        }
    }

    private void SourceListValidateReplaced(IObservableList<ModTool> observableList, int index, ModTool oldItem, ModTool newItem) {
        if (this.SelectedModTool == oldItem) {
            this.SelectedModTool = index > 0
                ? observableList[index - 1]
                : observableList.Count != 1
                    ? observableList[index + 1]
                    : null;
        }
    }

    public static ModToolManagerViewState GetInstance(ModToolManager manager) {
        return ((IComponentManager) manager).GetOrCreateComponent((t) => new ModToolManagerViewState((ModToolManager) t));
    }
}