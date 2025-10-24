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
using PFXToolKitUI.Avalonia.Interactivity.Dialogs;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.Services.Connectivity;

public class ConsoleConnectionManagerImpl : ConsoleConnectionManager {
    public override async Task<IOpenConnectionView?> ShowOpenConnectionView(OpenConnectionInfo info) {
        if (!WindowContextUtils.TryGetWindowManagerWithUsefulWindow(out IWindowManager? manager, out IDesktopWindow? parentWindow)) {
            return null;
        }

        IDesktopWindow window = manager.CreateWindow(new WindowBuilder() {
            Title = "Connect to a console",
            Content = new OpenConnectionView() {
                OpenConnectionInfo = info
            },
            TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.Tone6.Background.Static"),
            BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
            MinWidth = 600, MinHeight = 350,
            Width = 700, Height = 450,
            Parent = parentWindow
        });

        ((OpenConnectionView) window.Content!).DialogOperation = DialogOperations.WrapDesktopWindow<ConnectionResult>(window);

        window.Opened += static (sender, args) => ((OpenConnectionView) sender.Content!).OnDialogOpened();
        window.Closed += static (sender, args) => ((OpenConnectionView) sender.Content!).OnDialogClosed();
        
        await window.ShowAsync();
        return (OpenConnectionView) window.Content!;
    }
}