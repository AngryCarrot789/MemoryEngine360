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
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Scripting;

public class ScriptingManagerViewState {
    public static readonly DataKey<ScriptingManagerViewState> DataKey = DataKeys.Create<ScriptingManagerViewState>(nameof(ScriptingManager));
    
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
    
    public TopLevelIdentifier TopLevelIdentifier { get; }

    public event EventHandler<ValueChangedEventArgs<Script?>>? SelectedScriptChanged;

    private ScriptingManagerViewState(ScriptingManager ScriptingManager, TopLevelIdentifier topLevelIdentifier) {
        this.ScriptingManager = ScriptingManager;
        this.TopLevelIdentifier = topLevelIdentifier;
        this.ScriptingManager.ScriptRemoved += this.ScriptingManagerOnScriptRemoved;
        if (this.ScriptingManager.Scripts.Count > 0) {
            this.SelectedScript = this.ScriptingManager.Scripts[0];
        }
    }

    private void ScriptingManagerOnScriptRemoved(object? sender, ItemIndexEventArgs<Script> e) {
        ReadOnlyCollection<Script> list = this.ScriptingManager.Scripts;
        if (list.Count < 1) {
            this.SelectedScript = null;
        }
        else if (list[e.Index] == this.SelectedScript) {
            this.SelectedScript = e.Index > 0 ? list[e.Index - 1] : list[e.Index];
        }
    }

    public static ScriptingManagerViewState GetInstance(ScriptingManager manager, TopLevelIdentifier topLevelIdentifier) {
        return TopLevelDataMap.GetInstance(manager).GetOrCreate<ScriptingManagerViewState>(topLevelIdentifier, manager, (t, s) => new ScriptingManagerViewState((ScriptingManager) t!, s));
    }
}