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

using System.Diagnostics;
using MemEngine360.Engine;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.Engine.View;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands.ATM;

public class GroupEntriesCommand : BaseSavedAddressSelectionCommand {
    protected override Executability CanExecuteOverride(List<BaseAddressTableEntry> entries, MemoryEngine engine, CommandEventArgs e) {
        Executability exec = base.CanExecuteOverride(entries, engine, e);
        if (exec != Executability.Valid) {
            return exec;
        }

        if (!BaseAddressTableEntry.CheckHaveParentsAndAllMatch(entries, out AddressTableGroupEntry? parent)) {
            return Executability.ValidButCannotExecute;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(List<BaseAddressTableEntry> entries, MemoryEngineViewState engineVs, CommandEventArgs e) {
        List<BaseAddressTableEntry> modelList = entries.ToList();

        AddressTableGroupEntry? firstParent = modelList[0].Parent;
        if (firstParent == null)
            throw new Exception("Program corrupted");

        int minIndex = firstParent.IndexOf(modelList[0]);
        for (int i = 1; i < modelList.Count; i++) {
            if (firstParent != modelList[i].Parent) {
                await IMessageDialogService.Instance.ShowMessage("Cannot group", "The selected rows must all be within the same group, or be root rows", defaultButton: MessageBoxResult.OK);
                return;
            }

            minIndex = Math.Min(minIndex, firstParent.IndexOf(modelList[i]));
        }

        engineVs.AddressTableSelectionManager.Clear();

        Debug.Assert(minIndex != -1);
        firstParent.Items.RemoveRange(modelList);

        AddressTableGroupEntry newEntry = new AddressTableGroupEntry();
        newEntry.Items.AddRange(modelList);

        firstParent.Items.Insert(minIndex, newEntry);

        engineVs.AddressTableSelectionManager.SetSelection(newEntry);
        engineVs.RaiseRequestFocusOnSavedAddress(newEntry);
    }
}