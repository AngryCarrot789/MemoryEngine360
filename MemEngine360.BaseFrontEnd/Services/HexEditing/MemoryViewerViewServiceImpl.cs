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

using MemEngine360.Engine.HexEditing;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.Services.HexEditing;

public class MemoryViewerViewServiceImpl : IMemoryViewerViewService {
    public Task ShowMemoryViewer(MemoryViewer info) {
        ArgumentNullException.ThrowIfNull(info);
        if (IWindowManager.TryGetInstance(out IWindowManager? manager)) {
            MemoryViewerView control = new MemoryViewerView() {
                HexDisplayInfo = info
            };

            IWindow window = manager.CreateWindow(new WindowBuilder() {
                Title = "Memory Viewer",
                Content = control,
                TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.Tone4.Background.Static"),
                BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
                MinWidth = 800, MinHeight = 480,
                Width = 1280, Height = 720,
                FocusPath = "HexDisplayWindow"
            });

            window.WindowOpened += static (sender, args) => ((MemoryViewerView) sender.Content!).OnWindowOpened(sender);
            window.WindowClosed += static (sender, args) => ((MemoryViewerView) sender.Content!).OnWindowClosed();
            return window.ShowAsync();
        }

        return Task.CompletedTask;
    }
}