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
using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands.ATM;

public class EditSavedAddressValueCommand : BaseSavedAddressSelectionCommand {
    protected override Executability CanExecuteOverride(List<BaseAddressTableEntry> entries, MemoryEngine engine, CommandEventArgs e) {
        IConsoleConnection? connection = engine.Connection;
        if (connection == null || connection.IsClosed) {
            return Executability.ValidButCannotExecute;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(List<BaseAddressTableEntry> entries, MemoryEngine engine, CommandEventArgs e) {
        IConsoleConnection? connection = engine.Connection;
        if (connection == null) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Not connected to a console");
            return;
        }
        
        if (connection.IsClosed) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Connection is no longer connected. Please reconnect");
            return;
        }

        List<AddressTableEntry> savedList = entries.OfType<AddressTableEntry>().ToList();
        if (savedList.Count < 1) {
            return;
        }

        DataType dataType = savedList[0].DataType;
        int count = savedList.Count;
        for (int i = 1; i < count; i++) {
            if (dataType != savedList[i].DataType) {
                await IMessageDialogService.Instance.ShowMessage("Error", "Data types for the selected results are not all the same");
                return;
            }
        }

        AddressTableEntry lastResult = savedList[count - 1];
        SingleUserInputInfo input = new SingleUserInputInfo(
            $"Change {count} value{Lang.S(count)}",
            $"Immediately change the value at {Lang.ThisThese(count)} address{Lang.Es(count)}", "Value",
            lastResult.Value != null ? DataValueUtils.GetStringFromDataValue(lastResult, lastResult.Value) : "") {
            Validate = (args) => {
                DataValueUtils.TryParseTextAsDataValue(args, dataType, lastResult.NumericDisplayType, lastResult.StringType, out _);
            }
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input, ITopLevel.FromContext(e.ContextData)) != true) {
            return;
        }

        using IDisposable? token = await engine.BeginBusyOperationActivityAsync("Edit scan result value");
        if (token == null) {
            return;
        }

        if ((connection = engine.Connection) == null || connection.IsClosed) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Console was disconnected while trying to edit values. Nothing was modified. Please reconnect.");
            return;
        }

        ValidationArgs args = new ValidationArgs(input.Text, new List<string>(), false);
        bool parsed = DataValueUtils.TryParseTextAsDataValue(args, dataType, lastResult.NumericDisplayType, lastResult.StringType, out IDataValue? value);
        Debug.Assert(parsed && value != null);

        using CancellationTokenSource cts = new CancellationTokenSource();
        Result<int> result = await ActivityManager.Instance.RunTask(async () => {
            ActivityManager.Instance.GetCurrentProgressOrEmpty().SetCaptionAndText("Edit value", "Editing values");
            int success = 0;
            foreach (AddressTableEntry scanResult in savedList) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                uint? address = await scanResult.MemoryAddress.TryResolveAddress(connection);
                if (!address.HasValue)
                    continue; // pointer could not be resolved

                success++;
                await MemoryEngine.WriteDataValue(connection, address.Value, value);
                IDataValue actualValue = await MemoryEngine.ReadDataValue(connection, address.Value, value);
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

            return success;
        }, cts);

        if (!result.HasException && result.Value != count) {
            await IMessageDialogService.Instance.ShowMessage("Not all values updated",
                $"Only {result.Value}/{count} were updated. The others' addresses could not be resolved",
                defaultButton: MessageBoxResult.OK,
                persistentDialogName: "dialog.CouldNotSetAllAddressValues");
        }
    }
}