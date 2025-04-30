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

using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Commands;

public class CopyScanResultsToClipboardCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ScanResultViewModel.DataKey.TryGetContext(e.ContextData, out ScanResultViewModel? _)) {
            if (!IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
                return Executability.Invalid;
            }
            else if (ui.ScanResultSelectionManager.Count < 1) {
                return Executability.ValidButCannotExecute;
            }
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        MemoryEngine360? memoryEngine360 = null;
        List<ScanResultViewModel> scanResults = new List<ScanResultViewModel>();
        if (IMemEngineUI.DataKey.TryGetContext(e.ContextData, out IMemEngineUI? ui)) {
            scanResults.AddRange(ui.ScanResultSelectionManager.SelectedItems);
            memoryEngine360 = ui.MemoryEngine360;
        }

        if (ScanResultViewModel.DataKey.TryGetContext(e.ContextData, out ScanResultViewModel? theResult)) {
            memoryEngine360 ??= theResult.ScanningProcessor.MemoryEngine360;
            if (!scanResults.Contains(theResult))
                scanResults.Add(theResult);
        }

        if (memoryEngine360 == null || scanResults.Count < 1) {
            return;
        }

        MessageBoxInfo info = new MessageBoxInfo("Copy Rows", "Below is the selected rows") {
            Message = string.Join(Environment.NewLine, scanResults.Select(x => x.Address.ToString("X8") + "," + x.CurrentValue + "," + x.PreviousValue + "," + x.FirstValue)),
            Buttons = MessageBoxButton.OKCancel, DefaultButton = MessageBoxResult.OK,
            YesOkText = "Copy to Clipboard",
            CancelText = "Close"
        };

        if (await IMessageDialogService.Instance.ShowMessage(info) == MessageBoxResult.OK) {
            if (ITopLevel.DataKey.TryGetContext(e.ContextData, out ITopLevel? topLevel) && topLevel.ClipboardService != null) {
                await ActivityManager.Instance.RunTask(async () => {
                    ActivityManager.Instance.GetCurrentProgressOrEmpty().SetCaptionAndText("Clipboard", "Copying to clipboard");
                    CancellationToken token = ActivityManager.Instance.CurrentTask.CancellationToken;
                    Task mainTask = topLevel.ClipboardService.SetTextAsync(info.Message);
                    if (await Task.WhenAny(mainTask, Task.Delay(2000, token)) != mainTask && !token.IsCancellationRequested) {
                        await IMessageDialogService.Instance.ShowMessage("Error", "Clipboard busy. Please try again later");
                    }
                });
            }
            else {
                await IMessageDialogService.Instance.ShowMessage("Error", "Could not access clipboard. Fucking Avalonia");
            }
        }
    }
}