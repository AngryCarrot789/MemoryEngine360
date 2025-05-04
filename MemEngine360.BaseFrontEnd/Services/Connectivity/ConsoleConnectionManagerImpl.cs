// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.Avalonia.Services.Windowing;

namespace MemEngine360.Avalonia.Services.Connectivity;

public class ConsoleConnectionManagerImpl : ConsoleConnectionManager {
    public override Task OpenDialog(IMemEngineUI engine, string? focusedTypeId = null) {
        if (WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
            ConnectToConsoleView control = new ConnectToConsoleView() {
                EngineUI = engine, FocusedTypeId = focusedTypeId
            };
            
            IWindow window = system.CreateWindow(control);
            window.Show(null);
        }

        return Task.CompletedTask;
    }
}