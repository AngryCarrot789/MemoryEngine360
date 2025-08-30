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

using MemEngine360.Engine.Events;

namespace MemEngine360.Connections.Features;

public delegate void ConsoleSystemEventHandler(IConsoleConnection sender, ConsoleSystemEventArgs e);

/// <summary>
/// A feature for connections that can notify handlers of system event on the target console.
/// <para>
/// Note that a connection can be closed with subscribers still active, in which case,
/// once the connection is closed the events will not be published anymore. Handlers should
/// still dispose their subscription when the connection closes, to prevent possible memory leaks
/// </para>
/// </summary>
public interface IFeatureSystemEvents : IConsoleFeature {
    /// <summary>
    /// Adds the handler as a listener to this connection's system events.
    /// Disposing the returned object stops the handler receiving further notifications.
    /// <para>
    /// Some connections may require a background connection or thread to receive events, and once
    /// all handlers are removed, they may stop or pause the thread and close the connection. Therefore,
    /// it's important to always dispose the returned object when no longer required
    /// </para>
    /// <para>
    /// The event instance passed to the handler will always be a unique instance
    /// </para>
    /// </summary>
    /// <param name="handler">The handler of the system events. May be invoked from any thread</param>
    /// <returns>An object that unsubscribes when disposed</returns>
    IDisposable SubscribeToEvents(ConsoleSystemEventHandler handler);
}