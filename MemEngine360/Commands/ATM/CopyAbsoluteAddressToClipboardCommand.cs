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
using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.Engine.View;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands.ATM;

public class CopyAbsoluteAddressToClipboardCommand : BaseCopyAddressTableEntryCommand {
    protected override async Task Copy(List<BaseAddressTableEntry> entries, MemoryEngineViewState engineVs, IClipboardService clipboard) {
        StringJoiner sj = new StringJoiner(Environment.NewLine);
        foreach (BaseAddressTableEntry entry in entries) {
            if (entry is AddressTableEntry ate) {
                string addrText = (await MemoryAddressUtils.TryResolveAddressFromATE(ate))?.ToString("X8") ?? "NULL";
                sj.Append(addrText);
            }
        }
        
        try {
            await clipboard.SetTextAsync(sj.ToString());
        }
        catch {
            await IMessageDialogService.Instance.ShowMessage("Clipboard unavailable", $"Clipboard is in use. Value(s) = {Environment.NewLine}{sj}");
        }
    }
}