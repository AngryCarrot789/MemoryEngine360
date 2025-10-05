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

namespace MemEngine360.Scripting;

public delegate void ScriptViewStateEventHandler(ScriptViewState sender);

public class ScriptViewState {
    /// <summary>
    /// Gets the task sequence manager for this state
    /// </summary>
    public Script Script { get; }
    
    // Workaround for the fact AvaloniaEdit has no "core" project libraries, so we cannot access
    // TextDocument. Maybe we should just do without core projects deliberately not referencing avalonia...
    
    // The current impl uses a DataKey<TextDocument> to store the document itself in the script, lazily created,
    // and then simply writes the document text into the script source code
    
    /// <summary>
    /// Requests the UI to flush the code editor to the script's <see cref="Scripting.Script.SourceCode"/>
    /// </summary>
    public event ScriptViewStateEventHandler? FlushEditorToScript;

    private ScriptViewState(Script script) {
        this.Script = script;
    }

    public void RaiseFlushEditorToScript() => this.FlushEditorToScript?.Invoke(this);
    
    public static ScriptViewState GetInstance(Script manager) {
        return ((IComponentManager) manager).GetOrCreateComponent((t) => new ScriptViewState((Script) t));
    }
}