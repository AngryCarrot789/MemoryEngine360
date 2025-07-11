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
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.Avalonia.Services.Windowing;

namespace MemEngine360.BaseFrontEnd.EventViewing;

public partial class ConsoleEventViewerWindow : DesktopWindow {
    public static readonly StyledProperty<MemoryEngine?> MemoryEngineProperty = AvaloniaProperty.Register<ConsoleEventViewerWindow, MemoryEngine?>(nameof(MemoryEngine));

    public MemoryEngine? MemoryEngine {
        get => this.GetValue(MemoryEngineProperty);
        set => this.SetValue(MemoryEngineProperty, value);
    }

    public ConsoleEventViewerWindow() {
        this.InitializeComponent();
    }

    static ConsoleEventViewerWindow() {
        MemoryEngineProperty.Changed.AddClassHandler<ConsoleEventViewerWindow, MemoryEngine?>((s, e) => s.OnMemoryEngineChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnMemoryEngineChanged(MemoryEngine? oldValue, MemoryEngine? newValue) {
        if (oldValue != null) oldValue.ConnectionChanged -= this.OnConsoleConnectionChanged;
        if (newValue != null) newValue.ConnectionChanged += this.OnConsoleConnectionChanged;

        if (this.IsOpen) {
            this.PART_EventViewer.BusyLock = newValue?.BusyLocker;
            this.PART_EventViewer.ConsoleConnection = newValue?.Connection;
        }
    }

    private void OnConsoleConnectionChanged(MemoryEngine sender, ulong frame, IConsoleConnection? oldconnection, IConsoleConnection? newconnection, ConnectionChangeCause cause) {
        if (this.IsOpen) {
            this.PART_EventViewer.ConsoleConnection = newconnection;
        }
    }

    protected override void OnOpenedCore() {
        base.OnOpenedCore();

        this.PART_EventViewer.BusyLock = this.MemoryEngine?.BusyLocker;
        this.PART_EventViewer.ConsoleConnection = this.MemoryEngine?.Connection;
    }

    protected override void OnClosed(EventArgs e) {
        base.OnClosed(e);
        this.PART_EventViewer.ConsoleConnection = null;
        this.PART_EventViewer.BusyLock = null;
        this.MemoryEngine = null;
    }
}

public class ConsoleEventViewerServiceImpl : IConsoleEventViewerService {
    private WeakReference<ConsoleEventViewerWindow>? currentWindow;

    public Task ShowOrFocus(MemoryEngine engine) {
        if (WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
            if (this.currentWindow == null || !this.currentWindow.TryGetTarget(out ConsoleEventViewerWindow? existing) || existing.IsClosed) {
                ConsoleEventViewerWindow window = new ConsoleEventViewerWindow() {
                    MemoryEngine = engine
                };

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