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

using System.Globalization;
using MemEngine360.BaseFrontEnd.Services.Connectivity;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Xbox360XBDM.Commands;
using MemEngine360.Xbox360XBDM.Consoles;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;
using MemEngine360.Xbox360XBDM.Views;
using MemEngine360.XboxBase.Modules;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Plugins;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Xbox360XBDM;

public class PluginXbox360Xbdm : Plugin {
    public override void RegisterCommands(CommandManager manager) {
        base.RegisterCommands(manager);

        manager.Register("commands.memengine.remote.ListHelpCommand", new ListHelpCommand());

        // TODO: move commands to ME360 project and use a trait like IDiskEjectable
        manager.Register("commands.memengine.remote.ShowConsoleInfoCommand", new ShowConsoleInfoCommand());
        manager.Register("commands.memengine.remote.ShowXbeInfoCommand", new ShowXbeInfoCommand());
        manager.Register("commands.memengine.remote.EjectDiskTrayCommand", new EjectDiskTrayCommand());
        manager.Register("commands.memengine.remote.SendCmdCommand", new SendCmdCommand());
    }

    public override Task OnApplicationFullyLoaded() {
        OpenConnectionView.Registry.RegisterType<ConnectToXboxInfo>(() => new OpenXbdmConnectionView());

        ConsoleConnectionManager manager = ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionManager>();
        manager.Register(ConnectionTypeXbox360Xbdm.TheID, ConnectionTypeXbox360Xbdm.Instance);

        ModuleViewer.RegisterHandlerForConnectionType<XbdmConsoleConnection>(new XbdmModuleViewerProcessor());
        
        return Task.CompletedTask;
    }

    private class XbdmModuleViewerProcessor : IModuleManagerProcessor {
        public Task RefreshAll(ModuleViewer viewer, MemoryEngine engine, IConsoleConnection connection) {
            viewer.Modules.Clear();
            
            return FillModuleManager(engine, (XbdmConsoleConnection) connection, viewer);
        }

        public Task RefreshModule(ConsoleModule module, MemoryEngine engine, IConsoleConnection connection) {
            return Task.CompletedTask;
        }
    }

    private static async Task FillModuleManager(MemoryEngine engine, XbdmConsoleConnection connection, ModuleViewer viewer) {
        ActivityTask task = ActivityManager.Instance.CurrentTask;
        task.Progress.Caption = "Reading Modules";
        task.Progress.Text = "Reading modules...";
        task.Progress.IsIndeterminate = true;

        List<string> modules = await connection.SendCommandAndReceiveLines("modules");
        task.Progress.IsIndeterminate = false;

        CompletionState completion = task.Progress.CompletionState;
        using PopCompletionStateRangeToken completionState = completion.PushCompletionRange(0, 1.0 / modules.Count);
        
        foreach (string moduleLine in modules) {
            task.CheckCancelled();

            if (!ParamUtils.GetStrParam(moduleLine, "name", true, out string? name) ||
                !ParamUtils.GetDwParam(moduleLine, "base", true, out uint modBase) ||
                !ParamUtils.GetDwParam(moduleLine, "size", true, out uint modSize)) {
                continue;
            }

            // ParamUtils.GetDwParam(moduleLine, "timestamp", true, out uint modTimestamp);
            // ParamUtils.GetDwParam(moduleLine, "check", true, out uint modChecksum);
            ParamUtils.GetDwParam(moduleLine, "osize", true, out uint modOriginalSize);
            ParamUtils.GetDwParam(moduleLine, "timestamp", true, out uint timestamp);
            task.Progress.Text = "Processing " + name;
            completion.OnProgress(1.0);

            ConsoleModule consoleModule = new ConsoleModule() {
                Name = name,
                FullName = null, // unavailable until I can figure out how to get xbeinfo to work
                BaseAddress = modBase,
                ModuleSize = modSize,
                OriginalModuleSize = modOriginalSize,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime
                // EntryPoint = module.GetEntryPointAddress()
            };

            // 0x10100 = entry point for most things, apart from xboxkrnl.exe
            // format: 
            //   fieldsize=0x<size in uint32 hex>
            //   <value>
            // may return ResponseType.XexFieldNotFound
            XbdmResponse entryPointResponse = await connection.SendCommand($"xexfield module=\"{name}\" field=0x10100");
            if (entryPointResponse.ResponseType == XbdmResponseType.MultiResponse) {
                List<string> lines = await connection.ReadMultiLineResponse();
                if (lines.Count == 2 && uint.TryParse(lines[1], NumberStyles.HexNumber, null, out uint entryPoint)) {
                    consoleModule.EntryPoint = entryPoint;
                }
            }

            XbdmResponse response = await connection.SendCommand($"modsections name=\"{name}\"");
            if (response.ResponseType != XbdmResponseType.FileNotFound) {
                List<string> sections = await connection.ReadMultiLineResponse();
                foreach (string sectionLine in sections) {
                    task.CheckCancelled();

                    ParamUtils.GetStrParam(sectionLine, "name", true, out string? sec_name);
                    ParamUtils.GetDwParam(sectionLine, "base", true, out uint sec_base);
                    ParamUtils.GetDwParam(sectionLine, "size", true, out uint sec_size);
                    ParamUtils.GetDwParam(sectionLine, "index", true, out uint sec_index);
                    ParamUtils.GetDwParam(sectionLine, "flags", true, out uint sec_flags);

                    consoleModule.Sections.Add(new ConsoleModuleSection() {
                        Name = string.IsNullOrWhiteSpace(sec_name) ? null : sec_name,
                        BaseAddress = sec_base,
                        Size = sec_size,
                        Index = sec_index,
                        Flags = (XboxBase.XboxSectionInfoFlags) sec_flags,
                    });
                }

                viewer.Modules.Add(consoleModule);
            }
        }
    }
}