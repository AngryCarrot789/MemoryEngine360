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
    protected override async Task ExecuteCommandAsync(IHexDisplayView view, HexDisplayInfo info, CommandEventArgs e) {
        SingleUserInputInfo singleInfo = new SingleUserInputInfo("Go to address", "Specify an address to scroll to", "Address (hex)", view.CaretLocation.ByteIndex.ToString("X8")) {
            Validate = (a) => {
                if (!uint.TryParse(a.Input, NumberStyles.HexNumber, null, out uint addr))
                    a.Errors.Add("Invalid address");
                else if (addr >= view.DocumentRange)
                    a.Errors.Add("Address out of range");
            }
        };
        
        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(singleInfo) == true) {
            uint address = uint.Parse(singleInfo.Text, NumberStyles.HexNumber);
            view.CaretLocation = new BitLocation(address, view.CaretLocation.BitIndex);
        }
    }
}