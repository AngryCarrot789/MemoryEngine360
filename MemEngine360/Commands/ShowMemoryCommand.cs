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

using MemEngine360.Engine;
using MemEngine360.Engine.HexDisplay;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public class ShowMemoryCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        return engine.Connection == null ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        if (ApplicationPFX.Instance.ServiceManager.TryGetService(out IHexDisplayService? service)) {
            HexDisplayInfo info = new HexDisplayInfo(engine) {
                Caption = "Console Memory",
                StartAddress = engine.ScanningProcessor.StartAddress,
                Length = engine.ScanningProcessor.ScanLength,
                IsReadOnly = true,
            };

            await service.ShowHexEditor(info);
        }
    }
}