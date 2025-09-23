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
using MemEngine360.Engine.FileBrowsing;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.FileBrowsing;

public class FileBrowserServiceImpl : IFileBrowserService {
    private static readonly DataKey<IWindow> OpenedWindowKey = DataKeys.Create<IWindow>(nameof(IFileBrowserService) + "_OpenedFileExplorerWindow");

    public Task ShowFileBrowser(FileTreeExplorer explorer) {
        if (OpenedWindowKey.TryGetContext(explorer.MemoryEngine.UserContext, out IWindow? debuggerWindow)) {
            Debug.Assert(debuggerWindow.OpenState == OpenState.Open || debuggerWindow.OpenState == OpenState.TryingToClose);
            
            debuggerWindow.Activate();
            return Task.CompletedTask;
        }
        
        if (!WindowContextUtils.TryGetWindowManagerWithUsefulWindow(out IWindowManager? manager, out _)) {
            return Task.CompletedTask;
        }

        FileTreeExplorerView control = new FileTreeExplorerView() {
            PART_FileBrowser = { FileTreeManager = explorer }
        };

        IWindow window = manager.CreateWindow(new WindowBuilder() {
            Title = "File Browser",
            Content = control,
            TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.Tone4.Background.Static"),
            BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
            Width = 1024, Height = 768
        });

        window.WindowClosing += (sender, args) => {
            FileTreeExplorer exp = ((FileTreeExplorerView) sender.Content!).FileTreeExplorer;
            exp.MemoryEngine.UserContext.Remove(OpenedWindowKey);
        };
        
        explorer.MemoryEngine.UserContext.Set(OpenedWindowKey, window);
        return window.ShowAsync();
    }
}