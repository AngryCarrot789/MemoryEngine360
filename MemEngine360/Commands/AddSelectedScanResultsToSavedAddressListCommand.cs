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

using System.Collections.ObjectModel;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public class AddSelectedScanResultsToSavedAddressListCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            return Executability.Invalid;
        }

        if (ui.ScanResultSelectionManager.Count < 1) {
            return Executability.ValidButCannotExecute;
        }

        // Probably too intensive for CanExecute
        // List<ScanResultViewModel> list1 = ui.ScanResultSelectionManager.SelectedItemList.ToList();
        // HashSet<uint> addresses = ui.MemoryEngine360.ScanningProcessor.SavedAddresses.Select(x => x.Address).ToHashSet();
        // if (list1.All(x => addresses.Contains(x.Address))) {
        //     return Executability.ValidButCannotExecute;
        // }

        return Executability.Valid;
    }

    protected override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            return Task.CompletedTask;
        }

        if (ui.ScanResultSelectionManager.Count < 1) {
            return Task.CompletedTask;
        }

        ObservableCollection<SavedAddressViewModel> saved = ui.MemoryEngine360.ScanningProcessor.SavedAddresses;
        List<ScanResultViewModel> selection = ui.ScanResultSelectionManager.SelectedItemList.ToList();
        HashSet<uint> existing = ui.MemoryEngine360.ScanningProcessor.SavedAddresses.Select(x => x.Address).ToHashSet();
        foreach (ScanResultViewModel result in selection) {
            if (!existing.Contains(result.Address)) {
                saved.Add(new SavedAddressViewModel(result));
            }
        }
        
        if (selection.All(x => existing.Contains(x.Address))) {
            
        }

        return Task.CompletedTask;
    }
}