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

using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Commands;

public class CopyScanResultsToClipboardCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!ScanResultViewModel.DataKey.TryGetContext(e.ContextData, out ScanResultViewModel? _)) {
            if (!IEngineUI.DataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
                return Executability.Invalid;
            }
            else if (ui.ScanResultSelectionManager.Count < 1) {
                return Executability.ValidButCannotExecute;
            }
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        MemoryEngine? engine = null;
        List<ScanResultViewModel> scanResults = new List<ScanResultViewModel>();
        if (IEngineUI.DataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
            scanResults.AddRange(ui.ScanResultSelectionManager.SelectedItems);
            engine = ui.MemoryEngine;
        }

        if (ScanResultViewModel.DataKey.TryGetContext(e.ContextData, out ScanResultViewModel? theResult)) {
            engine ??= theResult.ScanningProcessor.MemoryEngine;
            if (!scanResults.Contains(theResult))
                scanResults.Add(theResult);
        }

        if (engine == null || scanResults.Count < 1) {
            return;
        }

        MessageBoxInfo info = new MessageBoxInfo("Copy Rows", "Below is the selected rows") {
            Message = string.Join(Environment.NewLine, scanResults.Select(x => x.Address.ToString("X8") + "," + 
                                                                               DataValueUtils.GetStringFromDataValue(x, x.CurrentValue) + "," + 
                                                                               DataValueUtils.GetStringFromDataValue(x, x.PreviousValue) + "," + 
                                                                               DataValueUtils.GetStringFromDataValue(x, x.FirstValue))),
            Buttons = MessageBoxButton.OKCancel, DefaultButton = MessageBoxResult.OK,
            YesOkText = "Copy to Clipboard",
            CancelText = "Close"
        };

        if (await IMessageDialogService.Instance.ShowMessage(info) == MessageBoxResult.OK) {
            if (IDesktopWindow.DataKey.TryGetContext(e.ContextData, out IDesktopWindow? topLevel) && topLevel.ClipboardService != null) {
                await ActivityManager.Instance.RunTask(async () => {
                    ActivityManager.Instance.GetCurrentProgressOrEmpty().SetCaptionAndText("Clipboard", "Copying to clipboard");
                    CancellationToken token = ActivityManager.Instance.CurrentTask.CancellationToken;
                    Task mainTask = topLevel.ClipboardService.SetTextAsync(info.Message);
                    if (await Task.WhenAny(mainTask, Task.Delay(900, token)) != mainTask && !token.IsCancellationRequested) {
                        await IMessageDialogService.Instance.ShowMessage("Error", "Clipboard busy. Please try again later");
                    }
                });
            }
            else {
                await IMessageDialogService.Instance.ShowMessage("Error", "Could not access clipboard");
            }
        }
    }
}