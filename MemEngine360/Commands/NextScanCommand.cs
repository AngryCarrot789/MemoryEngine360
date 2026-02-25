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
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Commands;

public class NextScanCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine engine, MemoryEngineViewState engineVs, CommandEventArgs e) {
        return !engine.ScanningProcessor.CanPerformNextScan ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngineViewState engineVs, MemoryEngine engine, CommandEventArgs e) {
        if (engine.ScanningProcessor.CanPerformNextScan) {
            // Save original selection state
            List<ScanResultViewModel> selection = engineVs.SelectedScanResults.SelectedItems.ToList();
            engineVs.SelectedScanResults.DeselectAll();
            
            await engine.ScanningProcessor.ScanFirstOrNext();
            
            // Try to restore any items that still exist, since their references will
            // be maintained unless for some reason the ScanningContext is kerfuckled

            ObservableList<ScanResultViewModel> srcList = engineVs.Engine.ScanningProcessor.ScanResults;
            engineVs.SelectedScanResults.SelectItems(selection.Where(x => srcList.Contains(x)));
        }
    }
}