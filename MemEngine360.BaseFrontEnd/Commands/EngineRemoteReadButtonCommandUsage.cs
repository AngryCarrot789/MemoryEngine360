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

using MemEngine360.Engine;

namespace MemEngine360.BaseFrontEnd.Commands;

public abstract class EngineRemoteReadButtonCommandUsage : EngineConnectionReliantButtonCommandUsage {
    protected EngineRemoteReadButtonCommandUsage(string commandId) : base(commandId) {
    }
    
    protected override void OnEngineChanged(MemoryEngine? oldEngine, MemoryEngine? newEngine) {
        base.OnEngineChanged(oldEngine, newEngine);
        if (oldEngine != null)
            oldEngine.IsBusyChanged -= this.ConnectionOnIsBusyChanged;
        if (newEngine != null)
            newEngine.IsBusyChanged += this.ConnectionOnIsBusyChanged;
    }

    private void ConnectionOnIsBusyChanged(MemoryEngine sender) {
        this.UpdateCanExecuteLater();
    }
}

public class EjectDiskTrayCommandUsage() : EngineRemoteReadButtonCommandUsage("commands.memengine.remote.EjectDiskTrayCommand");
public class ShutdownCommandUsage() : EngineRemoteReadButtonCommandUsage("commands.memengine.remote.ShutdownCommand");
public class SoftRebootCommandUsage() : EngineRemoteReadButtonCommandUsage("commands.memengine.remote.SoftRebootCommand");
public class ColdRebootCommandUsage() : EngineRemoteReadButtonCommandUsage("commands.memengine.remote.ColdRebootCommand");
public class DebugFreezeCommandUsage() : EngineRemoteReadButtonCommandUsage("commands.memengine.remote.DebugFreezeCommand");
public class DebugUnfreezeCommandUsage() : EngineRemoteReadButtonCommandUsage("commands.memengine.remote.DebugUnfreezeCommand");
