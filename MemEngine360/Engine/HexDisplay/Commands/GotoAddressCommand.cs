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
using AvaloniaHex.Core.Document;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.Engine.HexDisplay.Commands;

public class GotoAddressCommand : BaseHexEditorCommand {
    // 83551E10, Primary Reserve Ammo, Int32
    // 83551E4C, Primary Ammo,         Int32
    // 83551F98, Crosshair size,       Float
    // 8354AA08, Enemy Team Score,     Int32
    
    protected override async Task ExecuteCommandAsync(IHexDisplayView view, HexDisplayInfo info, CommandEventArgs e) {
        SingleUserInputInfo singleInfo = new SingleUserInputInfo("Go to address", "Specify an address to scroll to", "Address (hex)", (view.CaretLocation.ByteIndex + view.CurrentStartOffset).ToString("X8")) {
            Validate = (a) => {
                ulong len;
                if (!uint.TryParse(a.Input, NumberStyles.HexNumber, null, out uint addr))
                    a.Errors.Add("Invalid address");
                else if (addr >= (view.CurrentStartOffset + (len = view.DocumentLength)) || addr < view.CurrentStartOffset)
                    a.Errors.Add($"Address out of range. Document spans {view.CurrentStartOffset:X8} to {(view.CurrentStartOffset + len - 1):X8}");
            }
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(singleInfo) == true) {
            uint address = uint.Parse(singleInfo.Text, NumberStyles.HexNumber) - view.CurrentStartOffset;
            BitLocation caret = new BitLocation(address, view.CaretLocation.BitIndex);

            view.CaretLocation = caret;
            view.SelectionRange = new BitRange(caret, caret.AddBytes(1));
        }
    }
}