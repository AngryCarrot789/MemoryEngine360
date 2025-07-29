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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.Avalonia.Services.Windowing;

namespace MemEngine360.BaseFrontEnd.Services.Connectivity;

public class ConsoleConnectionManagerImpl : ConsoleConnectionManager {
    public override async Task<IOpenConnectionView?> ShowOpenConnectionView(MemoryEngine? engine, string? focusedTypeId = "console.xbox360.xbdm-coreimpl") {
        if (!WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
            return null;
        }

        OpenConnectionWindow window = new OpenConnectionWindow() {
            MemoryEngine = engine, TypeToFocusOnOpened = focusedTypeId
        };
        
        if (system.TryGetActiveWindow(out DesktopWindow? active))
            window.PlaceCenteredTo(active);
        
        system.Register(window).Show();
        return window;
    }

    public class CoolWindow : DesktopWindow {
        public CoolWindow() {
            CheckBox coolCheckBox = new CheckBox() {Content = "Item 1"};
            DynamicResourceExtension DynamicResource = new DynamicResourceExtension("ABrush.Tone7.Background.Static");
            BindingExpressionBase binding = coolCheckBox.Bind(BackgroundProperty, DynamicResource);
            binding.Dispose();
            
            this.Content = new Grid() {
                Children = {
                    new StackPanel() {
                        Spacing = 5, Margin = new Thickness(10),
                        Children = {
                            coolCheckBox,
                            new CheckBox() {Content = "Item 2"},
                            new CheckBox() {Content = "Item 3", [!BackgroundProperty] = new DynamicResourceExtension("ABrush.Tone8.Background.Static")},
                        }
                    }
                }
            };
        }
    }
}