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
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands.ATM;

public class CopyAddressTableEntryToClipboardCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        return e.ContextData.ContainsKey(IEngineUI.EngineUIDataKey) ? Executability.Valid : Executability.Invalid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IEngineUI.EngineUIDataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
            return;
        }

        List<IAddressTableEntryUI> selection = ui.AddressTableSelectionManager.SelectedItems.Where(x => x.Entry is AddressTableEntry).ToList();
        if (selection.Count < 1) {
            await IMessageDialogService.Instance.ShowMessage("No selection", "No selected items!", defaultButton: MessageBoxResult.OK);
        }
        else {
            List<AddressTableEntry> items = selection.Select(x => (AddressTableEntry) x.Entry).ToList();
            List<string> text = items.Select(x => x.Address + "," + x.Description + "," + x.DataType + "," + (x.Value != null ? DataValueUtils.GetStringFromDataValue(x, x.Value) : "")).ToList();
            await IMessageDialogService.Instance.ShowMessage("Address table entries", string.Join(Environment.NewLine, text), defaultButton: MessageBoxResult.OK);
        }
    }
}