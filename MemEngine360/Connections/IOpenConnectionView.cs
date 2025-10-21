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
using PFXToolKitUI.Interactivity.Dialogs;

namespace MemEngine360.Connections;

/// <summary>
/// An interface for an open connection window
/// </summary>
public interface IOpenConnectionView {
    /// <summary>
    /// Used to check if an attempt to open a connection is actually made from a <see cref="IOpenConnectionView"/>.
    /// <para>
    /// If the user clicks the "Reconnect" button in the notification in the engine's window when the connection is lost,
    /// then this key will either not be present or be false in the context data provided to <see cref="RegisteredConnectionType.OpenConnection"/>
    /// </para>
    /// </summary>
    static readonly DataKey<bool> IsConnectingFromViewDataKey = DataKeys.Create<bool>(nameof(IOpenConnectionView) + "_IsConnectingFromView");
    
    /// <summary>
    /// Gets the dialog operation
    /// </summary>
    IDialogOperation<ConnectionResult> DialogOperation { get; }

    /// <summary>
    /// Returns a task that completes when this window closes, passing the connection as a result.
    /// Not thread safe -- must be called on main thread, awaitable anywhere of course
    /// </summary>
    /// <param name="cancellation">A cancellation token that, when cancelled, will stop the waiting operation and return a null connection</param>
    /// <returns>A task that contains the connection that was made just before the window closed</returns>
    async Task<ConnectionResult?> WaitForConnection(CancellationToken cancellation = default) {
        try {
            return await this.DialogOperation.WaitForResultAsync(cancellation);
        }
        catch (OperationCanceledException) {
            return null;
        }
    }
}

public readonly struct ConnectionResult(IConsoleConnection connection, UserConnectionInfo? info) {
    /// <summary>
    /// Gets the connection
    /// </summary>
    public IConsoleConnection Connection { get; } = connection;

    /// <summary>
    /// Gets the user connection info used to actually connect
    /// </summary>
    public UserConnectionInfo? Info { get; } = info;
}