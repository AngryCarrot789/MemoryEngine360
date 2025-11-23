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
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.ModTools.Gui;

public abstract class BaseMTElement {
    public ModToolGUI? GUI {
        get => field;
        internal set => PropertyHelper.SetAndRaiseINE(ref field, value, this, static (t, o, n) => t.OnGUIChanged(o, n));
    }

    public BaseMTPanel? Parent {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.ParentChanged);
    }

    public EnumHorizontalAlign HorizontalAlignment {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.HorizontalAlignmentChanged);
    }

    public EnumVerticalAlign VerticalAlignment {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.VerticalAlignmentChanged);
    }

    public double? Width {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.WidthChanged);
    }

    public double? Height {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.HeightChanged);
    }

    public double? MinWidth {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.MinWidthChanged);
    }

    public double? MinHeight {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.MinHeightChanged);
    }

    public double? MaxWidth {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.MaxWidthChanged);
    }

    public double? MaxHeight {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.MaxHeightChanged);
    }

    public event EventHandler<ValueChangedEventArgs<ModToolGUI?>>? GUIChanged;
    public event EventHandler<ValueChangedEventArgs<BaseMTPanel?>>? ParentChanged;
    public event EventHandler? HorizontalAlignmentChanged;
    public event EventHandler? VerticalAlignmentChanged;
    public event EventHandler? WidthChanged, HeightChanged;
    public event EventHandler? MinWidthChanged, MinHeightChanged;
    public event EventHandler? MaxWidthChanged, MaxHeightChanged;

    // The table that is associated with this object
    internal LuaTable? ownerTable;

    protected BaseMTElement() {
    }

    protected virtual void OnGUIChanged(ModToolGUI? oldGui, ModToolGUI? newGui) {
        this.GUIChanged?.Invoke(this, new ValueChangedEventArgs<ModToolGUI?>(oldGui, newGui));
    }
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