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

namespace MemEngine360.Scripting;

public delegate void ScriptViewStateEventHandler(ScriptViewState sender);

public class ScriptViewState {
    private string scriptText;

    /// <summary>
    /// Gets the task sequence manager for this state
    /// </summary>
    public Script Script { get; }

    public string ScriptText {
        get => this.scriptText;
        set {
            ArgumentNullException.ThrowIfNull(value);
            PropertyHelper.SetAndRaiseINE(ref this.scriptText, value, this, static t => {
                t.ScriptTextChanged?.Invoke(t);
                t.Script.SetSourceCode(t.ScriptText);
            });
        }
    }

    public event ScriptViewStateEventHandler? ScriptTextChanged;

    private ScriptViewState(Script script) {
        this.Script = script;
        this.scriptText = script.SourceCode ?? "";
    }

    public static ScriptViewState GetInstance(Script manager) {
        return ((IComponentManager) manager).GetOrCreateComponent((t) => new ScriptViewState((Script) t));
    }
}