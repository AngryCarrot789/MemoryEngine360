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
using Avalonia.Controls;
using MemEngine360.Connections;
using MemEngine360.Connections.Traits;
using MemEngine360.Engine;
using MemEngine360.Engine.Events;
using MemEngine360.Engine.Events.XbdmEvents;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.BaseFrontEnd.EventViewing;

public partial class ConsoleEventViewerWindow : DesktopWindow {
    public MemoryEngine Engine { get; }

    private IDisposable? subscription;
    private readonly ConcurrentQueue<ConsoleSystemEventArgs> pendingInsertion;
    private readonly RateLimitedDispatchAction rldaInsertEvents;
    private volatile int isClosedState;

    [Obsolete("Do not use")]
    public ConsoleEventViewerWindow() : this(null) {
    }

    public ConsoleEventViewerWindow(MemoryEngine engine) {
        this.InitializeComponent();
        this.Engine = engine;
        this.pendingInsertion = new ConcurrentQueue<ConsoleSystemEventArgs>();
        this.rldaInsertEvents = RateLimitedDispatchActionBase.ForDispatcherSync(() => {
            List<ConsoleSystemEventArgs> items = new List<ConsoleSystemEventArgs>(20);
            for (int i = 0; i < 20 && this.pendingInsertion.TryDequeue(out ConsoleSystemEventArgs? result); i++) {
                items.Add(result);
            }

            if (items.Count < 1) {
                return;
            }

            int count = this.PART_List.Children.Count;
            if (count + items.Count > 1000) {
                Debug.Assert(count >= 50);
                this.PART_List.Children.RemoveRange(0, 50);
            }

            this.PART_List.Children.AddRange(items.Select(e => new TextBlock() { Text = (e as XbdmEventArgs)?.RawMessage ?? e.ToString() }));
            if (this.PART_AutoScroll.IsChecked == true && Math.Abs(this.PART_ScrollViewer.Offset.Y - this.PART_ScrollViewer.ScrollBarMaximum.Y) < 0.1D) {
                this.PART_ScrollViewer.ScrollToEnd();
            }

            if (!this.pendingInsertion.IsEmpty) {
                this.rldaInsertEvents!.InvokeAsync();
            }
        }, TimeSpan.FromMilliseconds(100));
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