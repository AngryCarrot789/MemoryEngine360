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

using MemEngine360.Xbox360XDevkit.Modules.Models;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Xbox360XDevkit.Commands;

public class ShowModuleSectionInfoInDialogCommand : Command {
    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (XboxModuleSection.DataKey.TryGetContext(e.ContextData, out XboxModuleSection? section)) {
            await IMessageDialogService.Instance.ShowMessage("Module size", $"Base Address: {section.BaseAddress:X8} (hex){Environment.NewLine}" +
                                                                            $"Section Size: {section.Size:X} (hex){Environment.NewLine}" +
                                                                            $"Flags: {(uint) section.Flags} ({section.Flags.ToString()})");
        }
    }
}