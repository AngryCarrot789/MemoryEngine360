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

using System.Diagnostics;
using PFXToolKitUI.DataTransfer;

namespace MemEngine360.Connections;

/// <summary>
/// The base class for a model used in MemEngine's connection dialog, which contains
/// properties that can be used during <see cref="RegisteredConnectionType.OpenConnection"/>
/// </summary>
public abstract class UserConnectionInfo : ITransferableData {
    private bool isBeingViewed;

    public TransferableData TransferableData { get; }
    
    public RegisteredConnectionType ConnectionType { get; }

    protected UserConnectionInfo(RegisteredConnectionType connectionType) {
        this.ConnectionType = connectionType ?? throw new ArgumentNullException(nameof(connectionType));
        this.TransferableData = new TransferableData(this);
    }

    /// <summary>
    /// Invoked when this info is now being viewed in the UI, as in, the user can see any UI controls associated with this info
    /// </summary>
    protected abstract void OnShown();

    /// <summary>
    /// Invoked when this info is no longer being viewed in the UI. This is only called when the connection dialog closes
    /// </summary>
    protected abstract void OnHidden();

    public static void InternalOnShown(UserConnectionInfo info) {
        if (info.isBeingViewed)
            throw new InvalidOperationException("Already shown");
        info.isBeingViewed = true;
        info.OnShown();
    }
    
    public static void InternalOnHidden(UserConnectionInfo info) {
        if (!info.isBeingViewed)
            throw new InvalidOperationException("Not shown");
        info.isBeingViewed = false;
        info.OnHidden();
    }

    public static bool InternalIsShown(UserConnectionInfo info) => info.isBeingViewed;
}