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
using MemEngine360.Engine.View;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Commands;

public class RefreshSavedAddressesCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine engine, CommandEventArgs e) {
        if (engine.Connection == null || engine.ScanningProcessor.IsRefreshingAddresses) {
            return Executability.ValidButCannotExecute;
        }
        
        return Executability.Valid;
    }
    
    protected override DisabledHintInfo? ProvideDisabledHintOverride(MemoryEngine engine, IContextData context, ContextRegistry? sourceContextMenu) {
        if (TryProvideNotConnectedDisabledHintInfo(engine, out DisabledHintInfo? hintInfo))
            return hintInfo;
        return null;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngineViewState engineVs, MemoryEngine engine, CommandEventArgs e) {
        using IBusyToken? token = await engine.BeginBusyOperationUsingActivityAsync("Refreshing values");
        if (token == null) {
            return;
        }

        ScanningProcessor p = engine.ScanningProcessor;
        if (engine.Connection != null && !p.IsRefreshingAddresses) {
            await engine.ScanningProcessor.RefreshSavedAddressesAsync(token, true, true);
        }
    }
}