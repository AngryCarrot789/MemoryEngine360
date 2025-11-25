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

using MemEngine360.Connections;
using PFXToolKitUI;
using PFXToolKitUI.Notifications;

namespace MemEngine360.Engine;

/// <summary>
/// Manages memory engine instances
/// </summary>
public abstract class MemoryEngineManager {
    private readonly List<MemoryEngine> engines;

    public static MemoryEngineManager Instance => ApplicationPFX.GetComponent<MemoryEngineManager>();

    /// <summary>
    /// Gets all opened engine views. This list is read-only
    /// </summary>
    public IList<MemoryEngine> Engines { get; }

    /// <summary>
    /// A global event fired when any mem engine view opens
    /// </summary>
    public event EventHandler<MemoryEngine>? EngineOpened;

    /// <summary>
    /// A global event fired when any mem engine view closes
    /// </summary>
    public event EventHandler<MemoryEngine>? EngineClosed;

    /// <summary>
    /// A custom handler for when the engine's main connection changes and the notification saying "Connected" is shown.
    /// This allows for adding custom actions to the notification.
    /// <para>
    /// This event is invoked before the notification is actually shown
    /// </para>
    /// </summary>
    public event EventHandler<ProvidePostConnectionActionsEventArgs>? ProvidePostConnectionActions;

    public MemoryEngineManager() {
        this.Engines = (this.engines = new List<MemoryEngine>(1)).AsReadOnly();
    }

    protected void OnEngineOpened(MemoryEngine engineUI) {
        if (this.engines.Contains(engineUI))
            throw new InvalidOperationException("Engine already opened");

        this.engines.Add(engineUI);
        this.EngineOpened?.Invoke(this, engineUI);
    }

    protected void OnEngineClosed(MemoryEngine engineUI) {
        if (!this.engines.Remove(engineUI))
            throw new InvalidOperationException("Engine not opened");

        this.EngineClosed?.Invoke(this, engineUI);
    }

    public void RaiseProvidePostConnectionActions(MemoryEngine engine, IConsoleConnection connection, Notification notification) {
        this.ProvidePostConnectionActions?.Invoke(this, new ProvidePostConnectionActionsEventArgs(engine, connection, notification));
    }
}

public readonly struct ProvidePostConnectionActionsEventArgs(MemoryEngine memoryEngine, IConsoleConnection connection, Notification notification) {
    public MemoryEngine MemoryEngine { get; } = memoryEngine;
    public IConsoleConnection Connection { get; } = connection;
    public Notification Notification { get; } = notification;
}