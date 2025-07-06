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

using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using MemEngine360.BaseFrontEnd.EventViewing.XbdmEvents;
using MemEngine360.Connections;
using MemEngine360.Connections.Traits;
using MemEngine360.Engine;
using MemEngine360.Engine.Events;
using MemEngine360.Engine.Events.XbdmEvents;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.BaseFrontEnd.EventViewing;

public partial class ConsoleEventViewerWindow : DesktopWindow {
    public static readonly ModelTypeControlRegistry<Control> EventTypeToDisplayControlRegistry;

    public static readonly AttachedProperty<bool> IsLineSelectedProperty = AvaloniaProperty.RegisterAttached<ConsoleEventViewerWindow, TextBlock, bool>("IsLineSelected");

    public MemoryEngine Engine { get; }

    private IDisposable? subscription;
    private readonly ConcurrentQueue<ConsoleSystemEventArgs> pendingInsertion;
    private readonly RateLimitedDispatchAction rldaInsertEvents;
    private volatile int isClosedState;

    private TextBlock? selectedLine;
    private readonly WeakReference debugWeakRefTestMemoryLeak;
    private readonly Dictionary<Type, Control> eventDisplayControlCache;

    static ConsoleEventViewerWindow() {
        EventTypeToDisplayControlRegistry = new ModelTypeControlRegistry<Control>();
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgs), () => new ConsoleEventArgsInfoControlXbdmEvent());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsAssert), () => new ConsoleEventArgsInfoControlXbdmEventAssert());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsBreakpoint), () => new TextBlock(){Text = "XbdmEventArgsBreakpoint"});//() => new ConsoleEventArgsInfoControlXbdmEventBreakpoint());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsDebugString), () => new ConsoleEventArgsInfoControlXbdmEventDebugString());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsException), () => new TextBlock(){Text = "XbdmEventArgsException"});//() => new ConsoleEventArgsInfoControlXbdmEventException());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsExecutionState), () => new TextBlock(){Text = "XbdmEventArgsExecutionState"});//() => new ConsoleEventArgsInfoControlXbdmEventExecutionState());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsExternal), () => new TextBlock(){Text = "XbdmEventArgsExternal"});//() => new ConsoleEventArgsInfoControlXbdmEventExternal());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsNotification), () => new TextBlock(){Text = "XbdmEventArgsNotification"});//() => new ConsoleEventArgsInfoControlXbdmEventNotification());
        EventTypeToDisplayControlRegistry.RegisterType(typeof(XbdmEventArgsRip), () => new TextBlock(){Text = "XbdmEventArgsRip"});//() => new ConsoleEventArgsInfoControlXbdmEventRip());
    }

    [Obsolete("Do not use")]
    public ConsoleEventViewerWindow() : this(new MemoryEngine()) {
    }

    public ConsoleEventViewerWindow(MemoryEngine engine) {
        this.InitializeComponent();
        this.Engine = engine;
        this.pendingInsertion = new ConcurrentQueue<ConsoleSystemEventArgs>();
        this.eventDisplayControlCache = new Dictionary<Type, Control>();
        TextBlock testMemoryLeakTB = new TextBlock();
        testMemoryLeakTB.PointerPressed += this.OnTextBlockPressed;
        this.debugWeakRefTestMemoryLeak = new WeakReference(testMemoryLeakTB);

        this.rldaInsertEvents = RateLimitedDispatchActionBase.ForDispatcherSync(() => {
            List<TextBlock> items = new List<TextBlock>(20);
            for (int i = 0; i < 20 && this.pendingInsertion.TryDequeue(out ConsoleSystemEventArgs? result); i++) {
                TextBlock tb = new TextBlock() {
                    Text = (result as XbdmEventArgs)?.RawMessage ?? result.ToString(),
                    Tag = result
                };

                tb.PointerPressed += this.OnTextBlockPressed;
                items.Add(tb);
            }

            if (items.Count < 1) {
                return;
            }

            int count = this.PART_List.Children.Count;
            if (count + items.Count > 1000) {
                Debug.Assert(count >= 50);
                this.PART_List.Children.RemoveRange(0, 50);

                // TODO: maybe move the selection to first item in list instead of clearing?
                if (this.selectedLine != null && this.selectedLine.GetLogicalParent() == null) {
                    Debug.Assert(GetIsLineSelected(this.selectedLine));
                    SetIsLineSelected(this.selectedLine, false);
                    this.selectedLine = null;
                    this.OnSelectedLineChanged();
                }
            }

            this.PART_List.Children.AddRange(items);
            if (this.PART_AutoScroll.IsChecked == true) {
                bool isScrolledToBottom = Math.Abs(this.PART_ScrollViewer.Offset.Y - this.PART_ScrollViewer.ScrollBarMaximum.Y) < 0.1D;
                if (isScrolledToBottom)
                    this.PART_ScrollViewer.ScrollToEnd();
            }

            if (!this.pendingInsertion.IsEmpty) {
                this.rldaInsertEvents!.InvokeAsync();
            }
        }, TimeSpan.FromMilliseconds(100));
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
    }

    protected override void OnClosed(EventArgs e) {
        base.OnClosed(e);
        this.isClosedState = 1;

        this.Engine.ConnectionChanged -= this.OnEngineConnectionChanged;
        this.subscription?.Dispose();

        this.PART_List.Children.Clear();
        if (this.selectedLine != null) {
            Debug.Assert(GetIsLineSelected(this.selectedLine));
            SetIsLineSelected(this.selectedLine, false);
            this.selectedLine = null;
            this.OnSelectedLineChanged();
        }

        this.pendingInsertion.Clear();
    }

    private void OnEngineConnectionChanged(MemoryEngine sender, ulong frame, IConsoleConnection? oldConn, IConsoleConnection? newConn, ConnectionChangeCause cause) {
        this.subscription?.Dispose();
        if (newConn is IHaveSystemEvents events) {
            this.subscription = events.SubscribeToEvents(this.OnEvent);
        }
    }

    private void OnEvent(IConsoleConnection sender, ConsoleSystemEventArgs e) {
        if (this.isClosedState == 0) {
            this.pendingInsertion.Enqueue(e);
            this.rldaInsertEvents.InvokeAsync();
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