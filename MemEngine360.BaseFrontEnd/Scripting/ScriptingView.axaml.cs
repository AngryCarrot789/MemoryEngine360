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
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using Lua.CodeAnalysis;
using MemEngine360.Scripting;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils.Debouncing;
using TextMateSharp.Grammars;

namespace MemEngine360.BaseFrontEnd.Scripting;

public partial class ScriptingView : UserControl {
    public static readonly DataKey<TextDocument> ScriptTextDocumentDataKey = DataKeys.Create<TextDocument>(nameof(ScriptTextDocumentDataKey));
    private static readonly DataKey<CompilationFailureRenderer> CompilationFailureRendererDataKey = DataKeys.Create<CompilationFailureRenderer>(nameof(CompilationFailureRendererDataKey));
    public static readonly StyledProperty<ScriptingManager?> ScriptingManagerProperty = AvaloniaProperty.Register<ScriptingView, ScriptingManager?>(nameof(ScriptingManager));

    public ScriptingManager? ScriptingManager {
        get => this.GetValue(ScriptingManagerProperty);
        set => this.SetValue(ScriptingManagerProperty, value);
    }

    private bool isUpdatingTabSelection;
    private bool isUpdatingCodeEditorText, isFlushingEditorTextToScript;
    private readonly TextMate.Installation myTextMate;
    private readonly TimerDispatcherDebouncer debounceFlushText;

    // The script currently being shown, i.e. the tab control's selected tab's script
    private Script? activeScript;

    private readonly IBinder<Script> binderClearConsoleOnEachRun =
        new AvaloniaPropertyToEventPropertyBinder<Script>(
            ToggleButton.IsCheckedProperty,
            nameof(Script.ClearConsoleOnRunChanged),
            b => ((CheckBox) b.Control).IsChecked = b.Model.ClearConsoleOnRun,
            b => b.Model.ClearConsoleOnRun = ((CheckBox) b.Control).IsChecked == true);

    private readonly CompilationFailureRenderer myCompilationFailureMarkerService;

    public IDesktopWindow? Window { get; private set; }

    public ScriptingView() {
        this.InitializeComponent();
        this.PART_TabControl.SelectionChanged += this.PART_TabControlOnSelectionChanged;
        this.PART_CodeEditor.TextChanged += this.PART_CodeEditorOnTextChanged;
        this.PART_CodeEditor.LostFocus += this.OnTextEditorLostFocus;
        this.PART_CodeEditor.Options.ConvertTabsToSpaces = true;
        this.PART_CodeEditor.Options.CutCopyWholeLine = true;
        
        this.binderClearConsoleOnEachRun.AttachControl(this.PART_ClearConsoleOnEachRun);
        this.debounceFlushText = new TimerDispatcherDebouncer(TimeSpan.FromSeconds(2), static t => ((ScriptingView) t!).FlushCurrentDocumentToScript(), this);

        RegistryOptions options = new RegistryOptions(ThemeName.Dark);
        this.myTextMate = this.PART_CodeEditor.InstallTextMate(options);
        this.myTextMate.SetGrammar(options.GetScopeByLanguageId(options.GetLanguageByExtension(".lua").Id));

        this.myCompilationFailureMarkerService = new CompilationFailureRenderer(this);
        this.PART_CodeEditor.TextArea.TextView.BackgroundRenderers.Add(this.myCompilationFailureMarkerService);
    }

    static ScriptingView() {
        ScriptingManagerProperty.Changed.AddClassHandler<ScriptingView, ScriptingManager?>((s, e) => s.OnScriptingManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);

        ApplicationPFX.Instance.Dispatcher.Post(static t => {
            // Force redraw view port. For some reason without this, the text editor
            // colours do not apply until you type something or switch tabs
            ScriptingView self = (ScriptingView) t!;
            self.myTextMate.EditorModel.InvalidateViewPortLines();
            self.PART_CodeEditor.TextArea.TextView.Redraw();
        }, this, DispatchPriority.Background);
    }

    private void PART_CodeEditorOnTextChanged(object? sender, EventArgs e) {
        if (!this.isUpdatingCodeEditorText && this.activeScript != null) {
            this.myCompilationFailureMarkerService.Clear();
            this.debounceFlushText.InvokeOrPostpone();
            if (this.activeScript != null)
                this.activeScript.HasUnsavedChanges = true;
        }
    }

    private void OnTextEditorLostFocus(object? sender, RoutedEventArgs e) {
        this.FlushCurrentDocumentToScript();
    }

    private void FlushCurrentDocumentToScript() {
        if (this.isUpdatingCodeEditorText) {
            throw new InvalidOperationException("Reentrancy -- currently updating editor due to script code changed");
        }

        if (this.activeScript != null) {
            this.isFlushingEditorTextToScript = true;
            TextDocument document = GetScriptTextDocument(this.activeScript);
            Debug.Assert(document == this.PART_CodeEditor.Document);

            if (this.activeScript.SourceCode != document.Text) {
                this.activeScript.SourceCode = document.Text;
            }

            this.isFlushingEditorTextToScript = false;
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
            int idx = this.PART_TabControl.SelectedIndex;
            ScriptingManagerViewState.GetInstance(this.ScriptingManager).SelectedScript = idx == -1 ? null : this.ScriptingManager.Scripts[idx];
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

        this.activeScript = newSel;
        this.debounceFlushText.Reset(); // cancel flush callback
        this.myCompilationFailureMarkerService.Clear();
        if (oldSel != null) {
            ScriptViewState.GetInstance(oldSel).FlushEditorToScript -= this.OnRequestFlushEditorToScript;
            oldSel.SourceCodeChanged -= this.OnScriptSourceCodeChanged;
            oldSel.CompilationFailure -= this.OnScriptCompilationFailed;

            // in case document wasn't flushed to script, do it now
            FlushToScript(oldSel, GetScriptTextDocument(oldSel));
        }

        if (newSel != null) {
            ScriptViewState.GetInstance(newSel).FlushEditorToScript += this.OnRequestFlushEditorToScript;
            newSel.SourceCodeChanged += this.OnScriptSourceCodeChanged;
            newSel.CompilationFailure += this.OnScriptCompilationFailed;

            TextDocument document = GetScriptTextDocument(newSel);
            document.Text = newSel.SourceCode; // source code may have changed, so update document

            this.isUpdatingCodeEditorText = true;
            this.PART_CodeEditor.Document = document;
            this.isUpdatingCodeEditorText = false;
            this.PART_ScriptPanel.IsEnabled = true;
        }
        else {
            this.PART_CodeEditor.Document = new TextDocument();
            this.PART_ScriptPanel.IsEnabled = false;
        }

        this.binderClearConsoleOnEachRun.SwitchModel(newSel);
        this.isUpdatingTabSelection = true;
        this.PART_TabControl.SelectedIndex = newSel == null ? -1 : (newSel.Manager?.Scripts.IndexOf(newSel) ?? -1);
        this.isUpdatingTabSelection = false;

        this.PART_ConsoleTextList.ItemsSource = newSel?.ConsoleLines;
        DataManager.GetContextData(this.PART_ScriptPanel).Set(Script.DataKey, newSel);
    }

    private void OnRequestFlushEditorToScript(ScriptViewState sender) {
        TextDocument document = GetScriptTextDocument(sender.Script);
        if (document != this.PART_CodeEditor.Document) {
            FlushToScript(sender.Script, document);
        }
        else {
            // When current document equals the script's document, ensure we aren't already doing stuff.
            // Ideally we never will be unless someone is using dispatcher frames (i.e. Dispatcher.Invoke())
            Debug.Assert(this.activeScript == sender.Script, "Expected currentScript to equal sender's script when the documents are equal");
            if (this.isUpdatingCodeEditorText)
                throw new InvalidOperationException("Already updating text editor from script source");
            if (this.isFlushingEditorTextToScript)
                throw new InvalidOperationException("Already flushing text editor to script source");

            this.isFlushingEditorTextToScript = true;
            FlushToScript(sender.Script, document);
            this.isFlushingEditorTextToScript = false;
        }
    }

    private static void FlushToScript(Script script, TextDocument document) {
        string newText = document.Text;
        if (newText != script.SourceCode) {
            script.SourceCode = newText;
        }
    }

    private void OnScriptSourceCodeChanged(Script script, string oldSourceCode, string newSourceCode) {
        Debug.Assert(this.activeScript == script);

        if (!this.isFlushingEditorTextToScript) {
            this.isUpdatingCodeEditorText = true;
            TextDocument document = GetScriptTextDocument(script);
            Debug.Assert(this.PART_CodeEditor.Document == document);

            document.Text = newSourceCode;
            this.isUpdatingCodeEditorText = false;
        }
    }

    private void OnScriptCompilationFailed(Script sender, string? chunkName, SourcePosition position) {
        TextDocument document = GetScriptTextDocument(sender);
        Debug.Assert(this.PART_CodeEditor.Document == document);

        Caret caret = this.PART_CodeEditor.TextArea.Caret;
        caret.Line = position.Line;
        caret.Column = position.Column;
        this.PART_CodeEditor.ScrollTo(position.Line, position.Column);

        ApplicationPFX.Instance.Dispatcher.Post(() => {
            VisualLine? line = this.PART_CodeEditor.TextArea.TextView.GetVisualLine(position.Line);
            if (line != null) {
                this.myCompilationFailureMarkerService.SetMarker(caret.Offset, (line.VisualLength - position.Column) + 1);
            }
        }, DispatchPriority.BeforeRender);
    }

    public void OnWindowOpened(IDesktopWindow sender) {
        this.Window = sender;
    }

    public void OnWindowClosed() {
        this.ScriptingManager = null;
        this.Window = null;
    }

    private class CompilationFailureRenderer(ScriptingView view) : IBackgroundRenderer {
        private static Pen? s_Pen;
        private TextSegment? myMarker;

        public KnownLayer Layer => KnownLayer.Selection;

        public void SetMarker(int startOffset, int length) {
            if (length <= 0) {
                this.Clear();
                return;
            }
            
            this.myMarker = new TextSegment() {
                StartOffset = startOffset,
                Length = length
            };

            ApplicationPFX.Instance.Dispatcher.Post(() => {
                view.PART_CodeEditor.TextArea.TextView.Redraw(startOffset, length);
            }, DispatchPriority.BeforeRender);
        }

        public void Clear() {
            if (this.myMarker != null) {
                int start = this.myMarker.StartOffset;
                int length = this.myMarker.Length;
                this.myMarker = null;

                ApplicationPFX.Instance.Dispatcher.Post(() => {
                    view.PART_CodeEditor.TextArea.TextView.Redraw(start, length);
                }, DispatchPriority.BeforeRender);
            }
        }

        public void Draw(TextView textView, DrawingContext drawingContext) {
            if (!textView.VisualLinesValid || this.myMarker == null) {
                return;
            }

            foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, this.myMarker)) {
                s_Pen ??= new Pen(Brushes.Red, 2);
                double y = rect.Bottom - 2;
                drawingContext.DrawLine(s_Pen, new Point(rect.Left, y), new Point(rect.Right, y));
            }
        }
    }
}