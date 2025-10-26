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

using PFXToolKitUI.Utils;

namespace MemEngine360.ModTools.Gui;

public delegate void ModToolGUIRootPanelChangedEventHandler(ModToolGUI sender, BaseMTPanel? oldRootPanel, BaseMTPanel? newRootPanel);

/// <summary>
/// Contains the GUI structure of a mod tool
/// </summary>
public sealed class ModToolGUI {
    private BaseMTPanel? rootPanel;

    public BaseMTPanel? RootPanel {
        get => this.rootPanel;
        set => PropertyHelper.SetAndRaiseINE(ref this.rootPanel, value, this, static (t, o, n) => {
            t.RootPanelChanged?.Invoke(t, o, n);
            if (o != null)
                o.GUI = null;
            if (n != null)
                n.GUI = t;
        });
    }

    public ModTool ModTool { get; }
    
    public event ModToolGUIRootPanelChangedEventHandler? RootPanelChanged;

    public ModToolGUI(ModTool modTool) {
        this.ModTool = modTool;
    }
}