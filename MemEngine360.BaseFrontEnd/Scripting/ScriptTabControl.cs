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

using Avalonia;
using Avalonia.Controls;
using MemEngine360.Scripting;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.BaseFrontEnd.Scripting;

public class ScriptTabControl : TabControl {
    public static readonly StyledProperty<ScriptingManager?> ScriptingManagerProperty = AvaloniaProperty.Register<ScriptTabControl, ScriptingManager?>(nameof(ScriptingManager));

    public ScriptingManager? ScriptingManager {
        get => this.GetValue(ScriptingManagerProperty);
        set => this.SetValue(ScriptingManagerProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TabControl);

    private ObservableItemProcessorIndexing<Script>? processor;
    
    public ScriptTabControl() {
    }

    static ScriptTabControl() {
        ScriptingManagerProperty.Changed.AddClassHandler<ScriptTabControl, ScriptingManager?>((s, e) => s.OnScriptingManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnScriptingManagerChanged(ScriptingManager? oldValue, ScriptingManager? newValue) {
        if (oldValue != null) {
            oldValue.ScriptAdded -= this.OnScriptAdded;
            oldValue.ScriptRemoved -= this.OnScriptRemoved;
            oldValue.ScriptMoved -= this.OnScriptMoved;
            ItemEventUtils.InvokeItems(oldValue.Scripts, null, this.OnScriptRemoved, isAdding: false);
        }

        if (newValue != null) {
            newValue.ScriptAdded += this.OnScriptAdded;
            newValue.ScriptRemoved += this.OnScriptRemoved;
            newValue.ScriptMoved += this.OnScriptMoved;
            ItemEventUtils.InvokeItems(newValue.Scripts, null, this.OnScriptAdded, isAdding: true);
        }
    }

    private void OnScriptAdded(object? sender, ItemIndexEventArgs<Script> e) {
        this.Items.Insert(e.Index, new ScriptTabItem() {Script = e.Item});
    }
    
    private void OnScriptRemoved(object? sender, ItemIndexEventArgs<Script> e) {
        ((ScriptTabItem) this.Items[e.Index]!).Script = null;
        this.Items.RemoveAt(e.Index);
    }
    
    private void OnScriptMoved(object? sender, ItemMovedEventArgs<Script> e) {
        object? theTabItem = this.Items[e.OldIndex];
        this.Items.RemoveAt(e.OldIndex);
        this.Items.Insert(e.NewIndex, theTabItem);
    }
}