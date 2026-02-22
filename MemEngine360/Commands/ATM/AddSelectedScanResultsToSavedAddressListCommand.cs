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
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.Engine.View;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Selections;

namespace MemEngine360.Commands.ATM;

public class AddSelectedScanResultsToSavedAddressListCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngineViewState.DataKey.TryGetContext(e.ContextData, out MemoryEngineViewState? engineVs)) {
            return Executability.Invalid;
        }

        if (engineVs.SelectedScanResults.Count < 1) {
            return Executability.ValidButCannotExecute;
        }

        // Probably too intensive for CanExecute
        // List<ScanResultViewModel> list1 = ui.ScanResultSelectionManager.SelectedItems.ToList();
        // HashSet<uint> addresses = ui.MemoryEngine.ScanningProcessor.SavedAddresses.Select(x => x.Address).ToHashSet();
        // if (list1.All(x => addresses.Contains(x.Address))) {
        //     return Executability.ValidButCannotExecute;
        // }

        return Executability.Valid;
    }

    protected override Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngineViewState.DataKey.TryGetContext(e.ContextData, out MemoryEngineViewState? engineVs)) {
            return Task.CompletedTask;
        }

        ListSelectionModel<ScanResultViewModel> selectionModel = engineVs.SelectedScanResults;
        if (selectionModel.Count < 1) {
            return Task.CompletedTask;
        }

        AddressTableGroupEntry saved = engineVs.Engine.AddressTableManager.RootEntry;
        List<ScanResultViewModel> selection = selectionModel.SelectedItems.ToList();
        foreach (ScanResultViewModel result in selection) {
            saved.Items.Add(new AddressTableEntry(result));
        }

        return Task.CompletedTask;
    }
}