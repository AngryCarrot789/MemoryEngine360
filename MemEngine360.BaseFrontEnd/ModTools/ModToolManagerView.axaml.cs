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
using MemEngine360.ModTools;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils.Debouncing;
using TextMateSharp.Grammars;

namespace MemEngine360.BaseFrontEnd.ModTools;

public partial class ModToolManagerView : UserControl {
    public static readonly DataKey<TextDocument> ModToolTextDocumentDataKey = DataKeys.Create<TextDocument>(nameof(ModToolTextDocumentDataKey));
    public static readonly StyledProperty<ModToolManager?> ModToolManagerProperty = AvaloniaProperty.Register<ModToolManagerView, ModToolManager?>(nameof(ModToolManager));

    public ModToolManager? ModToolManager {
        get => this.GetValue(ModToolManagerProperty);
        set => this.SetValue(ModToolManagerProperty, value);
    }

    private bool isUpdatingTabSelection;
    private bool isUpdatingCodeEditorText, isFlushingEditorTextToModTool;
    private readonly TextMate.Installation myTextMate;
    private readonly TimerDispatcherDebouncer debounceFlushText;

    // The mod tool currently being shown, i.e. the tab control's selected tab's mod tool
    private ModTool? activeModTool;

    private readonly IBinder<ModTool> binderClearConsoleOnEachRun =
        new AvaloniaPropertyToEventPropertyBinder<ModTool>(
            ToggleButton.IsCheckedProperty,
            nameof(ModTool.ClearConsoleOnRunChanged),
            b => ((CheckBox) b.Control).IsChecked = b.Model.ClearConsoleOnRun,
            b => b.Model.ClearConsoleOnRun = ((CheckBox) b.Control).IsChecked == true);

    private readonly CompilationFailureRenderer myCompilationFailureMarkerService;

    public IDesktopWindow? Window { get; private set; }

    public ModToolManagerView() {
        this.InitializeComponent();
        this.PART_TabControl.SelectionChanged += this.PART_TabControlOnSelectionChanged;
        this.PART_CodeEditor.TextChanged += this.PART_CodeEditorOnTextChanged;
        this.PART_CodeEditor.LostFocus += this.OnTextEditorLostFocus;
        this.PART_CodeEditor.Options.ConvertTabsToSpaces = true;
        this.PART_CodeEditor.Options.CutCopyWholeLine = true;

        this.binderClearConsoleOnEachRun.AttachControl(this.PART_ClearConsoleOnEachRun);
        this.debounceFlushText = new TimerDispatcherDebouncer(TimeSpan.FromSeconds(2), static t => ((ModToolManagerView) t!).FlushCurrentDocumentToModTool(), this);

        RegistryOptions options = new RegistryOptions(ThemeName.Dark);
        this.myTextMate = this.PART_CodeEditor.InstallTextMate(options);
        this.myTextMate.SetGrammar(options.GetScopeByLanguageId(options.GetLanguageByExtension(".lua").Id));

        this.myCompilationFailureMarkerService = new CompilationFailureRenderer(this);
        this.PART_CodeEditor.TextArea.TextView.BackgroundRenderers.Add(this.myCompilationFailureMarkerService);
    }

    static ModToolManagerView() {
        ModToolManagerProperty.Changed.AddClassHandler<ModToolManagerView, ModToolManager?>((s, e) => s.OnModToolManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);

        ApplicationPFX.Instance.Dispatcher.Post(static t => {
            // Force redraw view port. For some reason without this, the text editor
            // colours do not apply until you type something or switch tabs
            ModToolManagerView self = (ModToolManagerView) t!;
            self.myTextMate.EditorModel.InvalidateViewPortLines();
            self.PART_CodeEditor.TextArea.TextView.Redraw();
        }, this, DispatchPriority.Background);
    }

    private void PART_CodeEditorOnTextChanged(object? sender, EventArgs e) {
        if (!this.isUpdatingCodeEditorText && this.activeModTool != null) {
            this.myCompilationFailureMarkerService.Clear();
            this.debounceFlushText.InvokeOrPostpone();
            if (this.activeModTool != null)
                this.activeModTool.HasUnsavedChanges = true;
        }
    }

    private void OnTextEditorLostFocus(object? sender, RoutedEventArgs e) {
        this.FlushCurrentDocumentToModTool();
    }

    private void FlushCurrentDocumentToModTool() {
        if (this.isUpdatingCodeEditorText) {
            throw new InvalidOperationException("Reentrancy -- currently updating editor due to mod tool code changed");
        }

        if (this.activeModTool != null) {
            this.isFlushingEditorTextToModTool = true;
            TextDocument document = GetModToolTextDocument(this.activeModTool);
            Debug.Assert(document == this.PART_CodeEditor.Document);

            if (this.activeModTool.SourceCode != document.Text) {
                this.activeModTool.SourceCode = document.Text;
            }

            this.isFlushingEditorTextToModTool = false;
        }
    }

    private static TextDocument GetModToolTextDocument(ModTool modTool) {
        TextDocument? doc = ModToolTextDocumentDataKey.GetContext(modTool.UserContext);
        if (doc == null) {
            modTool.UserContext.Set(ModToolTextDocumentDataKey, doc = new TextDocument(modTool.SourceCode));
        }

        return doc;
    }

    private void PART_TabControlOnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        if (this.ModToolManager != null) {
            int idx = this.PART_TabControl.SelectedIndex;
            ModToolManagerViewState.GetInstance(this.ModToolManager).SelectedModTool = idx == -1 ? null : this.ModToolManager.ModTools[idx];
        }
    }

    private void OnModToolManagerChanged(ModToolManager? oldValue, ModToolManager? newValue) {
        if (oldValue != null) {
            ModToolManagerViewState vs = ModToolManagerViewState.GetInstance(oldValue);
            if (vs.SelectedModTool != null) {
                this.OnSelectedModToolChanged(vs, vs.SelectedModTool, null);
            }

            vs.SelectedModToolChanged -= this.OnSelectedModToolChanged;
            this.PART_TabControl.ModToolManager = null;
        }

        if (newValue != null) {
            ModToolManagerViewState vs = ModToolManagerViewState.GetInstance(newValue);
            this.PART_TabControl.ModToolManager = newValue;
            vs.SelectedModToolChanged += this.OnSelectedModToolChanged;
            if (vs.SelectedModTool != null) {
                this.OnSelectedModToolChanged(vs, null, vs.SelectedModTool);
            }
        }

        DataManager.GetContextData(this).Set(ModToolManager.DataKey, newValue);
    }

    private void OnSelectedModToolChanged(ModToolManagerViewState sender, ModTool? oldSel, ModTool? newSel) {
        if (this.isFlushingEditorTextToModTool)
            throw new InvalidOperationException("Currently flushing text editor to mod tool source");

        this.activeModTool = newSel;
        this.debounceFlushText.Reset(); // cancel flush callback
        this.myCompilationFailureMarkerService.Clear();
        if (oldSel != null) {
            ModToolViewState.GetInstance(oldSel).FlushEditorToModTool -= this.OnRequestFlushEditorToModTool;
            oldSel.SourceCodeChanged -= this.OnModToolSourceCodeChanged;
            oldSel.CompilationFailure -= this.OnModToolCompilationFailed;

            // in case document wasn't flushed to mod tool, do it now
            FlushToModTool(oldSel, GetModToolTextDocument(oldSel));
        }

        if (newSel != null) {
            ModToolViewState.GetInstance(newSel).FlushEditorToModTool += this.OnRequestFlushEditorToModTool;
            newSel.SourceCodeChanged += this.OnModToolSourceCodeChanged;
            newSel.CompilationFailure += this.OnModToolCompilationFailed;

            TextDocument document = GetModToolTextDocument(newSel);
            document.Text = newSel.SourceCode; // source code may have changed, so update document

            this.isUpdatingCodeEditorText = true;
            this.PART_CodeEditor.Document = document;
            this.isUpdatingCodeEditorText = false;
            this.PART_ModToolPanel.IsEnabled = true;
        }
        else {
            this.PART_CodeEditor.Document = new TextDocument();
            this.PART_ModToolPanel.IsEnabled = false;
        }

        this.binderClearConsoleOnEachRun.SwitchModel(newSel);
        this.isUpdatingTabSelection = true;
        this.PART_TabControl.SelectedIndex = newSel == null ? -1 : (newSel.Manager?.ModTools.IndexOf(newSel) ?? -1);
        this.isUpdatingTabSelection = false;

        this.PART_ConsoleTextList.ItemsSource = newSel?.ConsoleLines;
        DataManager.GetContextData(this.PART_ModToolPanel).Set(ModTool.DataKey, newSel);
    }

    private void OnRequestFlushEditorToModTool(ModToolViewState sender) {
        TextDocument document = GetModToolTextDocument(sender.ModTool);
        if (document != this.PART_CodeEditor.Document) {
            FlushToModTool(sender.ModTool, document);
        }
        else {
            // When current document equals the mod tool's document, ensure we aren't already doing stuff.
            // Ideally we never will be unless someone is using dispatcher frames (i.e. Dispatcher.Invoke())
            Debug.Assert(this.activeModTool == sender.ModTool, "Expected currentModTool to equal sender's mod tool when the documents are equal");
            if (this.isUpdatingCodeEditorText)
                throw new InvalidOperationException("Already updating text editor from mod tool source");
            if (this.isFlushingEditorTextToModTool)
                throw new InvalidOperationException("Already flushing text editor to mod tool source");

            this.isFlushingEditorTextToModTool = true;
            FlushToModTool(sender.ModTool, document);
            this.isFlushingEditorTextToModTool = false;
        }
    }

    private static void FlushToModTool(ModTool modTool, TextDocument document) {
        string newText = document.Text;
        if (newText != modTool.SourceCode) {
            modTool.SourceCode = newText;
        }
    }

    private void OnModToolSourceCodeChanged(ModTool modTool, string oldSourceCode, string newSourceCode) {
        Debug.Assert(this.activeModTool == modTool);

        if (!this.isFlushingEditorTextToModTool) {
            this.isUpdatingCodeEditorText = true;
            TextDocument document = GetModToolTextDocument(modTool);
            Debug.Assert(this.PART_CodeEditor.Document == document);

            document.Text = newSourceCode;
            this.isUpdatingCodeEditorText = false;
        }
    }

    private void OnModToolCompilationFailed(ModTool sender, string? chunkName, SourcePosition position) {
        TextDocument document = GetModToolTextDocument(sender);
        Debug.Assert(this.PART_CodeEditor.Document == document);

        Caret? caret = this.PART_CodeEditor.TextArea.Caret;
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
        this.ModToolManager = null;
        this.Window = null;
    }

    private class CompilationFailureRenderer(ModToolManagerView view) : IBackgroundRenderer {
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