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
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;

namespace MemEngine360.BaseFrontEnd.Scripting;

public partial class ScriptingManagerView : UserControl {
    public static readonly StyledProperty<ScriptingManager?> ScriptingManagerProperty = AvaloniaProperty.Register<ScriptingManagerView, ScriptingManager?>(nameof(ScriptingManager));

    public ScriptingManager? ScriptingManager {
        get => this.GetValue(ScriptingManagerProperty);
        set => this.SetValue(ScriptingManagerProperty, value);
    }

    private bool isUpdatingTabSelection;
    private bool isUpdatingCodeEditorText, isUpdatingScriptCode;

    public ScriptingManagerView() {
        this.InitializeComponent();
        this.PART_TabControl.SelectionChanged += this.PART_TabControlOnSelectionChanged;
        this.PART_CodeEditor.TextChanged += this.PART_CodeEditorOnTextChanged;
    }

    private void PART_CodeEditorOnTextChanged(object? sender, TextChangedEventArgs e) {
        if (this.isUpdatingCodeEditorText) {
            return;
        }

        if (this.ScriptingManager != null) {
            ScriptingManagerViewState vs = ScriptingManagerViewState.GetInstance(this.ScriptingManager);
            if (vs.SelectedScript != null) {
                ScriptViewState state = ScriptViewState.GetInstance(vs.SelectedScript);
                this.isUpdatingScriptCode = true;
                state.ScriptText = ((TextBox) sender!).Text ?? "";
                this.isUpdatingScriptCode = false;
            }
        }
    }

    private void PART_TabControlOnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        if (this.isUpdatingTabSelection) {
            return;
        }

        if (this.ScriptingManager != null) {
            ScriptingManagerViewState vs = ScriptingManagerViewState.GetInstance(this.ScriptingManager);

            int index = this.PART_TabControl.SelectedIndex;
            vs.SelectedScript = index == -1 ? null : vs.ScriptingManager.Scripts[index];
        }
    }

    static ScriptingManagerView() {
        ScriptingManagerProperty.Changed.AddClassHandler<ScriptingManagerView, ScriptingManager?>((s, e) => s.OnScriptingManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnScriptingManagerChanged(ScriptingManager? oldValue, ScriptingManager? newValue) {
        if (oldValue != null) {
            ScriptingManagerViewState vs = ScriptingManagerViewState.GetInstance(oldValue);
            if (vs.SelectedScript != null) {
                this.OnSelectedScriptChanged(vs, vs.SelectedScript, null);
            }

            vs.SelectedScriptChanged -= this.OnSelectedScriptChanged;
            this.PART_TabControl.ScriptingManager = null;
        }

        if (newValue != null) {
            ScriptingManagerViewState vs = ScriptingManagerViewState.GetInstance(newValue);
            this.PART_TabControl.ScriptingManager = newValue;
            vs.SelectedScriptChanged += this.OnSelectedScriptChanged;
            if (vs.SelectedScript != null) {
                this.OnSelectedScriptChanged(vs, null, vs.SelectedScript);
            }
        }
    }

    private void SetSelection(Script? script) {
        this.isUpdatingTabSelection = true;
        this.PART_TabControl.SelectedIndex = script == null ? -1 : (script.Manager?.Scripts.IndexOf(script) ?? -1);
        this.isUpdatingTabSelection = false;
    }

    private void SetSourceCode(string? sourceCode) {
        if (!this.isUpdatingScriptCode) {
            this.isUpdatingCodeEditorText = true;
            this.PART_CodeEditor.Text = sourceCode ?? "";
            this.isUpdatingCodeEditorText = false;
        }
    }

    private void OnSelectedScriptChanged(ScriptingManagerViewState sender, Script? oldSel, Script? newSel) {
        if (oldSel != null)
            ScriptViewState.GetInstance(oldSel).ScriptTextChanged -= this.OnScriptSourceCodeChanged;
        if (newSel != null)
            ScriptViewState.GetInstance(newSel).ScriptTextChanged += this.OnScriptSourceCodeChanged;

        this.SetSelection(newSel);
        this.SetSourceCode(newSel?.SourceCode);
        this.PART_ConsoleTextList.ItemsSource = newSel?.ConsoleLines; 
        DataManager.GetContextData(this.PART_ScriptPanel).Set(Script.DataKey, newSel);
    }

    private void OnScriptSourceCodeChanged(ScriptViewState sender) {
        this.SetSourceCode(sender.ScriptText);
    }

    public void OnWindowOpened(IDesktopWindow sender) {
    }

    public void OnWindowClosed() {
        this.ScriptingManager = null;
    }
}