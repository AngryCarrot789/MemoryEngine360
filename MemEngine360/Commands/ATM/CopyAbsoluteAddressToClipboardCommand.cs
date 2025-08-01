﻿// 
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

using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands.ATM;

public class CopyAbsoluteAddressToClipboardCommand : BaseCopyAddressTableEntryCommand {
    protected override async Task Copy(IAddressTableEntryUI entry, IClipboardService clipboard) {
        if (!(entry.Entry is AddressTableEntry ate)) {
            return;
        }
        
        string addrText = (await MemoryAddressUtils.TryResolveAddressFromATE(ate))?.ToString("X8") ?? "nullptr";
        try {
            await clipboard.SetTextAsync(addrText);
        }
        catch (Exception ex) {
            await IMessageDialogService.Instance.ShowMessage("Clipboard unavailable", "Clipboard is in use. Address = " + addrText);
        }
    }
}