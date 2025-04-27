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

using MemEngine360.Connections.Impl;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands;

public class MemProtectionCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        return engine.Connection != null ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        await engine.BeginBusyOperationActivityAsync(async (t, c) => {
            List<MemoryRegion> results = await c.GetMemoryRegions();
            List<string> lines = results.Select(x => $"Base: {x.BaseAddress:X8} Size: {x.Size:X8}, Protection: {x.Protection:X8}, PhysicalAddress: {x.PhysicalAddress:X8}").ToList();
            await IMessageDialogService.Instance.ShowMessage("Memory Regions", string.Join(Environment.NewLine, lines));
        });
    }
}