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

using MemEngine360.Engine.FileBrowsing;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.FileBrowsing;

public class FileBrowserServiceImpl : IFileBrowserService {
    private static readonly DataKey<IDesktopWindow> OpenedWindowKey = DataKeys.Create<IDesktopWindow>(nameof(IFileBrowserService) + "_OpenedFileExplorerWindow");

    public Task ShowFileBrowser(FileTreeExplorer explorer) {
        if (OpenedWindowKey.TryGetContext(explorer.MemoryEngine.UserContext, out IDesktopWindow? debuggerWindow)) {
            if (debuggerWindow.OpenState.IsOpenOrTryingToClose()) {
                debuggerWindow.Activate();
            }

            return Task.CompletedTask;
        }
        
        if (!IWindowManager.TryGetInstance(out IWindowManager? manager)) {
            return Task.CompletedTask;
        }

        FileTreeExplorerView control = new FileTreeExplorerView() {
            FileTreeExplorer = explorer
        };

        IDesktopWindow window = manager.CreateWindow(new WindowBuilder() {
            Title = "File Browser",
            Content = control,
            TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.Tone4.Background.Static"),
            BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
            Width = 1024, Height = 768
        });

        window.Closing += static (s, args) => {
            FileTreeExplorerView content = (FileTreeExplorerView) ((IDesktopWindow) s!).Content!;
            FileTreeExplorer exp = content.FileTreeExplorer!;
            exp.MemoryEngine.UserContext.Remove(OpenedWindowKey);

            content.FileTreeExplorer = null;
        };
        
        explorer.MemoryEngine.UserContext.Set(OpenedWindowKey, window);
        return window.ShowAsync();
    }
}