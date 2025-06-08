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
using MemEngine360.PointerScanning;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands;

public class PointerScanCommand : Command {
    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!Debugger.IsAttached) {
            await IMessageDialogService.Instance.ShowMessage("Debug only for now", "Cannot use outside of debugging environment");
            return;
        }
        
        Debugger.Break();
        PointerScanner scanner = new PointerScanner() {
            // AddressableBase = 0x826E0000,
            // AddressableLength = 0x20B4DC4,
            AddressableBase = 0xC0000000,
            AddressableLength = 0x20000000,
            SearchAddress = 0xC386A988, 
            MaximumOffset = 0x2000,
            MaxDepth = 7
        };
        
        string? filePath = await IFilePickDialogService.Instance.OpenFile("Open memory dump file");
        if (filePath == null) {
            return;
        }

        // SingleUserInputInfo info = new SingleUserInputInfo("Base address", "What is the base address of the memory dump?", "00000000") {
        //     Validate = (b) => {
        //         if (!uint.TryParse(b.Input, NumberStyles.HexNumber, null, out _))
        //             b.Errors.Add("Invalid uint32. Value is hex");
        //     }
        // };
        //
        // if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) != true) {
        //     return;
        // }

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

        await scanner.LoadMemoryDump(filePath, 0xC0000000, result == MessageBoxResult.No);

        try {
            await scanner.Run();
        }
        finally {
            scanner.DisposeMemoryDump();
        }

        await IMessageDialogService.Instance.ShowMessage("Completed", "Finished scan: " + scanner.PointerCount);
    }
}