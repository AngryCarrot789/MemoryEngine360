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

using MemEngine360.BaseFrontEnd.Services.Connectivity;
using MemEngine360.Connections;
using MemEngine360.Xbox360XDevkit.Commands;
using MemEngine360.Xbox360XDevkit.Views;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Plugins;

namespace MemEngine360.Xbox360XDevkit;

public class PluginXbox360XDevkit : Plugin {
    public override void OnCreated() {
        base.OnCreated();
        if (!OperatingSystem.IsWindows()) {
            throw new Exception("The " + this.Name + " plugin is only supported on windows");
        }
    }

    public override void RegisterCommands(CommandManager manager) {
        base.RegisterCommands(manager);
        manager.Register("commands.memengine.remote.ModulesCommand", new ModulesCommand());
        manager.Register("commands.memengine.remote.XboxRunningProcessCommand", new XboxRunningProcessCommand());
        manager.Register("commands.memengine.ShowDebuggerCommand", new ShowDebuggerCommand());
    }

    public override Task OnApplicationFullyLoaded() {
        ConnectToConsoleView.Registry.RegisterType<ConnectToXboxInfo>(() => new ConnectToXboxView());
        
        ConsoleConnectionManager manager = ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionManager>();
        manager.Register(ConsoleTypeXbox360XDevkit.TheID, ConsoleTypeXbox360XDevkit.Instance);
        return Task.CompletedTask;
    }
}