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

public class DeleteSelectedSavedAddressesCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? engine))
            return Executability.Invalid;
        
        return engine.SavedAddressesSelectionManager.Count < 1 ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? engine)) {
            return;
        }

        ObservableCollection<SavedAddressViewModel> items = engine.MemoryEngine360.ScanningProcessor.SavedAddresses;
        if (engine.SavedAddressesSelectionManager.Count == items.Count) {
            items.Clear();
        }
        else {
            List<SavedAddressViewModel> list = engine.SavedAddressesSelectionManager.SelectedItemList.ToList();
            foreach (SavedAddressViewModel address in list) {
                items.Remove(address);
            }
        }
    }
}