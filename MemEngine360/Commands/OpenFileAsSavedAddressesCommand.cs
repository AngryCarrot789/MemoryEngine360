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

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands;

public class OpenFileAsSavedAddressesCommand : Command {
    public readonly struct SavedAddress(bool IsRefreshActive, uint Address, string Desc, NumericDisplayType NDT, DataType DataType, StringType StringType, uint StringLength) {
        public bool IsRefreshActive { get; } = IsRefreshActive;
        public uint Address { get; } = Address;
        public string Desc { get; } = Desc;
        public NumericDisplayType NDT { get; } = NDT;
        public DataType DataType { get; } = DataType;
        public StringType StringType { get; } = StringType;
        public uint StringLength { get; } = StringLength;
    }

    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine)) {
            return;
        }

        string? path = await IFilePickDialogService.Instance.OpenFile("Open a CSV containing saved addresses", Filters.CsvAndAll);
        if (path == null) {
            return;
        }

        using CancellationTokenSource cts = new CancellationTokenSource();
        string[]? lines = await ActivityManager.Instance.RunTask(async () => {
            ActivityTask task = ActivityManager.Instance.CurrentTask;
            task.Progress.Caption = "Read addresses from CSV";
            task.Progress.Text = "Reading file...";
            task.Progress.IsIndeterminate = true;
            
            try {
                return await File.ReadAllLinesAsync(path, ActivityManager.Instance.CurrentTask.CancellationToken);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                await IMessageDialogService.Instance.ShowMessage("Error", "Error reading file", ex.GetToString());
                return null;
            }
        }, cts);

        if (lines == null || lines.Length < 1 || cts.IsCancellationRequested) {
            return;
        }

        List<string> trueLines = new List<string>(lines.Length);
        foreach (string line in lines) {
            if (!string.IsNullOrWhiteSpace(line))
                trueLines.Add(line);
        }

        if (trueLines.Count < 1) {
            return;
        }

        ActivityTask<List<SavedAddress>> task = ActivityManager.Instance.RunTask(async () => {
            ActivityTask task = ActivityManager.Instance.CurrentTask;
            task.Progress.Caption = "Read addresses from CSV";
            task.Progress.Text = "Deserializing...";
            task.Progress.IsIndeterminate = true;
            
            List<SavedAddress> list = new List<SavedAddress>();
            foreach (string line in trueLines) {
                task.CheckCancelled();

                string[] split = line.Split(',');
                if (split.Length != 7) {
                    return null;
                }

                bool IsRefreshActive = bool.Parse(split[0]);
                uint Address = uint.Parse(split[1], NumberStyles.HexNumber);
                string Desc = split[2];
                NumericDisplayType NDT = (NumericDisplayType) uint.Parse(split[3]);
                DataType DataType = (DataType) uint.Parse(split[4]);
                StringType StringType = (StringType) uint.Parse(split[5]);
                uint StringLength = uint.Parse(split[6]);

                list.Add(new SavedAddress(IsRefreshActive, Address, Desc, NDT, DataType, StringType, StringLength));
            }

            return list;
        }, cts);

        ObservableCollection<SavedAddressViewModel> saved = engine.ScanningProcessor.SavedAddresses;
        MessageBoxResult keepExistingResults = MessageBoxResult.Yes;
        if (saved.Count > 0) {
            MessageBoxInfo info = new MessageBoxInfo("Existing results", "Add the opened values to the end of your current saved addresses, or clear the existing ones?") {
                YesOkText = "Merge",
                NoText = "Clear existing",
                Buttons = MessageBoxButton.YesNoCancel,
                DefaultButton = MessageBoxResult.Yes
            };

            keepExistingResults = await IMessageDialogService.Instance.ShowMessage(info);
            if (keepExistingResults == MessageBoxResult.Cancel) {
                task.TryCancel();
                return;
            }
        }

        List<SavedAddress>? list = await task;
        if (list == null) {
            if (!task.IsCancelled) {
                await IMessageDialogService.Instance.ShowMessage("Error", "File contains invalid CSV entries for MemEngine360");
            }

            return;
        }
        
        if (keepExistingResults == MessageBoxResult.No) {
            engine.ScanningProcessor.SavedAddresses.Clear();
        }

        foreach (SavedAddress address in list) {
            saved.Add(new SavedAddressViewModel(engine.ScanningProcessor, address.Address) {
                DataType = address.DataType,
                Address = address.Address,
                Description = address.Desc,
                NumericDisplayType = address.NDT,
                StringType = address.StringType,
                StringLength = address.StringLength,
                IsAutoRefreshEnabled = address.IsRefreshActive,
            });
        }
    }
}

public class SaveFileAsSavedAddressesCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine)) {
            return Executability.Invalid;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine)) {
            return;
        }

        string? path = await IFilePickDialogService.Instance.SaveFile("Open a CSV containing saved addresses", Filters.CsvAndAll);
        if (path == null) {
            return;
        }

        List<SavedAddressViewModel> saved = engine.ScanningProcessor.SavedAddresses.ToList();
        if (saved.Count < 1) {
            return;
        }
        
        using CancellationTokenSource cts = new CancellationTokenSource();
        await ActivityManager.Instance.RunTask(async () => {
            ActivityTask task = ActivityManager.Instance.CurrentTask;
            task.Progress.Caption = "Save addresses to CSV";
            task.Progress.Text = "Serializing...";
            task.Progress.IsIndeterminate = true;

            List<string> lines = new List<string>();
            foreach (SavedAddressViewModel address in saved) {
                task.CheckCancelled();
                StringBuilder sb = new StringBuilder();
                sb.Append(address.IsAutoRefreshEnabled).Append(',').
                   Append(address.Address.ToString("X8")).Append(',').
                   Append(address.Description ?? "").Append(',').
                   Append((uint) address.NumericDisplayType).Append(',').
                   Append((uint) address.DataType).Append(',').
                   Append((uint) address.StringType).Append(',').
                   Append(address.StringLength);
                lines.Add(sb.ToString());
            }
            
            task.Progress.Text = "Writing to file...";
            await File.WriteAllLinesAsync(path, lines, task.CancellationToken);
        }, cts);
    }
}