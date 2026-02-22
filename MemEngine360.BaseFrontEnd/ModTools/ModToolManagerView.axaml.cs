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
using MemEngine360.BaseFrontEnd.Scripting;
using MemEngine360.ModTools;
using MemEngine360.ModTools.Commands;
using MemEngine360.Scripting;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Events;
using TextMateSharp.Grammars;

namespace MemEngine360.BaseFrontEnd.ModTools;

public partial class ModToolManagerView : UserControl {
    public static readonly StyledProperty<ModToolManager?> ModToolManagerProperty = AvaloniaProperty.Register<ModToolManagerView, ModToolManager?>(nameof(ModToolManager));

    public ModToolManager? ModToolManager {
        get => this.GetValue(ModToolManagerProperty);
        set => this.SetValue(ModToolManagerProperty, value);
    }

    private bool isUpdatingTabSelection;
    private readonly TextMate.Installation myTextMate;

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
        this.PART_CodeEditor.Options.ConvertTabsToSpaces = true;
        this.PART_CodeEditor.Options.CutCopyWholeLine = true;

        this.binderClearConsoleOnEachRun.AttachControl(this.PART_ClearConsoleOnEachRun);

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
        if (this.activeModTool != null) {
            this.myCompilationFailureMarkerService.Clear();
            this.activeModTool?.HasUnsavedChanges = true;
        }
    }

    private static TextDocument GetModToolTextDocument(ModTool modTool) {
        return ((AvaloniaEditLuaScriptDocumentImpl) modTool.Document).Document;
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
                this.OnSelectedModToolChanged(vs, new ValueChangedEventArgs<ModTool?>(vs.SelectedModTool, null));
            }

            vs.SelectedModToolChanged -= this.OnSelectedModToolChanged;
            this.PART_TabControl.ModToolManager = null;
        }

        if (newValue != null) {
            ModToolManagerViewState vs = ModToolManagerViewState.GetInstance(newValue);
            this.PART_TabControl.ModToolManager = newValue;
            vs.SelectedModToolChanged += this.OnSelectedModToolChanged;
            if (vs.SelectedModTool != null) {
                this.OnSelectedModToolChanged(vs, new ValueChangedEventArgs<ModTool?>(null, vs.SelectedModTool));
            }
        }

        DataManager.GetContextData(this).Set(ModToolManager.DataKey, newValue);
    }

    private void OnSelectedModToolChanged(object? o, ValueChangedEventArgs<ModTool?> e) {
        this.activeModTool = e.NewValue;
        this.myCompilationFailureMarkerService.Clear();
        if (e.OldValue != null) {
            e.OldValue.CompilationFailure -= this.OnModToolCompilationFailed;
            this.PART_CodeEditor.TextChanged -= this.PART_CodeEditorOnTextChanged;
        }

        if (e.NewValue != null) {
            e.NewValue.CompilationFailure += this.OnModToolCompilationFailed;
            this.PART_CodeEditor.Document = GetModToolTextDocument(e.NewValue);
            this.PART_CodeEditor.TextChanged += this.PART_CodeEditorOnTextChanged;
        }
        else {
            this.PART_CodeEditor.Document = new TextDocument();
        }

        this.binderClearConsoleOnEachRun.SwitchModel(e.NewValue);
        this.isUpdatingTabSelection = true;
        this.PART_TabControl.SelectedIndex = e.NewValue == null ? -1 : (e.NewValue.Manager?.ModTools.IndexOf(e.NewValue) ?? -1);
        this.isUpdatingTabSelection = false;

        this.PART_ConsoleTextList.ItemsSource = e.NewValue?.ConsoleLines;
        DataManager.GetContextData(this.PART_ModToolPanel).Set(ModTool.DataKey, e.NewValue);
    }

    private void OnModToolCompilationFailed(object? sender, CompilationFailureEventArgs e) {
        TextDocument document = GetModToolTextDocument((ModTool) sender!);
        Debug.Assert(this.PART_CodeEditor.Document == document);

        Caret? caret = this.PART_CodeEditor.TextArea.Caret;
        caret.Line = e.SourcePosition.Line;
        caret.Column = e.SourcePosition.Column;
        this.PART_CodeEditor.ScrollTo(e.SourcePosition.Line, e.SourcePosition.Column);

        ApplicationPFX.Instance.Dispatcher.Post(() => {
            VisualLine? line = this.PART_CodeEditor.TextArea.TextView.GetVisualLine(e.SourcePosition.Line);
            if (line != null) {
                this.myCompilationFailureMarkerService.SetMarker(caret.Offset, (line.VisualLength - e.SourcePosition.Column) + 1);
            }
        }, DispatchPriority.BeforeRender);
    }

    public void OnWindowOpened(IDesktopWindow sender) {
        this.Window = sender;
    }

    public enum CloseRequest { Cancel, Close }
    
    public async Task<CloseRequest> OnClosingAsync(IDesktopWindow sender, bool canCancel) {
        ModToolManager? mm = this.ModToolManager;
        if (mm == null) {
            return CloseRequest.Close;
        }

        mm.ModTools.ForEach(x => x.RequestCancelCompilation());
        if (mm.ModTools.Any(x => x.HasUnsavedChanges)) {
            using IDisposable _ = CommandManager.LocalContextManager.PushContext(new ContextData().Set(ITopLevel.TopLevelDataKey, sender));
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(
                "Unsaved changes", 
                "Do you want to save changes?", 
                canCancel ? MessageBoxButtons.YesNoCancel : MessageBoxButtons.YesNo, 
                MessageBoxResult.Yes);
            
            if (canCancel && result != MessageBoxResult.Yes && result != MessageBoxResult.No) {
                return CloseRequest.Cancel; // cancel close
            }

            if (result == MessageBoxResult.Yes) {
                bool savedAll = await SaveAllModToolsCommand.SaveAllAsync(mm.ModTools.ToList());
                if (savedAll)
                    return CloseRequest.Close;
                
                return canCancel ? CloseRequest.Cancel : CloseRequest.Close;
            }
        }

        return CloseRequest.Close;
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