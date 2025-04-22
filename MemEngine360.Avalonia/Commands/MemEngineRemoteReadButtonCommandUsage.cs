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

using MemEngine360.Engine;

namespace MemEngine360.Avalonia.Commands;

public abstract class MemEngineRemoteReadButtonCommandUsage : MemEngineConnectionReliantButtonCommandUsage {
    protected MemEngineRemoteReadButtonCommandUsage(string commandId) : base(commandId) {
    }
    
    protected override void OnEngineChanged(MemoryEngine360? oldEngine, MemoryEngine360? newEngine) {
        base.OnEngineChanged(oldEngine, newEngine);
        if (oldEngine != null)
            oldEngine.IsBusyChanged -= this.ConnectionOnIsBusyChanged;
        if (newEngine != null)
            newEngine.IsBusyChanged += this.ConnectionOnIsBusyChanged;
    }

    private void ConnectionOnIsBusyChanged(MemoryEngine360 sender) {
        this.UpdateCanExecuteLater();
    }
}

public class EjectDiskTrayCommandUsage() : MemEngineRemoteReadButtonCommandUsage("commands.memengine.remote.EjectDiskTrayCommand");
public class ShutdownCommandUsage() : MemEngineRemoteReadButtonCommandUsage("commands.memengine.remote.ShutdownCommand");
public class SoftRebootCommandUsage() : MemEngineRemoteReadButtonCommandUsage("commands.memengine.remote.SoftRebootCommand");
public class ColdRebootCommandUsage() : MemEngineRemoteReadButtonCommandUsage("commands.memengine.remote.ColdRebootCommand");
public class DebugFreezeCommandUsage() : MemEngineRemoteReadButtonCommandUsage("commands.memengine.remote.DebugFreezeCommand");
public class DebugUnfreezeCommandUsage() : MemEngineRemoteReadButtonCommandUsage("commands.memengine.remote.DebugUnfreezeCommand");
