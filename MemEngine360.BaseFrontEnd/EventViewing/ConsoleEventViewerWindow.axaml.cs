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
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using MemEngine360.BaseFrontEnd.EventViewing.XbdmEvents;
using MemEngine360.Connections;
using MemEngine360.Connections.Traits;
using MemEngine360.Engine;
using MemEngine360.Engine.Events;
using MemEngine360.Engine.Events.XbdmEvents;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Avalonia.Themes.BrushFactories;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.BaseFrontEnd.EventViewing;

public partial class ConsoleEventViewerWindow : DesktopWindow {
    public static readonly ModelTypeControlRegistry<Control> EventTypeToDisplayControlRegistry;

    public static readonly AttachedProperty<bool> IsLineSelectedProperty = AvaloniaProperty.RegisterAttached<ConsoleEventViewerWindow, TextBlock, bool>("IsLineSelected");

    public MemoryEngine Engine { get; }

    private IDisposable? subscription;

    // Using TryDequeue in a loop while the BG reader thread is going nuts on the events it's receiving (e.g. 1000s of exceptions per second)
    // actually stalls the UI thread pretty damn heavily. Therefore, we use a queue + lock to take X amount of items, and dispatch to UI thread 
    // private readonly ConcurrentQueue<ConsoleSystemEventArgs> pendingInsertion;
    private readonly Queue<ConsoleSystemEventArgs> pendingInsertionEx;
    private volatile int pendingInsertionCount;
    private readonly RateLimitedDispatchAction rldaInsertEvents;
    private volatile int isClosedState;

    private TextBlock? selectedLine;
    private readonly WeakReference debugWeakRefTestMemoryLeak;
    private readonly Dictionary<Type, Control> eventDisplayControlCache;
    private readonly Stack<TextBlock> cachedTextBlocks;

    private DispatcherTimer? flashingTextTimer;
    private int flashingIndex = -1;
    private readonly DynamicAvaloniaColourBrush PFXForegroundBrush;
    private IDisposable? foregroundSubscription;
    private int lastStatusMessageType = -1;

    static ConsoleEventViewerWindow() {
        EventTypeToDisplayControlRegistry = new ModelTypeControlRegistry<Control>();
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgs), () => new ConsoleEventArgsInfoControlXbdmEvent());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsAssert), () => new ConsoleEventArgsInfoControlXbdmEventAssert());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsBreakpoint), () => new TextBlock() { Text = "XbdmEventArgsBreakpoint" }); //() => new ConsoleEventArgsInfoControlXbdmEventBreakpoint());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsDebugString), () => new ConsoleEventArgsInfoControlXbdmEventDebugString());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsException), () => new TextBlock() { Text = "XbdmEventArgsException" }); //() => new ConsoleEventArgsInfoControlXbdmEventException());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsExecutionState), () => new TextBlock() { Text = "XbdmEventArgsExecutionState" }); //() => new ConsoleEventArgsInfoControlXbdmEventExecutionState());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsExternal), () => new TextBlock() { Text = "XbdmEventArgsExternal" }); //() => new ConsoleEventArgsInfoControlXbdmEventExternal());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsNotification), () => new TextBlock() { Text = "XbdmEventArgsNotification" }); //() => new ConsoleEventArgsInfoControlXbdmEventNotification());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsRip), () => new TextBlock() { Text = "XbdmEventArgsRip" }); //() => new ConsoleEventArgsInfoControlXbdmEventRip());
    }

    [Obsolete("Do not use")]
    public ConsoleEventViewerWindow() : this(new MemoryEngine()) {
    }

    public ConsoleEventViewerWindow(MemoryEngine engine) {
        this.InitializeComponent();
        this.Engine = engine;
        this.pendingInsertionEx = new Queue<ConsoleSystemEventArgs>(1024);
        this.eventDisplayControlCache = new Dictionary<Type, Control>();
        this.cachedTextBlocks = new Stack<TextBlock>(510);
        this.PFXForegroundBrush = (DynamicAvaloniaColourBrush) SimpleIcons.DynamicForegroundBrush;

        TextBlock testMemoryLeakTB = new TextBlock();
        testMemoryLeakTB.PointerPressed += this.OnTextBlockPressed;
        this.debugWeakRefTestMemoryLeak = new WeakReference(testMemoryLeakTB);

        this.rldaInsertEvents = new RateLimitedDispatchAction(this.OnTickInsertEventsCallback, TimeSpan.FromMilliseconds(50)) { DebugName = nameof(ConsoleEventViewerWindow) };
    }

    private void SetStatusText(string? text, bool isWarning) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();

        this.PART_Status.Text = text;
        if (!string.IsNullOrWhiteSpace(text) /* no need to flash when invisible */ && isWarning) {
            if (this.flashingTextTimer == null || !this.flashingTextTimer.IsEnabled) {
                this.flashingTextTimer ??= new DispatcherTimer(
                    TimeSpan.FromMilliseconds(250) /* full flash loop every 0.5 secs */,
                    DispatcherPriority.Normal,
                    this.OnTickFlashText);
                this.flashingTextTimer.Start();
            }

            if (this.flashingIndex == -1)
                this.flashingIndex = 0;

            this.UpdateStatusTextForeground();
        }
        else {
            this.flashingTextTimer?.Stop();
            this.flashingIndex = -1;
            this.UpdateStatusTextForeground();
        }
    }

    private void OnTickFlashText(object? sender, EventArgs e) {
        if (this.flashingIndex != -1) {
            this.flashingIndex = this.flashingIndex == 0 ? 1 : 0;
            this.UpdateStatusTextForeground();
        }
    }

    private void UpdateStatusTextForeground() {
        // CurrentBrush is updated by the subscription, added/removed when the window opens/closes
        this.PART_Status.Foreground = this.flashingIndex > 0 ? Brushes.Red : this.PFXForegroundBrush.CurrentBrush;
    }

    private async Task OnTickInsertEventsCallback() {
        List<ConsoleSystemEventArgs> events = await Task.Run(() => {
            const int InsertionCount = 20;
            List<ConsoleSystemEventArgs> list = new List<ConsoleSystemEventArgs>(InsertionCount);
            lock (this.pendingInsertionEx) {
                for (int i = 0; i < InsertionCount && this.pendingInsertionEx.TryDequeue(out ConsoleSystemEventArgs? result); i++) {
                    list.Add(result);
                }
            }

            Interlocked.Add(ref this.pendingInsertionCount, -list.Count);
            return list;
        });

        if (events.Count < 1) {
            return;
        }

        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
            int ec = this.pendingInsertionCount;
            if ((ec + events.Count) >= 50000) {
                if (this.lastStatusMessageType != 0) {
                    this.lastStatusMessageType = 0;
                    this.SetStatusText($"Ignoring incoming events: +50,000 currently queued", true);
                }
            }
            else if ((ec + events.Count) >= 10000) {
                this.SetStatusText($"More than 10,000 events queued! (+{ec})", false);
            }
            else if ((ec + events.Count) < 5000) {
                if (this.lastStatusMessageType != -1) {
                    this.lastStatusMessageType = -1;
                    this.SetStatusText(null, false);
                }
            }

            List<TextBlock> items = new List<TextBlock>(events.Count);
            foreach (ConsoleSystemEventArgs eventArgs in events) {
                if (!this.cachedTextBlocks.TryPop(out TextBlock? tb)) {
                    tb = new TextBlock();
                    tb.PointerPressed += this.OnTextBlockPressed;
                }

                string msg = (eventArgs as XbdmEventArgs)?.RawMessage ?? eventArgs.ToString() ?? "";
                string? tbMsg = tb.Text;

                // Avoid updating text if it's the same.
                if ((string.IsNullOrWhiteSpace(tbMsg) && !string.IsNullOrWhiteSpace(msg)) || msg != tbMsg) {
                    tb.Text = msg;
                }

                tb.Tag = eventArgs;
                items.Add(tb);
            }

            Controls listTextBlocks = this.PART_List.Children;
            if ((listTextBlocks.Count + items.Count) > 1000) {
                const int RemoveAmount = 500;
                if (this.selectedLine != null) {
                    // TODO: maybe move the selection to first item in list instead of clearing?
                    ILogical? parent = this.selectedLine.GetLogicalParent();
                    Debug.Assert(parent != null);

                    int index = listTextBlocks.IndexOf(this.selectedLine);
                    Debug.Assert(index >= 0);

                    if (index < RemoveAmount) {
                        Debug.Assert(GetIsLineSelected(this.selectedLine));
                        SetIsLineSelected(this.selectedLine, false);
                        this.selectedLine = null;
                        this.OnSelectedLineChanged();
                    }
                }

                for (int i = 0; i < RemoveAmount; i++) {
                    this.cachedTextBlocks.Push((TextBlock) listTextBlocks[i]);
                }

                listTextBlocks.RemoveRange(0, RemoveAmount);
            }

            listTextBlocks.AddRange(items);
            if (this.PART_AutoScroll.IsChecked == true) {
                bool isScrolledToBottom = Math.Abs(this.PART_ScrollViewer.Offset.Y - this.PART_ScrollViewer.ScrollBarMaximum.Y) < 0.1D;
                if (isScrolledToBottom)
                    this.PART_ScrollViewer.ScrollToEnd();
            }
        }, DispatchPriority.Default);

        if (this.pendingInsertionCount > 0) {
            this.rldaInsertEvents.InvokeAsync();
        }
    }

    private void OnTextBlockPressed(object? sender, PointerPressedEventArgs e) {
        if (this.selectedLine != null) {
            if (ReferenceEquals(sender, this.selectedLine)) {
                return; // same line so ignore change
            }

            Debug.Assert(GetIsLineSelected(this.selectedLine));
            SetIsLineSelected(this.selectedLine, false);
        }

        this.selectedLine = (TextBlock) sender!;
        SetIsLineSelected(this.selectedLine, true);
        this.OnSelectedLineChanged();
    }

    private void OnSelectedLineChanged() {
        object? oldContent = this.PART_EventInfoContent.Content;
        if (oldContent is IConsoleEventArgsInfoControl oldControl)
            oldControl.Disconnect();
        this.PART_EventInfoContent.Content = null;

        if (this.selectedLine != null) {
            ConsoleSystemEventArgs eventArgs = (ConsoleSystemEventArgs) this.selectedLine.Tag!;
            if (!this.eventDisplayControlCache.TryGetValue(eventArgs.GetType(), out Control? control)) {
                if (!EventTypeToDisplayControlRegistry.TryGetNewInstance(eventArgs.GetType(), out control)) {
                    return;
                }

                this.eventDisplayControlCache[eventArgs.GetType()] = control;
            }

            this.PART_EventInfoContent.Content = control;
            if (control is IConsoleEventArgsInfoControl newControl) {
                newControl.Connect(this.Engine, eventArgs);
            }
        }
    }

    protected override void OnOpenedCore() {
        base.OnOpenedCore();

        this.Engine.ConnectionChanged += this.OnEngineConnectionChanged;
        if (this.Engine.Connection is IHaveSystemEvents events) {
            this.subscription = events.SubscribeToEvents(this.OnEvent);
        }

        this.foregroundSubscription = this.PFXForegroundBrush.Subscribe(this.OnPFXForegroundBrushChanged);
    }

    private void OnPFXForegroundBrushChanged(IBrush? obj) => this.UpdateStatusTextForeground();

    protected override void OnClosed(EventArgs e) {
        base.OnClosed(e);
        this.isClosedState = 1;

        this.Engine.ConnectionChanged -= this.OnEngineConnectionChanged;
        this.subscription?.Dispose();

        this.foregroundSubscription?.Dispose();

        this.PART_List.Children.Clear();
        if (this.selectedLine != null) {
            Debug.Assert(GetIsLineSelected(this.selectedLine));
            SetIsLineSelected(this.selectedLine, false);
            this.selectedLine = null;
            this.OnSelectedLineChanged();
        }

        lock (this.pendingInsertionEx) {
            this.pendingInsertionCount = 0;
            this.pendingInsertionEx.Clear();
        }
    }

    private void OnEngineConnectionChanged(MemoryEngine sender, ulong frame, IConsoleConnection? oldConn, IConsoleConnection? newConn, ConnectionChangeCause cause) {
        this.subscription?.Dispose();
        if (newConn is IHaveSystemEvents events) {
            this.subscription = events.SubscribeToEvents(this.OnEvent);
        }
    }

    private void OnEvent(IConsoleConnection sender, ConsoleSystemEventArgs e) {
        if (this.isClosedState != 0 || this.pendingInsertionCount >= 50000) {
            return;
        }

        lock (this.pendingInsertionEx) {
            if (this.isClosedState == 0 && this.pendingInsertionCount < 50000) {
                this.pendingInsertionEx.Enqueue(e);
                this.rldaInsertEvents.InvokeAsync();
                Interlocked.Increment(ref this.pendingInsertionCount);
            }
        }
    }

    public static void SetIsLineSelected(TextBlock obj, bool value) => obj.SetValue(IsLineSelectedProperty, value);
    public static bool GetIsLineSelected(TextBlock obj) => obj.GetValue(IsLineSelectedProperty);
}

public class ConsoleEventViewerServiceImpl : IConsoleEventViewerService {
    private WeakReference<ConsoleEventViewerWindow>? currentWindow;

    public Task ShowOrFocus(MemoryEngine engine) {
        if (WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
            if (this.currentWindow == null || !this.currentWindow.TryGetTarget(out ConsoleEventViewerWindow? existing) || existing.IsClosed) {
                ConsoleEventViewerWindow window = new ConsoleEventViewerWindow(engine);

                if (this.currentWindow == null)
                    this.currentWindow = new WeakReference<ConsoleEventViewerWindow>(window);
                else
                    this.currentWindow.SetTarget(window);

                system.Register(window).Show();
            }
            else {
                existing.Activate();
            }
        }

        return Task.CompletedTask;
    }
}