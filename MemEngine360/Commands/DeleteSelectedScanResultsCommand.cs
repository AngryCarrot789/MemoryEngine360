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

using System.Collections.ObjectModel;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public class DeleteSelectedScanResultsCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IEngineUI.EngineUIDataKey.TryGetContext(e.ContextData, out IEngineUI? engine))
            return Executability.Invalid;

        return engine.ScanResultSelectionManager.Count < 1 ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IEngineUI.EngineUIDataKey.TryGetContext(e.ContextData, out IEngineUI? engine)) {
            return;
        }

        ObservableCollection<ScanResultViewModel> items = engine.MemoryEngine.ScanningProcessor.ScanResults;
        if (engine.ScanResultSelectionManager.Count == items.Count) {
            items.Clear();
        }
        else {
            List<ScanResultViewModel> list = engine.ScanResultSelectionManager.SelectedItemList.ToList();
            foreach (ScanResultViewModel address in list) {
                items.Remove(address);
            }
        }
    }
}