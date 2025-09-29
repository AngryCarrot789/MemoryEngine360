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
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Themes;
using PFXToolKitUI.Utils;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.XboxBase;

public class ShowModulesCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngine.EngineDataKey.TryGetContext(e.ContextData, out MemoryEngine? engine)) {
            return Executability.Invalid;
        }

        return engine.Connection != null ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine.EngineDataKey.TryGetContext(e.ContextData, out MemoryEngine? engine)) {
            return;
        }

        if (!IWindowManager.TryGetInstance(out IWindowManager? manager)) {
            return;
        }

        IConsoleConnection? connection;
        using IDisposable? token = await engine.BeginBusyOperationUsingActivityAsync("Begin reading modules");
        if (token == null || (connection = engine.Connection) == null || connection.IsClosed) {
            return;
        }

        using CancellationTokenSource cts = new CancellationTokenSource();
        ModuleViewer viewer = new ModuleViewer();
        
        Result<bool> result = await ActivityManager.Instance.RunTask(async () => await ModuleViewer.TryFillModuleManager(engine, connection, viewer), cts);
        if (result.Exception != null) {
            if (!(result.Exception is OperationCanceledException)) {
                await LogExceptionHelper.ShowMessageAndPrintToLogs("Error refreshing modules", result.Exception.Message, result.Exception);
            }
        }
        else if (result.Value) {
            ModuleViewerView control = new ModuleViewerView() {
                XboxModuleManager = viewer, MemoryEngine = engine
            };

            IWindow window = manager.CreateWindow(new WindowBuilder() {
                Title = "Memory Viewer",
                Content = control,
                TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.Tone6.Background.Static"),
                BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
                MinWidth = 640, MinHeight = 400,
                Width = 960, Height = 640,
                FocusPath = "ModuleViewerWindow"
            });

            window.WindowClosed += static (sender, args) => {
                ((ModuleViewerView) sender.Content!).XboxModuleManager = null;
                ((ModuleViewerView) sender.Content!).MemoryEngine = null;
            };
            await window.ShowAsync();
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Unsupported connection", "The current connection does not support listing xbox modules");
        }
    }
}