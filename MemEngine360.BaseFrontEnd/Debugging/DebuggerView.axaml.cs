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
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaHex.Async.Editing;
using AvaloniaHex.Async.Rendering;
using AvaloniaHex.Base.Document;
using MemEngine360.BaseFrontEnd.Services.HexEditing;
using MemEngine360.Connections;
using MemEngine360.Engine.Debugging;
using MemEngine360.Engine.HexEditing;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Avalonia.Themes.BrushFactories;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Logging;

namespace MemEngine360.BaseFrontEnd.Debugging;

public partial class DebuggerView : UserControl, IDebuggerWindow {
    public static readonly StyledProperty<ConsoleDebugger?> ConsoleDebuggerProperty = AvaloniaProperty.Register<DebuggerView, ConsoleDebugger?>(nameof(ConsoleDebugger));

    public ConsoleDebugger? ConsoleDebugger {
        get => this.GetValue(ConsoleDebuggerProperty);
        set => this.SetValue(ConsoleDebuggerProperty, value);
    }
    
    public IDesktopWindow? Window { get; private set; }

    private readonly IBinder<ConsoleDebugger> autoRefreshBinder = new AvaloniaPropertyToEventPropertyBinder<ConsoleDebugger>(ToggleButton.IsCheckedProperty, nameof(ConsoleDebugger.RefreshRegistersOnActiveThreadChangeChanged), (b) => b.Control.SetValue(ToggleButton.IsCheckedProperty, b.Model.RefreshRegistersOnActiveThreadChange), b => b.Model.RefreshRegistersOnActiveThreadChange = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ConsoleDebugger> autoAddRemoveThreadsBinder = new EventUpdateBinder<ConsoleDebugger>(nameof(ConsoleDebugger.AutoAddOrRemoveThreadsChanged), (b) => b.Control.SetValue(CheckBox.IsCheckedProperty, b.Model.AutoAddOrRemoveThreads));
    private readonly IBinder<ConsoleDebugger> currentConnectionTypeBinder = new EventUpdateBinder<ConsoleDebugger>(nameof(ConsoleDebugger.ConnectionChanged), (b) => ((TextBlock) b.Control).Text = (b.Model.Connection?.ConnectionType.DisplayName ?? "Not Connected"));

    private readonly IBinder<ConsoleDebugger> isConsoleRunningBinder = new MultiEventUpdateBinder<ConsoleDebugger>([nameof(ConsoleDebugger.IsConsoleRunningChanged), nameof(ConsoleDebugger.ConsoleExecutionStateChanged)], (b) => {
        string? text = b.Model.ConsoleExecutionState;
        if (string.IsNullOrWhiteSpace(text)) {
            bool? run = b.Model.IsConsoleRunning;
            text = run.HasValue ? run.Value ? "Running" : "Stopped" : "Unknown Exec State";
        }

        ((TextBlock) b.Control).Text = text;
    });

    private readonly MultiBrushFlipFlopTimer timer;
    private bool isUpdatingSelectedLBI;
    private ThreadMemoryAutoRefresh? autoRefresh;
    internal readonly HexEditorChangeManager changeManager;

    public DebuggerView() {
        this.InitializeComponent();
        this.autoRefreshBinder.AttachControl(this.PART_AutoRefreshRegistersOnThreadChange);
        this.autoAddRemoveThreadsBinder.AttachControl(this.PART_ToggleAutoAddRemoveThreads);
        this.currentConnectionTypeBinder.AttachControl(this.PART_ActiveConnectionTextBoxRO);
        this.isConsoleRunningBinder.AttachControl(this.PART_RunningState);
        this.PART_ThreadListBox.SelectionChanged += this.OnThreadListBoxSelectionChanged;

        this.PART_GotoTextBox.KeyDown += this.PART_GotoTextBoxOnKeyDown;
        this.PART_GotoTextBox.AcceptsReturn = false;
        this.PART_GotoTextBox.AcceptsTab = false;

        this.changeManager = new HexEditorChangeManager(this.PART_HexEditor);

        AsyncHexView view = this.PART_HexEditor.HexView;
        view.BytesPerLine = 16;
        view.Columns.Add(new OffsetColumn());
        view.Columns.Add(new HexColumn());
        view.Columns.Add(new AsciiColumn());

        this.PART_HexEditor.Caret.LocationChanged += (sender, args) => this.UpdateCaretText();
        this.PART_HexEditor.Caret.PrimaryColumnChanged += (sender, args) => this.UpdateCaretText();
        this.PART_HexEditor.Selection.RangeChanged += (sender, args) => this.UpdateSelectionText();

        this.timer = new MultiBrushFlipFlopTimer(TimeSpan.FromMilliseconds(500), [
            new BrushExchange(this.PART_RunningState, ForegroundProperty, StandardIcons.ForegroundBrush, new ConstantAvaloniaColourBrush(Brushes.Black)),
            new BrushExchange(this.PART_RunningState, BackgroundProperty, StandardIcons.TransparentBrush, new ConstantAvaloniaColourBrush(Brushes.Yellow)),
        ]) { LevelChangesToStop = 7 /* stop on HIGH state */, StartHigh = true };

        DataManager.GetContextData(this).Set(IDebuggerWindow.DataKey, this);
        this.PART_GotoTextBox.Text = "82600000";
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);
        if (uint.TryParse(this.PART_GotoTextBox.Text, NumberStyles.HexNumber, null, out uint address)) {
            this.ScrollToAddressAndMoveCaret(address, out _);
        }
    }

    internal void UpdateSelectionText() {
        Selection sel = this.PART_HexEditor.Selection;
        if (sel.Range.IsEmpty) {
            this.PART_SelectionText.Text = "<none>";
        }
        else {
            ulong end = sel.Range.End.ByteIndex;
            this.PART_SelectionText.Text = $"{sel.Range.ByteLength} bytes ({sel.Range.Start.ByteIndex:X8} -> {(end > 0 ? (end - 1) : 0):X8})";
        }
    }

    internal void UpdateCaretText() {
        BitLocation pos = this.PART_HexEditor.Caret.Location;
        this.PART_CaretText.Text = $"{pos.ByteIndex:X8}";
    }

    private void PART_GotoTextBoxOnKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key == Key.Enter) {
            string? text = ((TextBox) sender!).Text;
            if (text != null && text.StartsWith("0x")) {
                text = text.Substring(2);
            }

            if (uint.TryParse(text, NumberStyles.HexNumber, null, out uint parsedAddress)) {
                this.ScrollToAddressAndMoveCaret(parsedAddress, out _);
            }
        }
    }

    public void ScrollToAddressAndMoveCaret(uint address, out ulong lineStartOffset) {
        this.PART_HexEditor.HexView.ScrollToByteOffset(address, out lineStartOffset);
        this.PART_HexEditor.ResetSelection();
        this.PART_HexEditor.Caret.Location = new BitLocation(address);
        this.PART_HexEditor.Selection.Range = new BitRange(new BitLocation(address), new BitLocation(address + 1));
    }

    static DebuggerView() {
        ConsoleDebuggerProperty.Changed.AddClassHandler<DebuggerView, ConsoleDebugger?>((s, e) => s.OnConsoleDebuggerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    internal void OnWindowOpened(IDesktopWindow window) {
        this.Window = window;
        
        if (this.ConsoleDebugger != null) {
            this.ConsoleDebugger.IsWindowVisible = true;
            this.PART_EventViewer.BusyLock = this.ConsoleDebugger.BusyLock;
            this.PART_EventViewer.ConsoleConnection = this.ConsoleDebugger.Connection;
        }

        this.timer.EnableTargets();
    }

    internal async Task OnClosingAsync(WindowCloseReason reason) {
        this.timer.ClearTarget();

        ConsoleDebugger? debugger = this.ConsoleDebugger;
        this.ConsoleDebugger = null;

        if (debugger == null)
            return;

        this.autoRefresh?.Dispose();
        this.autoRefresh = null;

        this.PART_EventViewer.ConsoleConnection = null;
        this.PART_EventViewer.BusyLock = null;
        debugger.IsConsoleRunning = null;
        debugger.ConsoleExecutionState = null;
        debugger.IsWindowVisible = false;
        if (reason != WindowCloseReason.WindowClosing) {
            using IDisposable? token = await debugger.BusyLock.BeginBusyOperationAsync(1000);
            if (token == null) {
                AppLogger.Instance.WriteLine("Warning: could not obtain busy token to safely disconnect debugger connection");
                return; // probably cannot cancel window closing here
            }

            IConsoleConnection? connection = debugger.Connection;
            if (connection != null) {
                debugger.SetConnection(token, null);
                try {
                    connection.Close();
                }
                catch {
                    // ignored
                }
            }
        }
    }

    internal void OnWindowClosed() {
        this.Window = null;
    }

    private void OnConsoleDebuggerChanged(ConsoleDebugger? oldValue, ConsoleDebugger? newValue) {
        if (oldValue != null) {
            oldValue.ConnectionChanged -= this.OnConsoleConnectionChanged;
            oldValue.ActiveThreadChanged -= this.OnActiveThreadChanged;
            oldValue.IsConsoleRunningChanged -= this.OnIsConsoleRunningChanged;
            this.PART_HexEditor.BinarySource = null;
            this.changeManager.Clear();
            this.changeManager.OnBinarySourceChanged(null);
            this.UpdateSelectionText();
            this.UpdateCaretText();
        }

        if (newValue != null) {
            newValue.ConnectionChanged += this.OnConsoleConnectionChanged;
            newValue.ActiveThreadChanged += this.OnActiveThreadChanged;
            newValue.IsConsoleRunningChanged += this.OnIsConsoleRunningChanged;
            this.SetSourceForConnection(newValue.Connection, restartAutoRefresh: false);
        }

        this.timer.IsEnabled = newValue != null && newValue.IsConsoleRunning != true;

        this.PART_ThreadListBox.SetItemsSource(newValue?.ThreadEntries);
        this.PART_RegistersListBox.SetItemsSource(newValue?.RegisterEntries);
        this.PART_FunctionCallFrame.SetItemsSource(newValue?.FunctionCallEntries);

        this.autoRefreshBinder.SwitchModel(newValue);
        this.autoAddRemoveThreadsBinder.SwitchModel(newValue);
        this.currentConnectionTypeBinder.SwitchModel(newValue);
        this.isConsoleRunningBinder.SwitchModel(newValue);

        // allows it to check the endianness of the connection
        this.PART_RegistersListBox.ConsoleDebugger = newValue;
        this.PART_ThreadListBox.ConsoleDebugger = newValue;

        if (newValue != null)
            DataManager.GetContextData(this).Set(ConsoleDebugger.DataKey, newValue);
        else 
            DataManager.GetContextData(this).Remove(ConsoleDebugger.DataKey);

        if (this.Window != null && this.Window.OpenState == OpenState.Open) {
            this.PART_EventViewer.BusyLock = newValue?.BusyLock;
            this.PART_EventViewer.ConsoleConnection = newValue?.Connection;
        }

        this.RestartAutoRefresh();
    }

    private void OnConsoleConnectionChanged(ConsoleDebugger sender, IConsoleConnection? oldConn, IConsoleConnection? newConn) {
        if (this.Window != null && this.Window.OpenState == OpenState.Open)
            this.SetSourceForConnection(newConn);
    }

    private void SetSourceForConnection(IConsoleConnection? connection, bool restartAutoRefresh = false) {
        ConsoleDebugger? debugger = this.ConsoleDebugger;
        if (debugger != null) {
            this.PART_EventViewer.ConsoleConnection = connection;
            ConsoleHexBinarySource source = new ConsoleHexBinarySource(new ConnectionLockPair(debugger.BusyLock, connection));
            this.PART_HexEditor.BinarySource = source;
            this.changeManager.Clear();
            this.changeManager.OnBinarySourceChanged(source);
            this.UpdateSelectionText();
            this.UpdateCaretText();
            if (restartAutoRefresh)
                this.RestartAutoRefresh();

            ApplicationPFX.Instance.Dispatcher.Post(() => {
                this.PART_HexEditor.HexView.BringIntoView(this.PART_HexEditor.Caret.Location);
            });
        }
    }

    private void RestartAutoRefresh() {
        this.autoRefresh?.Dispose();
        this.autoRefresh = null;

        ConsoleDebugger? debugger = this.ConsoleDebugger;
        if (debugger != null && debugger.Connection != null) {
            this.autoRefresh = new ThreadMemoryAutoRefresh(debugger, this);
            this.autoRefresh.Run();
        }
    }

    private void OnIsConsoleRunningChanged(ConsoleDebugger sender) {
        this.timer.IsEnabled = sender.IsConsoleRunning != true;
    }

    private void OnActiveThreadChanged(ConsoleDebugger sender, ThreadEntry? oldThread, ThreadEntry? newThread) {
        this.isUpdatingSelectedLBI = true;
        this.PART_ThreadListBox.SelectedModel = newThread;
        this.isUpdatingSelectedLBI = false;
    }

    private void OnThreadListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        if (!this.isUpdatingSelectedLBI && this.ConsoleDebugger != null)
            this.ConsoleDebugger.ActiveThread = this.PART_ThreadListBox.SelectedModel;
    }

    public void FocusGoToTextBox() {
        TextBoxFocusTransition.Focus(this.PART_GotoTextBox);
    }
}

public class TextBoxFocusTransition {
    private readonly TextBox targetFocus;
    private IInputElement? lastFocused;

    private TextBoxFocusTransition(TextBox targetFocus) {
        this.targetFocus = targetFocus;
    }

    public static void Focus(TextBox target, bool selectAll = true) {
        new TextBoxFocusTransition(target).Focus(selectAll);
    }

    private void OnTargetOnKeyDown(object? sender, KeyEventArgs e) {
        Debug.Assert(this.lastFocused != null);

        if (e.Key == Key.Enter || e.Key == Key.Escape) {
            e.Handled = true;
            this.FocusPrevious();
        }
    }

    private void OnTargetLostFocus(object? sender, RoutedEventArgs e) {
        this.lastFocused = null;
        this.targetFocus.LostFocus -= this.OnTargetLostFocus;
        this.targetFocus.KeyDown -= this.OnTargetOnKeyDown;
    }

    private void Focus(bool selectAll) {
        TopLevel? topLevel = TopLevel.GetTopLevel(this.targetFocus);
        this.lastFocused = topLevel?.FocusManager?.GetFocusedElement();
        if (ReferenceEquals(this.lastFocused, this.targetFocus)) {
            // Already focused, so remove handler to prevent leak
            this.lastFocused = null;
            return;
        }

        if (this.targetFocus.Focus() && this.targetFocus.IsFocused) {
            if (selectAll) {
                this.targetFocus.SelectAll();
                BugFix.TextBox_UpdateSelection(this.targetFocus);
            }

            this.targetFocus.LostFocus += this.OnTargetLostFocus;
            this.targetFocus.KeyDown += this.OnTargetOnKeyDown;
        }
        else {
            // Could not focus, so remove handler to prevent leak
            this.lastFocused = null;
        }
    }

    private void FocusPrevious() {
        if (this.lastFocused != null) {
            this.lastFocused.Focus();

            // OnTargetLostFocus should get called
            Debug.Assert(this.lastFocused == null);
        }
    }
}