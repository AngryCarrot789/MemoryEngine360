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

using MemEngine360.BaseFrontEnd.XboxBase.Modules;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.XboxBase.Modules;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;

namespace MemEngine360.BaseFrontEnd.XboxBase;

public class ShowModulesCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IEngineUI.EngineUIDataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
            return Executability.Invalid;
        }

        return ui.MemoryEngine.Connection != null ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IEngineUI.EngineUIDataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
            return;
        }

        if (!WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
            return;
        }

        MemoryEngine engine = ui.MemoryEngine;
        IConsoleConnection? connection;
        using IDisposable? token = await engine.BeginBusyOperationActivityAsync("Begin reading modules");
        if (token == null || (connection = engine.Connection) == null || !connection.IsConnected) {
            return;
        }

        using CancellationTokenSource cts = new CancellationTokenSource();
        XboxModuleManager manager = new XboxModuleManager();
        
        bool result = await ActivityManager.Instance.RunTask(async () => await XboxModuleManager.TryFillModuleManager(engine, connection, manager), cts);
        if (result) { // may be null when cancelled
            ModuleViewerWindow window = new ModuleViewerWindow() {
                XboxModuleManager = manager
            };

            system.Register(window).Show();
            window.Activate();
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Unsupported connection", "The current connection does not support listing xbox modules");
        }
    }
}