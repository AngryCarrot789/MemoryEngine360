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
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Notifications;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands.ATM;

public class CopyAbsoluteAddressToClipboardCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IEngineUI.EngineUIDataKey.TryGetContext(e.ContextData, out IEngineUI? ui) || ui.ClipboardService == null) {
            return Executability.Invalid;
        }

        if (ui.AddressTableSelectionManager.Count != 1) {
            return Executability.ValidButCannotExecute;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IEngineUI.EngineUIDataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
            return;
        }

        if (ui.AddressTableSelectionManager.Count != 1) {
            return;
        }

        IClipboardService? clipboard = ui.ClipboardService;
        if (clipboard == null) {
            return;
        }

        IAddressTableEntryUI first = ui.AddressTableSelectionManager.SelectedItemList[0];
        uint address = (first.Entry as AddressTableEntry)?.AbsoluteAddress ?? ((AddressTableGroupEntry) first.Entry).AbsoluteAddress;
        string addrText = address.ToString("X8");
        try {
            await clipboard.SetTextAsync(addrText);
        }
        catch (Exception ex) {
            await IMessageDialogService.Instance.ShowMessage("Clipboard unavailable", "Clipboard is in use. Address = " + addrText);
        }
    }
}