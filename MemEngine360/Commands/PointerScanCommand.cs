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

using System.Globalization;
using MemEngine360.PointerScanning;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Commands;

public class PointerScanCommand : Command {
    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        PointerScanner scanner = new PointerScanner();
        string? filePath = await IFilePickDialogService.Instance.OpenFile("Open memory dump file");
        if (filePath != null) {
            SingleUserInputInfo info = new SingleUserInputInfo("Base address", "What is the base address of the memory dump?", "00000000") {
                Validate = (b) => {
                    if (!uint.TryParse(b.Input, NumberStyles.HexNumber, null, out _))
                        b.Errors.Add("Invalid uint32. Value is hex");
                }
            };

            if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) != true) {
                return;
            }

            MessageBoxInfo info2 = new MessageBoxInfo("Endianness", "Is the binary data big or little endian?") {
                YesOkText = "Big Endian",
                NoText = "Little Endian",
                Buttons = MessageBoxButton.YesNo,
                DefaultButton = MessageBoxResult.Yes
            };

            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(info2);
            if (result == MessageBoxResult.None) {
                return;
            }

            await scanner.LoadMemoryDump(filePath, uint.Parse(info.Text, NumberStyles.HexNumber), result == MessageBoxResult.Yes);

            await scanner.Run();
            
            await IMessageDialogService.Instance.ShowMessage("Completed", "Finished scan: " + scanner.PointerCount);
        }
    }
}