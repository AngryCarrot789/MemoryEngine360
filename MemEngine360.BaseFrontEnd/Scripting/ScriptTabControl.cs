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
            this.processor!.RemoveExistingItems();
            this.processor!.Dispose();
            this.processor = null;
        }
        
        if (newValue != null) {
            this.processor = ObservableItemProcessor.MakeIndexable(newValue.Scripts, this.OnScriptAdded, this.OnScriptRemoved, this.OnScriptMoved);
            this.processor.AddExistingItems();
        }
    }

    private void OnScriptAdded(object sender, int index, Script item) {
        this.Items.Insert(index, new ScriptTabItem() {Script = item});
    }
    
    private void OnScriptRemoved(object sender, int index, Script item) {
        ((ScriptTabItem) this.Items[index]!).Script = null;
        this.Items.RemoveAt(index);
    }
    
    private void OnScriptMoved(object sender, int oldindex, int newindex, Script item) {
        object? theTabItem = this.Items[oldindex];
        this.Items.RemoveAt(oldindex);
        this.Items.Insert(newindex, theTabItem);
    }
}