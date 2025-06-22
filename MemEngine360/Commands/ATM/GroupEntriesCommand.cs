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

using System.Diagnostics;
using MemEngine360.Engine;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands.ATM;

public class GroupEntriesCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!IEngineUI.DataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
            return Executability.Invalid;
        }

        List<BaseAddressTableEntry> list = ui.AddressTableSelectionManager.SelectedItems.Select(x => x.Entry).ToList();
        if (list.Count < 1) {
            return Executability.ValidButCannotExecute;
        }

        if (!BaseAddressTableEntry.CheckHaveParentsAndAllMatch(list, out AddressTableGroupEntry? parent)) {
            return Executability.ValidButCannotExecute;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!IEngineUI.DataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
            return;
        }

        List<BaseAddressTableEntry> list = ui.AddressTableSelectionManager.SelectedItems.Select(x => x.Entry).ToList();
        if (list.Count < 1) {
            return;
        }

        AddressTableGroupEntry? firstParent = list[0].Parent;
        if (firstParent == null)
            throw new Exception("Program corrupted");

        int minIndex = firstParent.IndexOf(list[0]);
        for (int i = 1; i < list.Count; i++) {
            if (firstParent != list[i].Parent) {
                await IMessageDialogService.Instance.ShowMessage("Cannot group", "The selected rows must all be within the same group, or be root rows");
                return;
            }

            minIndex = Math.Min(minIndex, firstParent.IndexOf(list[i]));
        }

        Debug.Assert(minIndex != -1);
        firstParent.RemoveEntries(list);

        AddressTableGroupEntry newEntry = new AddressTableGroupEntry();
        newEntry.AddEntries(list);
        
        firstParent.InsertEntry(minIndex, newEntry);
    }
}