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

using AvaloniaHex.Base.Document;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Engine.HexEditing;

public interface IHexEditorUI {
    public static readonly DataKey<IHexEditorUI> DataKey = DataKeys.Create<IHexEditorUI>("IHexDisplayView");

    /// <summary>
    /// Gets the hex display info
    /// </summary>
    MemoryViewer? HexDisplayInfo { get; }

    /// <summary>
    /// Gets or sets the caret's location
    /// </summary>
    BitLocation CaretLocation { get; set; }
    
    /// <summary>
    /// Gets or sets the selection range
    /// </summary>
    BitRange SelectionRange { get; set; }

    /// <summary>
    /// Reads only your selection from the console
    /// </summary>
    Task ReloadSelectionFromConsole();

    /// <summary>
    /// Uploads the selection to the console
    /// </summary>
    Task UploadSelectionToConsoleCommand();

    void ScrollToCaret();
}