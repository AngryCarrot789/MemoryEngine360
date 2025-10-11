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
using Avalonia.Interactivity;
using Avalonia.Media;
using MemEngine360.BaseFrontEnd.EventViewing.XbdmEvents;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using MemEngine360.Engine;
using MemEngine360.Engine.Events;
using MemEngine360.Engine.Events.XbdmEvents;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Themes.BrushFactories;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Destroying;
using PFXToolKitUI.Utils.RDA;
using VisualExtensions = Avalonia.VisualTree.VisualExtensions;

namespace MemEngine360.BaseFrontEnd.EventViewing;

public partial class ConsoleEventViewerView : UserControl {
    public static readonly ModelTypeControlRegistry<Control> EventTypeToDisplayControlRegistry;

    public static readonly StyledProperty<IConsoleConnection?> ConsoleConnectionProperty = AvaloniaProperty.Register<ConsoleEventViewerView, IConsoleConnection?>(nameof(ConsoleConnection));
    public static readonly StyledProperty<BusyLock?> BusyLockProperty = AvaloniaProperty.Register<ConsoleEventViewerView, BusyLock?>(nameof(BusyLock));

    public IConsoleConnection? ConsoleConnection {
        get => this.GetValue(ConsoleConnectionProperty);
        set => this.SetValue(ConsoleConnectionProperty, value);
    }
    
    public BusyLock? BusyLock {
        get => this.GetValue(BusyLockProperty);
        set => this.SetValue(BusyLockProperty, value);
    }

    private IDisposable? subscription;
    private readonly LambdaConnectionLockPair connectionLockPair;

    // Using TryDequeue in a loop while the BG reader thread is going nuts on the events it's receiving (e.g. 1000s of exceptions per second)
    // actually stalls the UI thread pretty damn heavily. Therefore, we use a queue + lock to take X amount of items, and dispatch to UI thread 
    // private readonly ConcurrentQueue<ConsoleSystemEventArgs> pendingInsertion;
    private readonly Queue<ConsoleSystemEventArgs> pendingInsertionEx;
    private volatile int pendingInsertionCount;
    private readonly RateLimitedDispatchAction rldaInsertEvents;
    private volatile int isUnloadedState;

    private readonly Dictionary<Type, Control> eventDisplayControlCache;
    private readonly ObservableList<ConsoleSystemEventArgs> myEvents;
    private ScrollViewer? PART_ScrollViewer;

    private int lastStatusMessageType = -1;
    private bool isScrolledToBottomOfList;
    private readonly BrushFlipFlopTimer statusAlarmTimer;

    static ConsoleEventViewerView() {
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
        ConsoleConnectionProperty.Changed.AddClassHandler<ConsoleEventViewerView, IConsoleConnection?>((s, e) => s.OnConsoleConnectionChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }
    
    public ConsoleEventViewerView() {
        this.InitializeComponent();
        this.connectionLockPair = new LambdaConnectionLockPair(() => this.BusyLock ?? throw new InvalidOperationException("Not ready yet"), () => this.ConsoleConnection);
        this.pendingInsertionEx = new Queue<ConsoleSystemEventArgs>(1024);
        this.eventDisplayControlCache = new Dictionary<Type, Control>();
        this.myEvents = new ObservableList<ConsoleSystemEventArgs>();
        this.PART_EventListBox.SetItemsSource(this.myEvents);

        this.rldaInsertEvents = new RateLimitedDispatchAction(this.OnTickInsertEventsCallback, TimeSpan.FromMilliseconds(50)) { DebugName = nameof(ConsoleEventViewerView) };
        this.PART_EventListBox.SelectionChanged += this.OnSelectedItemChanged;

        this.statusAlarmTimer = new BrushFlipFlopTimer(TimeSpan.FromMilliseconds(250), StandardIcons.ForegroundBrush, new ConstantAvaloniaColourBrush(Brushes.Red));
    }
    
    private void OnConsoleConnectionChanged(IConsoleConnection? oldConnection, IConsoleConnection? newConnection) {
        DisposableUtils.Dispose(ref this.subscription);
        if (newConnection?.TryGetFeature(out IFeatureSystemEvents? events) == true) {
            this.subscription = events.SubscribeToEvents(this.OnEvent);
        }
    }
    
    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);
        this.isUnloadedState = 0;
        if (this.PART_ScrollViewer == null) {
            this.PART_ScrollViewer = VisualExtensions.FindDescendantOfType<ScrollViewer>(this.PART_EventListBox);
            if (this.PART_ScrollViewer != null) {
                this.PART_ScrollViewer.LayoutUpdated += this.PART_ScrollViewerOnLayoutUpdated;
                this.PART_ScrollViewer.PropertyChanged += this.PART_ScrollViewerOnPropertyChanged;
                this.PART_ScrollViewer.ScrollChanged += this.PART_ScrollViewerOnScrollChanged;
            }
        }

        this.statusAlarmTimer.SetTarget(this.PART_Status, TextBlock.ForegroundProperty);
        
        // ConsoleConnection may change after OnLoaded
        if (this.subscription == null && this.ConsoleConnection?.TryGetFeature(out IFeatureSystemEvents? events) == true) {
            this.subscription = events.SubscribeToEvents(this.OnEvent);
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e) {
        base.OnUnloaded(e);

        this.isUnloadedState = 1;
        DisposableUtils.Dispose(ref this.subscription);
        this.statusAlarmTimer.ClearTarget();
        // this.myEvents.Clear();
        // lock (this.pendingInsertionEx) {
        //     this.pendingInsertionCount = 0;
        //     this.pendingInsertionEx.Clear();
        // }
    }

    private void PART_ScrollViewerOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
        if (e.Property == ScrollViewer.ExtentProperty) {
            ScrollViewer sv = (ScrollViewer) sender!;
            AvaloniaPropertyChangedEventArgs<Size> args = (AvaloniaPropertyChangedEventArgs<Size>) e;
            Size oldExtent = args.OldValue.HasValue ? args.OldValue.Value : sv.Extent;
            this.isScrolledToBottomOfList = Math.Abs(sv.Offset.Y - Math.Max(oldExtent.Height - sv.Viewport.Height, 0)) < 20;
        }
    }

    private void PART_ScrollViewerOnScrollChanged(object? sender, ScrollChangedEventArgs e) {
        ScrollViewer sv = (ScrollViewer) sender!;
        this.isScrolledToBottomOfList = Math.Abs(sv.Offset.Y - Math.Max(sv.Extent.Height - sv.Viewport.Height, 0)) < 20;
    }

    private void PART_ScrollViewerOnLayoutUpdated(object? sender, EventArgs e) {
        ScrollViewer sv = (ScrollViewer) sender!;
        if (this.isScrolledToBottomOfList && this.PART_AutoScroll.IsEnabled) {
            // scroll to end, without affecting horizontal offset
            sv.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(sv.Offset.X, double.PositiveInfinity));
        }
    }

    private void OnSelectedItemChanged(object? sender, SelectionChangedEventArgs e) => this.OnSelectedLineChanged();

    private void SetStatusText(string? text, bool isWarning) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();

        this.PART_Status.Text = text;
        this.statusAlarmTimer.IsEnabled = isWarning && !string.IsNullOrWhiteSpace(text);
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

            if ((this.myEvents.Count + events.Count) > 1000) {
                const int RemoveAmount = 500;
                this.myEvents.RemoveRange(0, RemoveAmount);
            }

            this.myEvents.AddRange(events); /* insert items into INotifyCollectionChanged */
            this.PART_EventListBox.UpdateLayout();
            if (this.PART_AutoScroll.IsChecked == true && this.isScrolledToBottomOfList) {
                ScrollViewer sv = this.PART_ScrollViewer!;
                // scroll to end, without affecting horizontal offset
                sv.UpdateLayout();
                sv.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(sv.Offset.X, double.PositiveInfinity));
            }
            else {
            }
        }, DispatchPriority.Default);

        if (this.pendingInsertionCount > 0) {
            this.rldaInsertEvents.InvokeAsync();
        }
    }

    private void OnSelectedLineChanged() {
        object? oldContent = this.PART_EventInfoContent.Content;
        if (oldContent is IConsoleEventArgsInfoControl oldControl)
            oldControl.Disconnect();
        this.PART_EventInfoContent.Content = null;

        ConsoleSystemEventArgs? selectedLine = this.PART_EventListBox.SelectedModel;
        if (selectedLine != null) {
            Debug.Assert(this.ConsoleConnection != null && this.BusyLock != null);

            if (!this.eventDisplayControlCache.TryGetValue(selectedLine.GetType(), out Control? control)) {
                if (!EventTypeToDisplayControlRegistry.TryGetNewInstance(selectedLine.GetType(), out control)) {
                    return;
                }

                this.eventDisplayControlCache[selectedLine.GetType()] = control;
            }

            this.PART_EventInfoContent.Content = control;
            if (control is IConsoleEventArgsInfoControl newControl) {
                newControl.Connect(this.connectionLockPair, selectedLine);
            }
        }
    }

    private void OnEvent(IConsoleConnection sender, ConsoleSystemEventArgs e) {
        if (this.isUnloadedState != 0 || this.pendingInsertionCount >= 50000) {
            return;
        }

        lock (this.pendingInsertionEx) {
            if (this.isUnloadedState == 0 && this.pendingInsertionCount < 50000) {
                this.pendingInsertionEx.Enqueue(e);
                this.rldaInsertEvents.InvokeAsync();
                Interlocked.Increment(ref this.pendingInsertionCount);
            }
        }
    }
}