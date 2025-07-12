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

using System.Runtime.InteropServices;
using MemEngine360.BaseFrontEnd.Services.Connectivity;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Xbox360XDevkit.Commands;
using MemEngine360.Xbox360XDevkit.Views;
using MemEngine360.XboxBase.Modules;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Plugins;
using PFXToolKitUI.Tasks;
using XDevkit;

namespace MemEngine360.Xbox360XDevkit;

public class PluginXbox360XDevkit : Plugin {
    public override string Name => "Xbox360 XDevkit";

    public override void OnCreated() {
        base.OnCreated();
        if (!OperatingSystem.IsWindows()) {
            throw new Exception("The " + this.Name + " plugin is only supported on windows");
        }
    }

    public override void RegisterCommands(CommandManager manager) {
        base.RegisterCommands(manager);
        manager.Register("commands.memengine.remote.XboxRunningProcessCommand", new XboxRunningProcessCommand());
    }

    public override Task OnApplicationFullyLoaded() {
        OpenConnectionView.Registry.RegisterType<ConnectToXboxInfo>(() => new OpenXDevkitConnectionView());

        ConsoleConnectionManager manager = ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionManager>();
        manager.Register(ConnectionTypeXbox360XDevkit.TheID, ConnectionTypeXbox360XDevkit.Instance);

        ModuleViewer.RegisterHandlerForConnectionType<XDevkitConsoleConnection>(new XDevkitModuleViewerProcessor());

        return Task.CompletedTask;
    }
    
    private class XDevkitModuleViewerProcessor : IModuleManagerProcessor {
        public Task RefreshAll(ModuleViewer viewer, MemoryEngine engine, IConsoleConnection connection) {
            return FillModuleManager(engine, (XDevkitConsoleConnection) connection, viewer);
        }

        public Task RefreshModule(ConsoleModule module, MemoryEngine engine, IConsoleConnection connection) {
            return Task.CompletedTask;
        }
    }

    private static async Task FillModuleManager(MemoryEngine arg1, XDevkitConsoleConnection connection, ModuleViewer viewer) {
        ActivityTask task = ActivityManager.Instance.CurrentTask;
        task.Progress.Caption = "Reading Modules";
        task.Progress.Text = "Reading modules...";
        task.Progress.IsIndeterminate = true;

        foreach (IXboxModule module in connection.Console.DebugTarget.Modules) {
            task.CheckCancelled();

            XBOX_MODULE_INFO info = module.ModuleInfo;
            task.Progress.Text = "Processing " + info.Name;

            uint entryPoint = module.GetEntryPointAddress();

            string? fullName = info.FullName;
            ConsoleModule consoleModule = new ConsoleModule() {
                Name = info.Name,
                FullName = Equals(info.Name, fullName) ? null : fullName,
                BaseAddress = info.BaseAddress,
                ModuleSize = info.Size,
                OriginalModuleSize = module.OriginalSize,
                EntryPoint = entryPoint
            };

            try {
                consoleModule.PEModuleName = module.Executable.GetPEModuleName();
            }
            catch (COMException ex) {
                consoleModule.PEModuleName = $"<COMException: {ex.Message}>";
            }

            foreach (IXboxSection section in module.Sections) {
                task.CheckCancelled();

                XBOX_SECTION_INFO secInf = section.SectionInfo;
                consoleModule.Sections.Add(new ConsoleModuleSection() {
                    Name = string.IsNullOrWhiteSpace(secInf.Name) ? null : secInf.Name,
                    BaseAddress = secInf.BaseAddress,
                    Size = secInf.Size,
                    Index = secInf.Index,
                    Flags = (MemEngine360.XboxBase.XboxSectionInfoFlags) secInf.Flags,
                });
            }

            viewer.Modules.Add(consoleModule);
        }
    }
}