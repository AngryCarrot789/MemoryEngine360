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

namespace MemEngine360.Connections;

/// <summary>
/// An interface for an open connection window
/// </summary>
public interface IOpenConnectionView {
    /// <summary>
    /// Returns true when this window was closed by the user clicking the close button, cancel, or it closed mysteriously (e.g. app or OS shutdown)
    /// </summary>
    bool IsClosed { get; }

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
    /// <returns></returns>
    Task<IConsoleConnection?> WaitForClose();
}