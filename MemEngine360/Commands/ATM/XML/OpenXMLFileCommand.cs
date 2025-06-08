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
using MemEngine360.Engine;
using MemEngine360.Engine.SavedAddressing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands.ATM.XML;

public class OpenXMLFileCommand : Command {
    public static readonly XmlSerializer XmlGroupSerializer = new XmlSerializer(typeof(XmlAddressEntryGroup));
    
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngine.EngineDataKey.TryGetContext(e.ContextData, out MemoryEngine? engine)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine.EngineDataKey.TryGetContext(e.ContextData, out MemoryEngine? engine)) {
            return;
        }
        
        AddressTableGroupEntry rootEntry = engine.AddressTableManager.RootEntry;
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
            await IMessageDialogService.Instance.ShowMessage("Error", "Error deserializing file", ex.GetToString());
            return;
        }

        if (rootGroup == null) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Failed to deserialize object... it was null...");
            return;
        }
        
        if (rootEntry.Items.Count > 0) {
            MessageBoxInfo info = new MessageBoxInfo("Existing results", "Add the opened values to the end of your current saved addresses, or clear the existing ones?") {
                YesOkText = "Merge",
                NoText = "Clear existing",
                Buttons = MessageBoxButton.YesNoCancel,
                DefaultButton = MessageBoxResult.Yes
            };

            if ((keepExistingResults = await IMessageDialogService.Instance.ShowMessage(info)) == MessageBoxResult.Cancel || keepExistingResults == MessageBoxResult.None) {
                return;
            }
        }

        if (keepExistingResults == MessageBoxResult.No) {
            rootEntry.Clear();
        }
        
        AddToEntry(engine.ScanningProcessor, rootGroup, rootEntry);
    }

    private static void AddToEntry(ScanningProcessor processor, XmlAddressEntryGroup group, AddressTableGroupEntry entry) {
        foreach (XmlBaseAddressEntry item in group.Items) {
            if (item is XmlAddressEntryGroup subGroup) {
                AddressTableGroupEntry subEntry = new AddressTableGroupEntry(subGroup.GroupAddress, subGroup.IsAddressAbsolute) {
                    Description = subGroup.Description
                };
                
                entry.AddEntry(subEntry);
                AddToEntry(processor, subGroup, subEntry);
            }
            else {
                XmlAddressEntry theEntry = (XmlAddressEntry) item;
                entry.AddEntry(new AddressTableEntry(processor, theEntry.Address, theEntry.IsAddressAbsolute) {
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