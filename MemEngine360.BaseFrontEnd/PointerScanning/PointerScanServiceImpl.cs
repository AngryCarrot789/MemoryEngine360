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

using MemEngine360.Engine;
using MemEngine360.PointerScanning;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.PointerScanning;

public class PointerScanServiceImpl : IPointerScanService {
    public Task ShowPointerScan(MemoryEngine engine) {
        if (!WindowContextUtils.TryGetWindowManagerWithUsefulWindow(out IWindowManager? manager, out IWindow? parentWindow)) {
            return Task.CompletedTask;
        }

        IWindow window = manager.CreateWindow(new WindowBuilder() {
            Title = "Pointer Scanner",
            Content = new PointerScannerView() {
                PointerScanner = engine.PointerScanner
            },
            TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.Tone7.Background.Static"),
            BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
            MinWidth = 500, MinHeight = 200,
            Width = 1024, Height = 576,
            Parent = parentWindow
        });

        window.WindowClosed += static (sender, args) => {
            PointerScannerView view = (PointerScannerView) sender.Content!;
            if (view.PointerScanner is PointerScanner scanner) {
                scanner.DisposeMemoryDump();
                scanner.Clear();
                view.PointerScanner = null;
            }
        };

        return window.ShowAsync();
    }
}