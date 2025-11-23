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
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Scripting;

public class ScriptingManagerViewState {
    /// <summary>
    /// Gets the task sequence manager for this state
    /// </summary>
    public ScriptingManager ScriptingManager { get; }

    /// <summary>
    /// Gets or sets the script being viewed in the editor
    /// </summary>
    public Script? SelectedScript {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.SelectedScriptChanged);
    }

    public event EventHandler<ValueChangedEventArgs<Script?>>? SelectedScriptChanged;

    private ScriptingManagerViewState(ScriptingManager ScriptingManager) {
        this.ScriptingManager = ScriptingManager;
        this.ScriptingManager.Scripts.ValidateRemove += this.SourceListValidateRemove;
        this.ScriptingManager.Scripts.ValidateReplace += this.SourceListValidateReplaced;
        if (this.ScriptingManager.Scripts.Count > 0) {
            this.SelectedScript = this.ScriptingManager.Scripts[0];
        }
    }

    private void SourceListValidateRemove(IObservableList<Script> observableList, int index, int count) {
        if (observableList.Count - count == 0) {
            this.SelectedScript = null;
            return;
        }

        for (int i = 0; i < count; i++) {
            if (observableList[index + i] == this.SelectedScript) {
                this.SelectedScript = index > 0
                    ? observableList[index - 1]
                    : observableList[index + count];
                return;
            }
        }
    }

    private void SourceListValidateReplaced(IObservableList<Script> observableList, int index, Script oldItem, Script newItem) {
        if (this.SelectedScript == oldItem) {
            this.SelectedScript = index > 0
                ? observableList[index - 1]
                : observableList.Count != 1
                    ? observableList[index + 1]
                    : null;
        }
    }

    public static ScriptingManagerViewState GetInstance(ScriptingManager manager) {
        return ((IComponentManager) manager).GetOrCreateComponent((t) => new ScriptingManagerViewState((ScriptingManager) t));
    }
}