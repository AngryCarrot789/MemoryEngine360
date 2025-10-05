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

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using MemEngine360.Scripting;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils.Debouncing;
using TextMateSharp.Grammars;

namespace MemEngine360.BaseFrontEnd.Scripting;

public partial class ScriptingManagerView : UserControl {
    public static readonly DataKey<TextDocument> ScriptTextDocumentDataKey = DataKeys.Create<TextDocument>(nameof(ScriptTextDocumentDataKey));
    public static readonly StyledProperty<ScriptingManager?> ScriptingManagerProperty = AvaloniaProperty.Register<ScriptingManagerView, ScriptingManager?>(nameof(ScriptingManager));

    public ScriptingManager? ScriptingManager {
        get => this.GetValue(ScriptingManagerProperty);
        set => this.SetValue(ScriptingManagerProperty, value);
    }

    private bool isUpdatingTabSelection;
    private bool isUpdatingCodeEditorText, isFlushingEditorTextToScript;
    private readonly TextMate.Installation myTextMate;
    private readonly TimerDispatcherDebouncer debounceFlushText;

    private readonly IBinder<Script> binderClearConsoleOnEachRun =
        new AvaloniaPropertyToEventPropertyBinder<Script>(
            ToggleButton.IsCheckedProperty,
            nameof(Script.ClearConsoleOnRunChanged),
            b => ((CheckBox) b.Control).IsChecked = b.Model.ClearConsoleOnRun,
            b => b.Model.ClearConsoleOnRun = ((CheckBox) b.Control).IsChecked == true);

    public IDesktopWindow? Window { get; private set; }

    public ScriptingManagerView() {
        this.InitializeComponent();
        this.PART_TabControl.SelectionChanged += this.PART_TabControlOnSelectionChanged;
        this.PART_CodeEditor.TextChanged += this.PART_CodeEditorOnTextChanged;
        this.PART_CodeEditor.LostFocus += this.OnTextEditorLostFocus;
        this.binderClearConsoleOnEachRun.AttachControl(this.PART_ClearConsoleOnEachRun);
        this.debounceFlushText = new TimerDispatcherDebouncer(TimeSpan.FromSeconds(2), static t => ((ScriptingManagerView) t!).FlushEditorTextToScript(), this);

        RegistryOptions options = new RegistryOptions(ThemeName.Dark);
        this.myTextMate = this.PART_CodeEditor.InstallTextMate(options);
        this.myTextMate.SetGrammar(options.GetScopeByLanguageId(options.GetLanguageByExtension(".lua").Id));
    }

    static ScriptingManagerView() {
        ScriptingManagerProperty.Changed.AddClassHandler<ScriptingManagerView, ScriptingManager?>((s, e) => s.OnScriptingManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);

        ApplicationPFX.Instance.Dispatcher.Post(static t => {
            // Force redraw view port. For some reason without this, the text editor
            // colours do not apply until you type something or switch tabs
            ScriptingManagerView self = (ScriptingManagerView) t!;
            self.myTextMate.EditorModel.InvalidateViewPortLines();
            self.PART_CodeEditor.TextArea.TextView.Redraw();
        }, this, DispatchPriority.Background);
    }

    private void PART_CodeEditorOnTextChanged(object? sender, EventArgs e) {
        if (!this.isUpdatingCodeEditorText) {
            this.debounceFlushText.InvokeOrPostpone();
        }
    }

    private void OnTextEditorLostFocus(object? sender, RoutedEventArgs e) {
        this.FlushEditorTextToScript();
    }

    private void FlushEditorTextToScript() {
        if (this.isUpdatingCodeEditorText) {
            throw new InvalidOperationException("Reentrancy -- currently updating editor due to script code changed");
        }

        if (this.ScriptingManager != null) {
            ScriptingManagerViewState vs = ScriptingManagerViewState.GetInstance(this.ScriptingManager);
            if (vs.SelectedScript != null) {
                this.isFlushingEditorTextToScript = true;
                TextDocument document = GetScriptTextDocument(vs.SelectedScript);
                Debug.Assert(document == this.PART_CodeEditor.Document);

                vs.SelectedScript.SourceCode = document.Text;
                this.isFlushingEditorTextToScript = false;
            }
        }
    }

    private static TextDocument GetScriptTextDocument(Script script) {
        TextDocument? doc = ScriptTextDocumentDataKey.GetContext(script.UserContext);
        if (doc == null) {
            script.UserContext.Set(ScriptTextDocumentDataKey, doc = new TextDocument(script.SourceCode));
        }

        return doc;
    }

    private void PART_TabControlOnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        if (this.ScriptingManager != null) {
            ScriptingManagerViewState vs = ScriptingManagerViewState.GetInstance(this.ScriptingManager);

            int index = this.PART_TabControl.SelectedIndex;
            vs.SelectedScript = index == -1 ? null : vs.ScriptingManager.Scripts[index];
        }
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

        DataManager.GetContextData(this).Set(ScriptingManager.DataKey, newValue);
    }

    private void OnSelectedScriptChanged(ScriptingManagerViewState sender, Script? oldSel, Script? newSel) {
        if (this.isFlushingEditorTextToScript)
            throw new InvalidOperationException("Currently flushing text editor to script source");

        this.debounceFlushText.Reset(); // cancel flush callback
        if (oldSel != null) {
            oldSel.SourceCodeChanged -= this.OnScriptSourceCodeChanged;
            ScriptViewState.GetInstance(oldSel).FlushEditorToScript -= this.OnRequestFlushEditorToScript;

            this.isFlushingEditorTextToScript = true;
            oldSel.SourceCode = GetScriptTextDocument(oldSel).Text;
            this.isFlushingEditorTextToScript = false;
        }

        if (newSel != null) {
            newSel.SourceCodeChanged += this.OnScriptSourceCodeChanged;
            ScriptViewState.GetInstance(newSel).FlushEditorToScript += this.OnRequestFlushEditorToScript;
        }

        this.binderClearConsoleOnEachRun.SwitchModel(newSel);
        this.isUpdatingTabSelection = true;
        this.PART_TabControl.SelectedIndex = newSel == null ? -1 : (newSel.Manager?.Scripts.IndexOf(newSel) ?? -1);
        this.isUpdatingTabSelection = false;

        this.isUpdatingCodeEditorText = true;
        this.PART_CodeEditor.Document = newSel != null ? GetScriptTextDocument(newSel) : new TextDocument();
        this.isUpdatingCodeEditorText = false;

        this.PART_ConsoleTextList.ItemsSource = newSel?.ConsoleLines;
        DataManager.GetContextData(this.PART_ScriptPanel).Set(Script.DataKey, newSel);
    }

    private void OnRequestFlushEditorToScript(ScriptViewState sender) {
        TextDocument document = GetScriptTextDocument(sender.Script);
        if (document != this.PART_CodeEditor.Document) {
            sender.Script.SourceCode = document.Text;
        }
        else {
            if (this.isUpdatingCodeEditorText)
                throw new InvalidOperationException("Already updating text editor from script source");
            if (this.isFlushingEditorTextToScript)
                throw new InvalidOperationException("Already flushing text editor to script source");

            this.isFlushingEditorTextToScript = true;
            sender.Script.SourceCode = document.Text;
            this.isFlushingEditorTextToScript = false;
        }
    }

    private void OnScriptSourceCodeChanged(Script script, string oldSourceCode, string newSourceCode) {
        if (!this.isFlushingEditorTextToScript) {
            this.isUpdatingCodeEditorText = true;
            TextDocument document = GetScriptTextDocument(script);
            Debug.Assert(this.PART_CodeEditor.Document == document);

            document.Text = newSourceCode;
            this.isUpdatingCodeEditorText = false;
        }
    }

    public void OnWindowOpened(IDesktopWindow sender) {
        this.Window = sender;
    }

    public void OnWindowClosed() {
        this.ScriptingManager = null;
        this.Window = null;
    }
}