// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using MemEngine360.Connections;

namespace MemEngine360.BaseFrontEnd.Services.Connectivity;

public interface IConsoleConnectivityControl {
    /// <summary>
    /// Invoked when the registered console type which represents this control is selected in the connection view
    /// </summary>
    void OnConnected(OpenConnectionView dialog, UserConnectionInfo info);

    /// <summary>
    /// The list box item is no longer selected
    /// </summary>
    void OnDisconnected();
}