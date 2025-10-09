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
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.PS3;

public class ConnectionTypePS3CCAPI : RegisteredConnectionType {
    public const string TheID = "console.ps3.ccapi-coreimpl"; // -coreimpl suffix added to core plugins, not that we need to but eh
    public static readonly RegisteredConnectionType Instance = new ConnectionTypePS3CCAPI();

    public override string DisplayName => "PS3 (CCAPI)";

    public override string? FooterText => "Untested";

    public override string LongDescription => "A connection to a PS3 using CCAPI";

    public override Icon Icon => SimpleIcons.PS3CCAPIIcon;

    public override bool SupportsEvents => false;

    private ConnectionTypePS3CCAPI() {
    }

    public override async Task<IConsoleConnection?> OpenConnection(UserConnectionInfo? _info, IContextData additionalContext, CancellationTokenSource cancellation) {
        await IMessageDialogService.Instance.ShowMessage("Unsupported", "Coming soon!");
        return null;
    }
}