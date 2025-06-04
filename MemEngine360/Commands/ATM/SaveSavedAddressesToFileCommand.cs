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

using System.Text;
using MemEngine360.Commands.ATM.XML;
using MemEngine360.Engine;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands.ATM;

public class SaveSavedAddressesToFileCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine)) {
            return;
        }

        MessageBoxInfo info = new MessageBoxInfo("File type", "What type of file to save as? Note, CSV does not support groups") {
            YesOkText = "XML", NoText = "CSV", DefaultButton = MessageBoxResult.Yes, Buttons = MessageBoxButton.YesNoCancel
        };
        
        MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(info);
        if (result == MessageBoxResult.Cancel || result == MessageBoxResult.None) {
            return;
        }

        if (result == MessageBoxResult.Yes) {
            string? path = await IFilePickDialogService.Instance.SaveFile("Save addresses to XML", Filters.XmlAndAll);
            if (path == null) {
                return;
            }

            await ActivityManager.Instance.RunTask(async () => {
                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = "Save addresses to XML";
                task.Progress.Text = "Serializing...";
                task.Progress.IsIndeterminate = true;
                try {
                    using MemoryStream stream = new MemoryStream();
                    stream.Capacity = 4096;
                    
                    XmlAddressEntryGroup rootGroup = new XmlAddressEntryGroup();
                    AddToGroup(engine.AddressTableManager.RootEntry, rootGroup);
                    OpenXMLFileCommand.XmlGroupSerializer.Serialize(stream, rootGroup);
                    
                    await File.WriteAllBytesAsync(path, stream.ToArray());
                }
                catch (Exception ex) {
                    await IMessageDialogService.Instance.ShowMessage("Error", "Error serializing", ex.GetToString());
                }
            });
        }
        else {
            string? path = await IFilePickDialogService.Instance.SaveFile("Open a CSV containing saved addresses", Filters.CsvAndAll);
            if (path == null) {
                return;
            }

            List<AddressTableEntry> saved = engine.AddressTableManager.GetAllAddressEntries().ToList();
            if (saved.Count < 1) {
                return;
            }

            using CancellationTokenSource cts = new CancellationTokenSource();
            await ActivityManager.Instance.RunTask(async () => {
                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = "Save addresses to CSV";
                task.Progress.Text = "Serializing...";
                task.Progress.IsIndeterminate = true;

                List<string> lines = new List<string>();
                foreach (AddressTableEntry address in saved) {
                    task.CheckCancelled();
                    StringBuilder sb = new StringBuilder();
                    sb.Append(address.IsAutoRefreshEnabled).Append(',').
                       Append(address.Address.ToString("X8")).Append(',').
                       Append(address.Description ?? "").Append(',').
                       Append((uint) address.NumericDisplayType).Append(',').
                       Append((uint) address.DataType).Append(',').
                       Append((uint) address.StringType).Append(',').
                       Append(address.StringLength);
                    lines.Add(sb.ToString());
                }

                task.Progress.Text = "Writing to file...";
                await File.WriteAllLinesAsync(path, lines, task.CancellationToken);
            }, cts);
        }
    }

    private static void AddToGroup(AddressTableGroupEntry entry, XmlAddressEntryGroup group) {
        foreach (BaseAddressTableEntry item in entry.Items) {
            if (item is AddressTableGroupEntry subEntry1) {
                XmlAddressEntryGroup subGroup = new XmlAddressEntryGroup() {
                    Description = subEntry1.Description,
                    GroupAddress = subEntry1.GroupAddress,
                    IsAddressAbsolute = subEntry1.IsAddressAbsolute,
                };

                group.Items.Add(subGroup);
                AddToGroup(subEntry1, subGroup);
            }
            else {
                AddressTableEntry subEntry2 = (AddressTableEntry) item;
                group.Items.Add(new XmlAddressEntry() {
                    DataType = subEntry2.DataType,
                    Address = subEntry2.Address,
                    IsAddressAbsolute = subEntry2.IsAddressAbsolute,
                    Description = subEntry2.Description,
                    NumericDisplayType = subEntry2.NumericDisplayType,
                    StringType = subEntry2.StringType,
                    StringLength = subEntry2.StringLength,
                    IsRefreshActive = subEntry2.IsAutoRefreshEnabled,
                });
            }
        }
    }
}