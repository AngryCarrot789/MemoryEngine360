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
using MemEngine360.Xbox360XBDM.Commands;
using MemEngine360.Xbox360XBDM.Consoles;
using MemEngine360.Xbox360XBDM.Views;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Plugins;

namespace MemEngine360.Xbox360XBDM;

public class PluginXbox360Xbdm : Plugin {
    public override void RegisterCommands(CommandManager manager) {
        base.RegisterCommands(manager);
        
        manager.Register("commands.memengine.remote.ListHelpCommand", new ListHelpCommand());
        
        // TODO: move commands to ME360 project and use a trait like IDiskEjectable
        manager.Register("commands.memengine.remote.ShowConsoleInfoCommand", new ShowConsoleInfoCommand());
        manager.Register("commands.memengine.remote.ShowXbeInfoCommand", new ShowXbeInfoCommand());
        manager.Register("commands.memengine.remote.EjectDiskTrayCommand", new EjectDiskTrayCommand());
    }
    
    public override Task OnApplicationFullyLoaded() {
        ConnectToConsoleView.Registry.RegisterType<ConnectToXboxInfo>(() => new ConnectToXboxView());
        
        ConsoleConnectionManager manager = ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionManager>();
        manager.Register(ConsoleTypeXbox360Xbdm.TheID, ConsoleTypeXbox360Xbdm.Instance);
        return Task.CompletedTask;
    }
}