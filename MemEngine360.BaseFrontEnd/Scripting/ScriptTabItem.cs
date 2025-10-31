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
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using MemEngine360.Scripting;
using PFXToolKitUI.Avalonia.AdvancedMenuService;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.BaseFrontEnd.Scripting;

public class ScriptTabItem : TabItem {
    public static readonly StyledProperty<Script?> ScriptProperty = AvaloniaProperty.Register<ScriptTabItem, Script?>(nameof(Script));

    public Script? Script {
        get => this.GetValue(ScriptProperty);
        set => this.SetValue(ScriptProperty, value);
    }

    private readonly IBinder<Script> nameBinder = new MultiEventUpdateBinder<Script>([nameof(Script.FilePathChanged), nameof(Script.HasUnsavedChangesChanged)], b => ((ScriptTabItem) b.Control).Header = (b.Model.Name ?? "(unnammed script)") + (b.Model.HasUnsavedChanges ? "*" : ""));
    private readonly IBinder<Script> toolTipBinder = new EventUpdateBinder<Script>(nameof(Script.FilePathChanged), b => ToolTip.SetTip((ScriptTabItem) b.Control, b.Model.FilePath ?? AvaloniaProperty.UnsetValue));

    private Button? PART_CloseTabButton;

    public ScriptTabItem() {
        this.nameBinder.AttachControl(this);
        this.toolTipBinder.AttachControl(this);
    }

    static ScriptTabItem() {
        ScriptProperty.Changed.AddClassHandler<ScriptTabItem, Script?>((s, e) => s.OnScriptChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed) {
            e.Handled = true;
            ((DataManagerCommandWrapper?) this.PART_CloseTabButton?.Command)?.Execute(null);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_CloseTabButton = e.NameScope.GetTemplateChild<Button>("PART_CloseTabButton");
        this.PART_CloseTabButton.Command = new DataManagerCommandWrapper(this, "commands.scripting.CloseScriptCommand", true);
    }
    
    private void OnScriptChanged(Script? oldValue, Script? newValue) {
        if (oldValue != null)
            AdvancedContextMenu.SetContextRegistry(this, null);
        
        this.nameBinder.SwitchModel(newValue);
        this.toolTipBinder.SwitchModel(newValue);
        DataManager.GetContextData(this).Set(Script.DataKey, newValue);
        
        if (newValue != null)
            AdvancedContextMenu.SetContextRegistry(this, ScriptTabContextRegistry.Registry);
    }
}