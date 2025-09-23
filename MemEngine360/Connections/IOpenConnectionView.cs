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

using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Connections;

/// <summary>
/// An interface for an open connection window
/// </summary>
public interface IOpenConnectionView {
    /// <summary>
    /// Gets the data key used to access the <see cref="IOpenConnectionView"/> from context data.
    /// </summary>
    static readonly DataKey<IOpenConnectionView> DataKey = DataKeys.Create<IOpenConnectionView>(nameof(IOpenConnectionView));
    
    /// <summary>
    /// Used to check if an attempt to open a connection is actually made from a <see cref="IOpenConnectionView"/>.
    /// <para>
    /// If the user clicks the "Reconnect" button in the notification in the engine's window when the connection is lost,
    /// then this key will either not be present or be false in the context data provided to <see cref="RegisteredConnectionType.OpenConnection"/>
    /// </para>
    /// </summary>
    static readonly DataKey<bool> IsConnectingFromViewDataKey = DataKeys.Create<bool>(nameof(IOpenConnectionView) + "_IsConnectingFromView");
    
    /// <summary>
    /// Returns true when this window was closed by the user clicking the close button, cancel, or it closed mysteriously (e.g. app or OS shutdown)
    /// </summary>
    bool IsWindowOpen { get; }

    /// <summary>
    /// Gets the <see cref="UserConnectionInfo"/> that was used to configure the UI to open the <see cref="IConsoleConnection"/>
    /// </summary>
    UserConnectionInfo? UserConnectionInfoForConnection { get; }

    /// <summary>
    /// Closes the view
    /// </summary>
    void Close();

    /// <summary>
    /// Activates the view, bringing it to the foreground
    /// </summary>
    void Activate();

    /// <summary>
    /// Returns a task that completes when this window closes, passing the connection as a result.
    /// Not thread safe -- must be called on main thread, awaitable anywhere of course
    /// </summary>
    /// <param name="cancellation">A cancellation token that, when cancelled, will stop the waiting operation and return a null connection</param>
    /// <returns>A task that contains the connection that was made just before the window closed</returns>
    Task<IConsoleConnection?> WaitForConnection(CancellationToken cancellation = default);

    void SetUserInfoForConnectionType(string registeredId, UserConnectionInfo info);
}