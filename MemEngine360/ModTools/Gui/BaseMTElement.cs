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

using Lua;
using PFXToolKitUI.Utils;

namespace MemEngine360.ModTools.Gui;

public delegate void BaseMTElementEventHandler(BaseMTElement sender);

public delegate void BaseMTElementGUIChangedEventHandler(BaseMTElement sender, ModToolGUI? oldGUI, ModToolGUI? newGUI);

public delegate void BaseMTElementParentChangedEventHandler(BaseMTElement sender, BaseMTPanel? oldParent, BaseMTPanel? newParent);

public abstract class BaseMTElement {
    private ModToolGUI? gui;
    private BaseMTPanel? parent;
    private EnumHorizontalAlign horizontalAlignment;
    private EnumVerticalAlign verticalAlignment;

    public ModToolGUI? GUI {
        get => this.gui;
        internal set => PropertyHelper.SetAndRaiseINE(ref this.gui, value, this, static (t, o, n) => t.OnGUIChanged(o, n));
    }

    public BaseMTPanel? Parent {
        get => this.parent;
        set => PropertyHelper.SetAndRaiseINE(ref this.parent, value, this, static (t, o, n) => t.ParentChanged?.Invoke(t, o, n));
    }

    public EnumHorizontalAlign HorizontalAlignment {
        get => this.horizontalAlignment;
        set => PropertyHelper.SetAndRaiseINE(ref this.horizontalAlignment, value, this, static t => t.HorizontalAlignmentChanged?.Invoke(t));
    }
    
    public EnumVerticalAlign VerticalAlignment {
        get => this.verticalAlignment;
        set => PropertyHelper.SetAndRaiseINE(ref this.verticalAlignment, value, this, static t => t.VerticalAlignmentChanged?.Invoke(t));
    }
    
    public event BaseMTElementGUIChangedEventHandler? GUIChanged;
    public event BaseMTElementParentChangedEventHandler? ParentChanged;
    public event BaseMTElementEventHandler? HorizontalAlignmentChanged;
    public event BaseMTElementEventHandler? VerticalAlignmentChanged;
    
    // The table that is associated with this object
    internal LuaTable? ownerTable;

    protected BaseMTElement() {
    }

    protected virtual void OnGUIChanged(ModToolGUI? oldGui, ModToolGUI? newGui) {
        this.GUIChanged?.Invoke(this, oldGui, newGui);
    }

    public enum EnumHorizontalAlign {
        Stretch,
        Left,
        Center,
        Right,
    }
    
    public enum EnumVerticalAlign {
        Stretch,
        Top,
        Center,
        Bottom,
    }
}