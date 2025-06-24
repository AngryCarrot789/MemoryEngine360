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
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands.ATM;

public class CopyATEValueToClipboardCommand : BaseCopyAddressTableEntryCommand {
    protected override Executability CanExecute(IAddressTableEntryUI entry, CommandEventArgs e) {
        return entry.Entry is AddressTableEntry ? Executability.Valid : Executability.Invalid;
    }

    protected override async Task Copy(IAddressTableEntryUI _entry, IClipboardService clipboard) {
        if (_entry.Entry is AddressTableEntry entry) {
            IDataValue? value = entry.Value;
            if (value != null) {
                string text = DataValueUtils.GetStringFromDataValue(entry, value);
                try {
                    await clipboard.SetTextAsync(text);
                }
                catch (Exception ex) {
                    await IMessageDialogService.Instance.ShowMessage("Clipboard unavailable", "Clipboard is in use. Value = " + text, ex.ToString());
                }
            }
            else {
                await clipboard.ClearAsync();
            }
        }
    }
}