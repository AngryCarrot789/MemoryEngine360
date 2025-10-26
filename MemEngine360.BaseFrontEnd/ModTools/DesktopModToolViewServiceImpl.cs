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

using System.Diagnostics;
using MemEngine360.ModTools;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.ModTools;

public class DesktopModToolViewServiceImpl : IModToolViewService {
    public static readonly DataKey<IDesktopWindow> ToolGuiDataKey = DataKeys.Create<IDesktopWindow>(nameof(DesktopModToolViewServiceImpl) + "_" + nameof(ToolGuiDataKey));


    public async Task ShowOrFocusWindow(ModToolManager modToolManager) {
        if (ITopLevel.TryGetFromContext(modToolManager.UserContext, out ITopLevel? sequencerTopLevel)) {
            IDesktopWindow window = (IDesktopWindow) sequencerTopLevel;

            Debug.Assert(window.OpenState.IsOpenOrTryingToClose());
            window.Activate();
            return;
        }

        if (IWindowManager.TryGetInstance(out IWindowManager? manager)) {
            IDesktopWindow window = manager.CreateWindow(new WindowBuilder() {
                Title = "Mod Tools",
                FocusPath = "ModToolManagerWindow",
                Content = new ModToolManagerView() {
                    ModToolManager = modToolManager,
                },
                TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.MemEngine.Sequencer.TitleBarBackground"),
                BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
                MinWidth = 640, MinHeight = 400,
                Width = 960, Height = 640
            });

            window.Opened += (sender, args) => ((ModToolManagerView) sender.Content!).OnWindowOpened(sender);
            window.Closing += (sender, args) => {
                ModToolManager tsm = ((ModToolManagerView) sender.Content!).ModToolManager!;
                tsm.UserContext.Remove(ITopLevel.TopLevelDataKey);
            };

            window.Closed += (sender, args) => ((ModToolManagerView) sender.Content!).OnWindowClosed();

            modToolManager.UserContext.Set(ITopLevel.TopLevelDataKey, window);
            await window.ShowAsync();
        }
    }

    public async Task ShowOrFocusGui(ModTool tool) {
        if (ToolGuiDataKey.TryGetContext(tool.UserContext, out IDesktopWindow? currentWindow)) {
            currentWindow.Activate();
            return;
        }

        if (!IWindowManager.TryGetInstance(out IWindowManager? manager)) {
            return;
        }

        IDesktopWindow window = manager.CreateWindow(new WindowBuilder() {
            Title = tool.Name ?? "Unnamed mod tool",
            FocusPath = "ModToolWindow",
            Content = new ModToolView() {
                ModTool = tool,
            },
            TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.MemEngine.Sequencer.TitleBarBackground"),
            BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
            MinWidth = 250, MinHeight = 120,
            Width = 500, Height = 500
        });

        window.Opened += static (sender, args) => ((ModToolView) sender.Content!).OnWindowOpened(sender);
        window.TryCloseAsync += static async (sender, args) => {
            bool cancel = await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(async () => {
                return await ((ModToolView) sender.Content!).ShouldCancelWindowClosing(false);
            });
            
            if (cancel)
                args.SetCancelled();
        };
        
        window.ClosingAsync += async static (sender, args) => {
            await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(async () => {
                await ((ModToolView) sender.Content!).ShouldCancelWindowClosing(true);
                
                ModTool t = ((ModToolView) sender.Content!).ModTool!;
                t.UserContext.Remove(ToolGuiDataKey);
            });
        };

        window.Closed += (sender, args) => {
            ModToolView view = (ModToolView) sender.Content!;
            view.OnWindowClosed();
        };

        tool.UserContext.Set(ToolGuiDataKey, window);
        
        await window.ShowAsync();
    }

    public async Task CloseGui(ModTool tool) {
        if (ToolGuiDataKey.TryGetContext(tool.UserContext, out IDesktopWindow? currentWindow)) {
            await currentWindow.RequestCloseAsync();
        }
    }
}