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

using MemEngine360.Engine.SavedAddressing;
using MemEngine360.Engine.View;
using PFXToolKitUI.Activities;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands.ATM.XML;

public class SaveSavedAddressesToFileCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngineViewState.DataKey.TryGetContext(e.ContextData, out MemoryEngineViewState? engineVs)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngineViewState.DataKey.TryGetContext(e.ContextData, out MemoryEngineViewState? engineVs)) {
            return;
        }

        string? path = await IFilePickDialogService.Instance.SaveFile("Save addresses to XML", Filters.XmlAndAll);
        if (path == null) {
            return;
        }

        await ActivityManager.Instance.RunTask(async () => {
            ActivityTask task = ActivityTask.Current;
            task.Progress.Caption = "Save addresses to XML";
            task.Progress.Text = "Serializing...";
            task.Progress.IsIndeterminate = true;
            try {
                using MemoryStream stream = new MemoryStream();
                stream.Capacity = 4096;

                XmlAddressEntryGroup rootGroup = new XmlAddressEntryGroup();
                AddToGroup(engineVs.Engine.AddressTableManager.RootEntry, rootGroup);
                OpenXMLFileCommand.XmlGroupSerializer.Serialize(stream, rootGroup);

                await File.WriteAllBytesAsync(path, stream.ToArray());
            }
            catch (Exception ex) {
                await IMessageDialogService.Instance.ShowMessage("Error", "Error serializing", ex.GetToString());
            }
        });
    }

    private static void AddToGroup(AddressTableGroupEntry entry, XmlAddressEntryGroup group) {
        foreach (BaseAddressTableEntry item in entry.Items) {
            switch (item) {
                case AddressTableGroupEntry subEntry1: {
                    XmlAddressEntryGroup subGroup = new XmlAddressEntryGroup() {
                        Description = subEntry1.Description
                    };

                    group.Items.Add(subGroup);
                    AddToGroup(subEntry1, subGroup);
                    break;
                }
                case AddressTableEntry subEntry2: {
                    group.Items.Add(new XmlAddressEntry() {
                        DataType = subEntry2.DataType,
                        Address = subEntry2.MemoryAddress.ToString(),
                        Description = subEntry2.Description,
                        NumericDisplayType = subEntry2.NumericDisplayType,
                        StringType = subEntry2.StringType,
                        StringLength = subEntry2.StringLength,
                        IsRefreshActive = subEntry2.IsAutoRefreshEnabled,
                    });
                    break;
                }
            }
        }
    }
}