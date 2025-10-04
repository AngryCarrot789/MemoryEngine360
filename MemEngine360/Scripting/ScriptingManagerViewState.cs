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

namespace MemEngine360.Scripting;

public delegate void ScriptingManagerViewStateScriptEventHandler(ScriptingManagerViewState sender, Script script);
public delegate void ScriptingManagerViewStateSelectedScriptChangedEventHandler(ScriptingManagerViewState sender, Script? oldSelectedScript, Script? newSelectedScript);

public class ScriptingManagerViewState {
    private Script? selectedScript;
    
    /// <summary>
    /// Gets the task sequence manager for this state
    /// </summary>
    public ScriptingManager ScriptingManager { get; }

    /// <summary>
    /// Gets or sets the script being viewed in the editor
    /// </summary>
    public Script? SelectedScript {
        get => this.selectedScript;
        set => PropertyHelper.SetAndRaiseINE(ref this.selectedScript, value, this, static (t, o, n) => t.SelectedScriptChanged?.Invoke(t, o, n));
    }

    public event ScriptingManagerViewStateSelectedScriptChangedEventHandler? SelectedScriptChanged;
    
    private ScriptingManagerViewState(ScriptingManager ScriptingManager) {
        this.ScriptingManager = ScriptingManager;
        this.ScriptingManager.Scripts.BeforeItemsRemoved += this.SourceListBeforeItemsRemoved;
        this.ScriptingManager.Scripts.BeforeItemReplace += this.SourceListBeforeItemReplaced;
        if (this.ScriptingManager.Scripts.Count > 0) {
            this.SelectedScript = this.ScriptingManager.Scripts[0];
        }
    }

    private void SourceListBeforeItemsRemoved(IObservableList<Script> observableList, int index, int count) {
        this.SelectedScript = null;
    }

    private void SourceListBeforeItemReplaced(IObservableList<Script> observableList, int index, Script oldItem, Script newItem) {
        this.SelectedScript = null;
    }

    public static ScriptingManagerViewState GetInstance(ScriptingManager manager) {
        return ((IComponentManager) manager).GetOrCreateComponent((t) => new ScriptingManagerViewState((ScriptingManager) t));
    }
}