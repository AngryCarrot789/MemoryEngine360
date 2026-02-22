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

using MemEngine360.Engine.Scanners;
using MemEngine360.Engine.View;
using MemEngine360.XboxBase.Modules;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.XboxBase;

public class ShowModuleSectionInfoInDialogCommand : Command {
    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (ConsoleModuleSection.DataKey.TryGetContext(e.ContextData, out ConsoleModuleSection? section)) {
            string sizeKb = ValueScannerUtils.ByteFormatter.ToString(section.Size, false);

            string text = $"Name: {section.Name ?? "(unavailable)"}{Environment.NewLine}" +
                          $"Base Address: {section.BaseAddress:X8}{Environment.NewLine}" +
                          $"Section Size: {section.Size:X8} ({sizeKb}){Environment.NewLine}" +
                          $"Flags: {(uint) section.Flags} ({section.Flags.ToString()})";

            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(new MessageBoxInfo("Section Info", text) {
                // Cheesing it by using cancel as the OK button ;)
                Buttons = MessageBoxButtons.OKCancel,
                DefaultButton = MessageBoxResult.Cancel,
                YesOkText = "Copy to Scanner",
                CancelText = "OK"
            });

            if (result == MessageBoxResult.OK && MemoryEngineViewState.DataKey.TryGetContext(e.ContextData, out MemoryEngineViewState? engineVs)) {
                if (section.BaseAddress + section.Size >= section.BaseAddress) {
                    engineVs.Engine.ScanningProcessor.SetScanRange(section.BaseAddress, section.Size);
                }
            }
        }
    }
}