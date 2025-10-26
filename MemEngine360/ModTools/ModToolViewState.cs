﻿// 
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

namespace MemEngine360.ModTools;

public delegate void ScriptViewStateEventHandler(ModToolViewState sender);

public class ModToolViewState {
    /// <summary>
    /// Gets the task sequence manager for this state
    /// </summary>
    public ModTool ModTool { get; }
    
    // Workaround for the fact AvaloniaEdit has no "core" project libraries, so we cannot access
    // TextDocument. Maybe we should just not use core projects at all since it makes things 1000% harder...
    
    // The current impl uses a DataKey<TextDocument> to store the document itself in the script, lazily created,
    // and then simply writes the document text into the script source code
    
    /// <summary>
    /// Requests the UI to flush the text editor document associated with this
    /// script view state to our script's <see cref="Scripting.Script.SourceCode"/>
    /// </summary>
    public event ScriptViewStateEventHandler? FlushEditorToModTool;

    private ModToolViewState(ModTool modTool) {
        this.ModTool = modTool;
    }

    /// <summary>
    /// Raises the <see cref="FlushEditorToModTool"/> event
    /// </summary>
    public void RaiseFlushEditorToScript() => this.FlushEditorToModTool?.Invoke(this);
    
    public static ModToolViewState GetInstance(ModTool modTool) {
        return ((IComponentManager) modTool).GetOrCreateComponent((t) => new ModToolViewState((ModTool) t));
    }
}