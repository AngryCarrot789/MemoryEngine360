﻿// 
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
using PFXToolKitUI.Avalonia.Services.Windowing;

namespace MemEngine360.BaseFrontEnd.PointerScanning;

public class PointerScanServiceImpl : IPointerScanService {
    public async Task ShowPointerScan(MemoryEngine engine) {
        if (!WindowingSystem.TryGetInstance(out var system)) {
            return;
        }
        
        PointerScanWindow window = new PointerScanWindow() {
            PointerScanner = engine.PointerScanner
        };

        window.Closed += (sender, args) => {
            if (((PointerScanWindow) sender!).PointerScanner is PointerScanner scanner) {
                scanner.DisposeMemoryDump();
                scanner.Clear();
                ((PointerScanWindow) sender!).PointerScanner = null;
            }
        };
        
        system.Register(window).Show();
    }
}