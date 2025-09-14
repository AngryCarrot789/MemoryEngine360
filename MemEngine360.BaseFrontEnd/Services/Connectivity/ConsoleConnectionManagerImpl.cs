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

using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.Services.Connectivity;

public class ConsoleConnectionManagerImpl : ConsoleConnectionManager {
    public override async Task<IOpenConnectionView?> ShowOpenConnectionView(MemoryEngine? engine, string? focusedTypeId = "console.xbox360.xbdm-coreimpl") {
        if (IWindowManager.TryGetInstance(out IWindowManager? manager)) {
            OpenConnectionViewEx view = new OpenConnectionViewEx() {
                MemoryEngine = engine, TypeToFocusOnOpened = focusedTypeId
            };
            
            IWindow window = manager.CreateWindow(new WindowBuilder() {
                Title = "Connect to a console",
                Content = view,
                TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.Tone6.Background.Static"),
                BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
                MinWidth = 600, MinHeight = 350,
                Width = 700, Height = 450
            });

            window.WindowOpened += (sender, args) => ((OpenConnectionViewEx) sender.Content!).OnWindowOpened(sender);
            window.WindowClosed += (sender, args) => ((OpenConnectionViewEx) sender.Content!).OnWindowClosed();
            await window.ShowAsync();
            return view;
        }
        
        return null;
    }
}