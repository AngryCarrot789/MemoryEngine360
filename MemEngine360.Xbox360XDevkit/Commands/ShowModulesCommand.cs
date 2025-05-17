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

using System.Runtime.InteropServices;
using MemEngine360.Engine;
using MemEngine360.Xbox360XDevkit.Modules;
using MemEngine360.Xbox360XDevkit.Modules.Models;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using XDevkit;

namespace MemEngine360.Xbox360XDevkit.Commands;

public class ShowModulesCommand : Command {
    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IMemEngineUI.MemUIDataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            return;
        }

        if (!WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
            return;
        }

        if (!(ui.MemoryEngine360.Connection is Devkit360Connection connection)) {
            await IMessageDialogService.Instance.ShowMessage("Unsupported connection", "This command requires you be connected via the XDevkit connection");
            return;
        }

        using IDisposable? token = await ui.MemoryEngine360.BeginBusyOperationActivityAsync("Begin reading modules");
        if (token == null) {
            return;
        }

        using CancellationTokenSource cts = new CancellationTokenSource();
        XboxModuleManager? manager = await ActivityManager.Instance.RunTask(async () => {
            ActivityTask task = ActivityManager.Instance.CurrentTask;
            task.Progress.Caption = "Reading Modules";
            task.Progress.Text = "Reading modules...";
            task.Progress.IsIndeterminate = true;

            XboxModuleManager manager = new XboxModuleManager();
            foreach (IXboxModule module in connection.Console.DebugTarget.Modules) {
                task.CheckCancelled();

                XBOX_MODULE_INFO info = module.ModuleInfo;
                task.Progress.Text = "Processing " + info.Name;

                XboxModule xboxModule = new XboxModule() {
                    ShortName = info.Name,
                    LongName = info.FullName,
                    BaseAddress = info.BaseAddress,
                    ModuleSize = info.Size,
                    OriginalModuleSize = module.OriginalSize,
                    EntryPoint = module.GetEntryPointAddress()
                };

                try {
                    xboxModule.PEModuleName = module.Executable.GetPEModuleName();
                }
                catch (COMException ex) {
                    xboxModule.PEModuleName = $"<COMException: {ex.Message}>";
                }

                foreach (IXboxSection section in module.Sections) {
                    task.CheckCancelled();

                    XBOX_SECTION_INFO secInf = section.SectionInfo;
                    xboxModule.Sections.Add(new XboxModuleSection() {
                        BaseAddress = secInf.BaseAddress,
                        Size = secInf.Size,
                        Index = secInf.Index,
                        Flags = secInf.Flags,
                    });
                }

                manager.Modules.Add(xboxModule);
            }

            return manager;
        }, cts);

        if (manager != null) { // may be null when cancelled
            ModuleViewerWindow window = new ModuleViewerWindow() {
                XboxModuleManager = manager
            };

            system.Register(window).Show();
            window.Activate();
        }
    }
}