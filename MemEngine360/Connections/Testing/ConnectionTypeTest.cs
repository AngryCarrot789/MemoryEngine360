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

namespace MemEngine360.Connections.Testing;

public class ConnectionTypeTest : RegisteredConnectionType {
    public const string TheID = "console.test-donotuse";
    public static readonly RegisteredConnectionType Instance = new ConnectionTypeTest();
    
    public override string DisplayName => "Test (debug)";
    
    public override string LongDescription => "Test connection for debugging. Can do nothing or throw timeout or IO exception,in hopes that the program can handle them.";

    public override UserConnectionInfo? CreateConnectionInfo() {
        return new TestConnectionInfo(this);
    }

    public override Task<IConsoleConnection?> OpenConnection(UserConnectionInfo? _info, IContextData additionalContext, CancellationTokenSource cancellation) {
        return Task.FromResult<IConsoleConnection?>(new TestConsoleConnection(((TestConnectionInfo) _info!).Mode));
    }
}