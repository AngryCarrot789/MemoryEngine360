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

namespace MemEngine360.Engine;

/// <summary>
/// A cause for the engine connection changing.
/// </summary>
public enum ConnectionChangeCause {
    /// <summary>
    /// The user connected to or disconnected from the console in a standard way (e.g. open connection view or clicking disconnect)
    /// </summary>
    User,
    /// <summary>
    /// We lost connection to the console, maybe it was switched off or the network was disconnected
    /// </summary>
    LostConnection,
    /// <summary>
    /// An unexpected error occurred while reading data, which ultimately meant the connection
    /// was no longer in a stable state and had to be shut down
    /// </summary>
    ConnectionError,
    /// <summary>
    /// The user closed the window which automatically disconnects the connection
    /// </summary>
    ClosingWindow,
    /// <summary>
    /// The connection changed for an unknown reason
    /// </summary>
    Custom
}