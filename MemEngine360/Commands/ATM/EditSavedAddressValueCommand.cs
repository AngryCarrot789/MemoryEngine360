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
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands.ATM;

public class EditSavedAddressValueCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        ScanningProcessor? processor = null;
        if (IAddressTableEntryUI.DataKey.TryGetContext(e.ContextData, out IAddressTableEntryUI? theResult)) {
            if (!(theResult.Entry is AddressTableEntry)) {
                return Executability.Invalid;
            }

            processor = theResult.Entry.AddressTableManager!.MemoryEngine.ScanningProcessor;
        }

        if (IEngineUI.EngineUIDataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
            processor = ui.MemoryEngine.ScanningProcessor;
        }

        if (processor == null)
            return Executability.Invalid;
        
        if (processor.MemoryEngine.Connection == null)
            return Executability.ValidButCannotExecute;
        
        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        MemoryEngine? engine = null;
        List<AddressTableEntry> savedList = new List<AddressTableEntry>();
        if (IEngineUI.EngineUIDataKey.TryGetContext(e.ContextData, out IEngineUI? ui)) {
            savedList.AddRange(ui.AddressTableSelectionManager.SelectedItems.Where(x => x.Entry is AddressTableEntry).Select(x => (AddressTableEntry) x.Entry));
            engine = ui.MemoryEngine;
        }

        if (IAddressTableEntryUI.DataKey.TryGetContext(e.ContextData, out IAddressTableEntryUI? theResult)) {
            engine ??= theResult.Entry.AddressTableManager!.MemoryEngine;
            if (theResult.Entry is AddressTableEntry entry && !savedList.Contains(entry)) {
                savedList.Add(entry);
            }
        }

        if (engine == null || savedList.Count < 1) {
            return;
        }

        if (engine.Connection == null) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Not connected to a console");
            return;
        }

        DataType dataType = savedList[0].DataType;
        for (int i = 1; i < savedList.Count; i++) {
            if (dataType != savedList[i].DataType) {
                await IMessageDialogService.Instance.ShowMessage("Error", "Data types for the selected results are not all the same");
                return;
            }
        }

        int c = savedList.Count;
        AddressTableEntry lastResult = savedList[savedList.Count - 1];
        SingleUserInputInfo input = new SingleUserInputInfo(
            $"Change {c} value{Lang.S(c)}",
            $"Immediately change the value at {Lang.ThisThese(c)} address{Lang.Es(c)}", "Value",
            lastResult.Value != null ? DataValueUtils.GetStringFromDataValue(lastResult, lastResult.Value) : "") {
            Validate = (args) => {
                DataValueUtils.TryParseTextAsDataValue(args, dataType, lastResult.NumericDisplayType, lastResult.StringType, out _);
            }
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input) != true) {
            return;
        }

        using IDisposable? token = await engine.BeginBusyOperationActivityAsync("Edit scan result value");
        IConsoleConnection? conn;
        if (token == null || (conn = engine.Connection) == null) {
            return;
        }

        ValidationArgs args = new ValidationArgs(input.Text, new List<string>(), false);
        bool parsed = DataValueUtils.TryParseTextAsDataValue(args, dataType, lastResult.NumericDisplayType, lastResult.StringType, out IDataValue? value);
        Debug.Assert(parsed && value != null);

        using CancellationTokenSource cts = new CancellationTokenSource();
        await ActivityManager.Instance.RunTask(async () => {
            ActivityManager.Instance.GetCurrentProgressOrEmpty().SetCaptionAndText("Edit value", "Editing values");
            foreach (AddressTableEntry scanResult in savedList) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                await MemoryEngine.WriteDataValue(conn, scanResult.Address, value!);
                IDataValue actualValue = await MemoryEngine.ReadDataValue(conn, scanResult.Address, value);
                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    if (actualValue is DataValueString str) {
                        lastResult.StringType = str.StringType;
                        lastResult.StringLength = str.Value.Length;
                    }
                    else if (actualValue is DataValueByteArray arr) {
                        lastResult.ArrayLength = arr.Value.Length;
                    }

                    scanResult.DataType = actualValue.DataType;
                    scanResult.Value = actualValue;
                }, token: CancellationToken.None);
            }
        }, cts);
    }
}