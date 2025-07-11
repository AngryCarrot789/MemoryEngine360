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
using Avalonia.Media;
using MemEngine360.Connections;
using MemEngine360.Engine.Debugging;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Avalonia.Themes.BrushFactories;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Logging;

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

    public DebuggerWindow() {
        this.InitializeComponent();
        this.autoRefreshBinder.AttachControl(this.PART_AutoRefreshRegistersOnThreadChange);
        this.autoAddRemoveThreadsBinder.AttachControl(this.PART_ToggleAutoAddRemoveThreads);
        this.currentConnectionTypeBinder.AttachControl(this.PART_ActiveConnectionTextBoxRO);
        this.isConsoleRunningBinder.AttachControl(this.PART_RunningState);
        this.PART_ThreadListBox.SelectionChanged += this.OnThreadListBoxSelectionChanged;

        this.timer = new MultiBrushFlipFlopTimer(TimeSpan.FromMilliseconds(500), [
            new BrushExchange(this.PART_RunningState, ForegroundProperty, SimpleIcons.DynamicForegroundBrush, new ConstantAvaloniaColourBrush(Brushes.Black)),
            new BrushExchange(this.PART_RunningState, BackgroundProperty, SimpleIcons.ConstantTransparentBrush, new ConstantAvaloniaColourBrush(Brushes.Yellow)),
        ]) { LevelChangesToStop = 7 /* stop on HIGH state */, StartHigh = true };
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
        if (await base.OnClosingAsync(reason)) return true;

        this.timer.ClearTarget();

        ConsoleDebugger? debugger = this.ConsoleDebugger;
        this.ConsoleDebugger = null;

        if (debugger == null)
            return false;

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
                    await connection.Close();
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
    }

    private void OnConsoleConnectionChanged(ConsoleDebugger sender, IConsoleConnection? oldconnection, IConsoleConnection? newconnection) {
        if (this.IsOpen) {
            this.PART_EventViewer.ConsoleConnection = newconnection;
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
}