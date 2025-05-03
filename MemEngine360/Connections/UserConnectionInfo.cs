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

using PFXToolKitUI.DataTransfer;

namespace MemEngine360.Connections;

/// <summary>
/// The base class for a model used in MemEngine's connection dialog, which contains
/// properties that can be used during <see cref="RegisteredConsoleType.OpenConnection"/>
/// </summary>
public abstract class UserConnectionInfo : ITransferableData {
    public TransferableData TransferableData { get; }
    
    public UserConnectionInfo() {
        this.TransferableData = new TransferableData(this);
    }

    /// <summary>
    /// Invoked when this object is associated with a control
    /// </summary>
    /// <param name="consoleType">The console type</param>
    /// <param name="engine">The engine</param>
    public abstract void OnCreated();

    /// <summary>
    /// Invoked when this info is destroyed. Invoked when the user selects
    /// another list box item in the connection UI, therefore this info is no longer needed
    /// </summary>
    public abstract void OnDestroyed();
}