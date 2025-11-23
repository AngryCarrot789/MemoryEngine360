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

using PFXToolKitUI.Utils.Events;

namespace MemEngine360.ModTools.Gui;

/// <summary>
/// Contains the GUI structure of a mod tool
/// </summary>
public sealed class ModToolGUI {
    public BaseMTPanel? RootPanel {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, static (t, o, n) => {
            t.RootPanelChanged?.Invoke(t, new ValueChangedEventArgs<BaseMTPanel?>(o, n));
            o?.GUI = null;
            n?.GUI = t;
        });
    }

    public ModTool ModTool { get; }
    
    public event EventHandler<ValueChangedEventArgs<BaseMTPanel?>>? RootPanelChanged;

    public ModToolGUI(ModTool modTool) {
        this.ModTool = modTool;
    }
}