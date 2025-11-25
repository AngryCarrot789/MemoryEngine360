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

using System.Runtime.Versioning;
using MemEngine360.BaseFrontEnd.Services.Connectivity;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.PS3.CC;
using MemEngine360.PS3.Commands;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Notifications;
using PFXToolKitUI.Plugins;

namespace MemEngine360.PS3;

[SupportedOSPlatform("windows")]
public class PluginPS3 : Plugin {
    protected override void OnInitialize() {
        base.OnInitialize();

        CommandManager.Instance.Register("commands.ps3ccapi.SetProcessToActiveGameCommand", new SetProcessToActiveGameCommand());
        CommandManager.Instance.Register("commands.ps3ccapi.SetProcessCommand", new SetProcessCommand());
        CommandManager.Instance.Register("commands.ps3ccapi.ListAllProcessesCommand", new ListAllProcessesCommand());
    }

    protected override async Task OnApplicationFullyLoaded() {
        if (OperatingSystem.IsWindows()) {
            ConsoleConnectionManager manager = ApplicationPFX.GetComponent<ConsoleConnectionManager>();
            manager.Register(ConnectionTypePS3CCAPI.TheID, ConnectionTypePS3CCAPI.Instance);
            OpenConnectionView.Registry.RegisterType<ConnectToCCAPIInfo>(
                () => {
                    // The callback is only registered in the OS.IsWindows() block, so this is safe
#pragma warning disable CA1416
                    return new OpenCCAPIConnectionView();
#pragma warning restore CA1416
                });
        }

        MemoryEngineManager.Instance.ProvidePostConnectionActions += OnProvidePostConnectionActions;
    }

    private static void OnProvidePostConnectionActions(object? sender, ProvidePostConnectionActionsEventArgs e) {
        if (e.Connection is ConsoleConnectionCCAPI) {
            e.Notification.Actions.Add(new CommandNotificationAction("Attach to Game Process", "commands.ps3ccapi.SetProcessToActiveGameCommand") {
                ToolTip = "Attach CCAPI to the current running game process. This is required to read/write game memory"
            });
        }
    }
}