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

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaHex.Base.Document;
using AvaloniaHex.Rendering;
using MemEngine360.BaseFrontEnd.Services.HexEditing;
using MemEngine360.Connections;
using MemEngine360.Engine.Debugging;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Avalonia.Themes.BrushFactories;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.BaseFrontEnd.Debugging;

public partial class DebuggerWindow : DesktopWindow {
    public static readonly StyledProperty<ConsoleDebugger?> ConsoleDebuggerProperty = AvaloniaProperty.Register<DebuggerWindow, ConsoleDebugger?>(nameof(ConsoleDebugger));

    public ConsoleDebugger? ConsoleDebugger {
        get => this.GetValue(ConsoleDebuggerProperty);
        set => this.SetValue(ConsoleDebuggerProperty, value);
    }

    private readonly IBinder<ConsoleDebugger> autoRefreshBinder = new EventUpdateBinder<ConsoleDebugger>(nameof(ConsoleDebugger.RefreshRegistersOnActiveThreadChangeChanged), (b) => b.Control.SetValue(ToggleButton.IsCheckedProperty, b.Model.RefreshRegistersOnActiveThreadChange));
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
    private readonly RateLimitedDispatchAction rldaUpdateMemoryDocument;
    private readonly OffsetColumn myOffsetColumn;
    private readonly HexEditorChangeManager changeManager;

    private long newCaretByteIndex;
    private long newSelectionByteIndex;
    private bool isDocumentFreshForChangeManager = true;
    private uint lastUpdateAddress;

    public DebuggerWindow() {
        this.InitializeComponent();
        this.autoRefreshBinder.AttachControl(this.PART_AutoRefreshRegistersOnThreadChange);
        this.autoAddRemoveThreadsBinder.AttachControl(this.PART_ToggleAutoAddRemoveThreads);
        this.currentConnectionTypeBinder.AttachControl(this.PART_ActiveConnectionTextBoxRO);
        this.isConsoleRunningBinder.AttachControl(this.PART_RunningState);
        this.PART_ThreadListBox.SelectionChanged += this.OnThreadListBoxSelectionChanged;
        this.PART_HexEditor.EffectiveViewportChanged += this.OnHexEditorViewportChanged;
        this.PART_HexEditor.AddHandler(PointerWheelChangedEvent, this.OnHexEditorMouseWheel, RoutingStrategies.Tunnel);

        this.PART_GotoTextBox.KeyDown += this.PART_GotoTextBoxOnKeyDown;
        this.PART_GotoTextBox.AcceptsReturn = false;
        this.PART_GotoTextBox.AcceptsTab = false;

        this.PART_HexEditor.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        HexView view = this.PART_HexEditor.HexView;
        view.CanVerticallyScroll = false;
        view.BytesPerLine = 16;
        view.Columns.Add(this.myOffsetColumn = new OffsetColumn());
        view.Columns.Add(new HexColumn());
        view.Columns.Add(new AsciiColumn());

        this.changeManager = new HexEditorChangeManager(this.PART_HexEditor);

        this.rldaUpdateMemoryDocument = RateLimitedDispatchActionBase.ForDispatcherSync(this.DoUpdateDocumentNow, TimeSpan.FromMilliseconds(100));

        this.timer = new MultiBrushFlipFlopTimer(TimeSpan.FromMilliseconds(500), [
            new BrushExchange(this.PART_RunningState, ForegroundProperty, SimpleIcons.DynamicForegroundBrush, new ConstantAvaloniaColourBrush(Brushes.Black)),
            new BrushExchange(this.PART_RunningState, BackgroundProperty, SimpleIcons.ConstantTransparentBrush, new ConstantAvaloniaColourBrush(Brushes.Yellow)),
        ]) { LevelChangesToStop = 7 /* stop on HIGH state */, StartHigh = true };

        this.myOffsetColumn.AdditionalOffset = 0x8303AA10;
        this.PART_GotoTextBox.Text = "8303AA10";

        this.rldaUpdateMemoryDocument.InvokeAsync();
    }

    private void DoUpdateDocumentNow() {
        int bytesPerLine = this.PART_HexEditor.HexView.ActualBytesPerLine;
        // int lineCount = this.PART_HexEditor.HexView.VisualLines.Count;
        int lineCount = (int) Math.Ceiling((this.PART_HexEditor.Bounds.Height + 22.0) / 14.0);
        int byteCount = bytesPerLine * lineCount;

        this.isDocumentFreshForChangeManager = true;
        MemoryBinaryDocument document = new MemoryBinaryDocument(new byte[byteCount], false);
        this.PART_HexEditor.Document = document;
        this.changeManager.OnDocumentChanged(document);
        this.UpdateAutoRefreshSpan((uint) this.myOffsetColumn.AdditionalOffset);
    }

    private void PART_GotoTextBoxOnKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key != Key.Enter)
            return;

        string? text = ((TextBox) sender!).Text;
        if (text != null && text.StartsWith("0x")) {
            text = text.Substring(2);
        }

        if (uint.TryParse(text, NumberStyles.HexNumber, null, out uint address)) {
            uint mod = address % 16;
            this.myOffsetColumn.AdditionalOffset = address - mod;
            this.DoUpdateDocumentNow();

            this.PART_HexEditor.ResetSelection();
            this.PART_HexEditor.Selection.Range = default;
            this.PART_HexEditor.Caret.Location = new BitLocation(mod);
            this.PART_HexEditor.Selection.Range = new BitRange(new BitLocation(mod), new BitLocation(mod + 1));
        }

        ((TextBox) sender).Text = this.myOffsetColumn.AdditionalOffset.ToString("X8");
    }

    static DebuggerWindow() {
        ConsoleDebuggerProperty.Changed.AddClassHandler<DebuggerWindow, ConsoleDebugger?>((s, e) => s.OnConsoleDebuggerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    protected override void OnOpenedCore() {
        base.OnOpenedCore();
        if (this.ConsoleDebugger != null) {
            this.ConsoleDebugger.IsWindowVisible = true;
            this.PART_EventViewer.BusyLock = this.ConsoleDebugger.BusyLock;
            this.PART_EventViewer.ConsoleConnection = this.ConsoleDebugger.Connection;
        }

        this.timer.EnableTargets();
    }

    protected sealed override async Task<bool> OnClosingAsync(WindowCloseReason reason) {
        if (await base.OnClosingAsync(reason))
            return true;

        this.timer.ClearTarget();

        ConsoleDebugger? debugger = this.ConsoleDebugger;
        this.ConsoleDebugger = null;

        if (debugger == null)
            return false;

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
                return false; // probably cannot cancel window closing here
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

        return false;
    }

    private void OnConsoleDebuggerChanged(ConsoleDebugger? oldValue, ConsoleDebugger? newValue) {
        if (oldValue != null) {
            oldValue.ConnectionChanged -= this.OnConsoleConnectionChanged;
            oldValue.ActiveThreadChanged -= this.OnActiveThreadChanged;
            oldValue.IsConsoleRunningChanged -= this.OnIsConsoleRunningChanged;
        }

        if (newValue != null) {
            newValue.ConnectionChanged += this.OnConsoleConnectionChanged;
            newValue.ActiveThreadChanged += this.OnActiveThreadChanged;
            newValue.IsConsoleRunningChanged += this.OnIsConsoleRunningChanged;
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

        DataManager.GetContextData(this).Set(ConsoleDebugger.DataKey, newValue);

        if (this.IsOpen) {
            this.PART_EventViewer.BusyLock = newValue?.BusyLock;
            this.PART_EventViewer.ConsoleConnection = newValue?.Connection;
        }

        this.RestartAutoRefresh();
    }

    private async void OnConsoleConnectionChanged(ConsoleDebugger sender, IConsoleConnection? oldconnection, IConsoleConnection? newconnection) {
        if (this.IsOpen) {
            this.PART_EventViewer.ConsoleConnection = newconnection;
            this.RestartAutoRefresh();
        }
    }

    private void RestartAutoRefresh() {
        this.autoRefresh?.Dispose();
        this.autoRefresh = null;

        if (this.ConsoleDebugger != null && this.ConsoleDebugger.Connection != null) {
            this.autoRefresh = new ThreadMemoryAutoRefresh(this.ConsoleDebugger, this);
            this.UpdateAutoRefreshSpan((uint) this.myOffsetColumn.AdditionalOffset);
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

    private void OnHexEditorMouseWheel(object? sender, PointerWheelEventArgs e) {
        if (!DoubleUtils.AreClose(e.Delta.Y, 0.0)) {
            const int LinesPerScroll = 3;
            const uint offsetBytes = LinesPerScroll * 16;

            long oldOffset = (long) this.myOffsetColumn.AdditionalOffset;
            long newOffset = Math.Max(0, oldOffset + (e.Delta.Y > 0 ? -offsetBytes : offsetBytes));

            BitLocation oldPos = this.PART_HexEditor.Caret.Location;
            this.newCaretByteIndex = (long) oldPos.ByteIndex + (oldOffset - newOffset);
            this.PART_HexEditor.Caret.Location = this.newCaretByteIndex < 0 ? default : new BitLocation((ulong) this.newCaretByteIndex, oldPos.BitIndex);

            BitRange oldRange = this.PART_HexEditor.Selection.Range;
            long rangeLength = (long) (oldRange.End.ByteIndex - oldRange.Start.ByteIndex);
            this.newSelectionByteIndex = (long) oldRange.Start.ByteIndex + (oldOffset - newOffset);
            this.PART_HexEditor.Selection.Range = new BitRange(
                new BitLocation((ulong) Math.Max(0, this.newSelectionByteIndex), oldRange.Start.BitIndex),
                new BitLocation((ulong) Math.Max(0, this.newSelectionByteIndex + rangeLength), oldRange.End.BitIndex));

            this.myOffsetColumn.AdditionalOffset = (ulong) newOffset;

            long change = (newOffset >= oldOffset ? (newOffset - oldOffset) : (oldOffset - newOffset));
            long byteOffsetChange = change * 16;

            this.UpdateAutoRefreshSpan((uint) newOffset);

            e.Handled = true;
        }
    }

    private void UpdateAutoRefreshSpan(uint startAddress) {
        if (this.autoRefresh != null) {
            int bytesPerLine = this.PART_HexEditor.HexView.ActualBytesPerLine;
            int lineCount = (int) Math.Ceiling((this.PART_HexEditor.Bounds.Height + 22.0) / 14.0);
            int byteCount = bytesPerLine * lineCount;

            this.autoRefresh.UpdateReadSpan(startAddress, (uint) byteCount);
        }
    }

    private void OnHexEditorViewportChanged(object? sender, EffectiveViewportChangedEventArgs e) {
        this.rldaUpdateMemoryDocument.InvokeAsync();
    }

    public void UpdateMemoryBuffer(ThreadMemoryAutoRefresh autoRefresher, byte[] buffer, uint addr, uint len) {
        if (this.autoRefresh != autoRefresher)
            return;

        MemoryBinaryDocument document = (MemoryBinaryDocument) this.PART_HexEditor.Document!;

        // Say we scroll down mid read: Offset = 8000003, addr = 80000000
        // So we want to show the buffer starting at Offset-addr
        // uint startAddress = (uint) this.myOffsetColumn.AdditionalOffset;
        // long offset = startAddress - addr;
        // if (offset < 0) {
        //     document.WriteBytes(0, new ReadOnlySpan<byte>(buffer, 0, Math.Min(buffer.Length, (int) document.Length)));
        // }

        int count = Math.Min(buffer.Length, (int) document.Length);
        if (this.isDocumentFreshForChangeManager) {
            this.isDocumentFreshForChangeManager = false;
        }
        else if (this.lastUpdateAddress == addr) {
            // long offset = (long) this.lastUpdateAddress - (long) addr;
            // this.changeManager.ProcessChanges((uint) Math.Max(offset, 0), buffer, (int) Math.Max(0, (count - offset)));
            this.changeManager.ProcessChanges(0, buffer, count);
        }

        document.WriteBytes(0, new ReadOnlySpan<byte>(buffer, 0, count));
        this.lastUpdateAddress = addr;
    }
}