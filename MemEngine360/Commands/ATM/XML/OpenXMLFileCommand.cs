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

using System.Xml.Serialization;
using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.Engine.View;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands.ATM.XML;

public class OpenXMLFileCommand : Command {
    public static readonly XmlSerializer XmlGroupSerializer = new XmlSerializer(typeof(XmlAddressEntryGroup));
    
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
        
        AddressTableGroupEntry rootEntry = engineVs.Engine.AddressTableManager.RootEntry;
        MessageBoxResult keepExistingResults = MessageBoxResult.Yes;
        string? path = await IFilePickDialogService.Instance.OpenFile("Open an XML containing saved addresses", Filters.XmlAndAll);
        if (path == null) {
            return;
        }

        XmlAddressEntryGroup? rootGroup;
        try {
            await using BufferedStream stream = new BufferedStream(File.OpenRead(path));
            rootGroup = (XmlAddressEntryGroup?) XmlGroupSerializer.Deserialize(stream);
        }
        catch (Exception ex) {
            await IMessageDialogService.Instance.ShowExceptionMessage("XML Error", "Error deserializing address table: " + ExceptionUtils.GetFullMessageChain(ex), ex);
            return;
        }

        if (rootGroup == null) {
            await IMessageDialogService.Instance.ShowMessage("Error", "XML deserialization resulted in a null object. Is the file empty?");
            return;
        }
        
        if (rootEntry.Items.Count > 0) {
            MessageBoxInfo info = new MessageBoxInfo("Existing results", "Add the opened values to the end of your current saved addresses, or clear the existing ones?") {
                YesOkText = "Merge",
                NoText = "Clear existing",
                Buttons = MessageBoxButtons.YesNoCancel,
                DefaultButton = MessageBoxResult.Yes,
                PersistentDialogName = "dialog.MergeOrClearExisting"
            };

            if ((keepExistingResults = await IMessageDialogService.Instance.ShowMessage(info)) == MessageBoxResult.Cancel || keepExistingResults == MessageBoxResult.None) {
                return;
            }
        }

        if (keepExistingResults == MessageBoxResult.No) {
            rootEntry.Clear();
        }

        AddressTableGroupEntry entries;
        try {
            AddToEntry(rootGroup, entries = new AddressTableGroupEntry());
        }
        catch (Exception ex) {
            AppLogger.Instance.WriteLine("Error deserializing address table");
            AppLogger.Instance.WriteLine(ex.GetToString());
            await IMessageDialogService.Instance.ShowMessage("XML Error", "Error deserializing address table." + Environment.NewLine + "See logs for more info", ExceptionUtils.GetFullMessageChain(ex));
            return;
        }

        foreach (BaseAddressTableEntry entry in entries.Items.ToList()) {
            entries.MoveEntryTo(entry, rootEntry);
        }
    }

    private static void AddToEntry(XmlAddressEntryGroup group, AddressTableGroupEntry entry) {
        foreach (XmlBaseAddressEntry item in group.Items) {
            if (item is XmlAddressEntryGroup subGroup) {
                AddressTableGroupEntry subEntry = new AddressTableGroupEntry() {
                    Description = subGroup.Description
                };
                
                entry.Items.Add(subEntry);
                AddToEntry(subGroup, subEntry);
            }
            else {
                XmlAddressEntry theEntry = (XmlAddressEntry) item;
                if (!MemoryAddressUtils.TryParse(theEntry.Address, out IMemoryAddress? address)) {
                    throw new Exception($"Invalid address: '{theEntry.Address}'");
                }
                
                entry.Items.Add(new AddressTableEntry() {
                    MemoryAddress = address,
                    DataType = theEntry.DataType,
                    Description = theEntry.Description,
                    NumericDisplayType = theEntry.NumericDisplayType,
                    StringType = theEntry.StringType,
                    StringLength = theEntry.StringLength,
                    IsAutoRefreshEnabled = theEntry.IsRefreshActive,
                });
            }
        }
    }
}