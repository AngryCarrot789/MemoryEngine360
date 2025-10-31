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

using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands.ATM;

public class EditSavedAddressValueCommand : BaseSavedAddressSelectionCommand {
    protected override Executability CanExecuteOverride(List<BaseAddressTableEntry> entries, MemoryEngine engine, CommandEventArgs e) {
        if (engine.Connection == null || engine.Connection.IsClosed)
            return Executability.ValidButCannotExecute;
        if (entries.All(x => x is AddressTableGroupEntry))
            return Executability.ValidButCannotExecute;
        return Executability.Valid;
    }

    protected override DisabledHintInfo? ProvideDisabledHintOverride(MemoryEngine engine, IContextData context, ContextRegistry? sourceContextMenu) {
        if (BaseMemoryEngineCommand.TryProvideNotConnectedDisabledHintInfo(engine, out DisabledHintInfo? hintInfo))
            return hintInfo;
        return null;
    }

    protected override async Task ExecuteCommandAsync(List<BaseAddressTableEntry> entries, MemoryEngine engine, CommandEventArgs e) {
        IConsoleConnection? connection = engine.Connection;
        if (connection == null) {
            await MessageBoxes.NoConnection.ShowMessage();
            return;
        }

        if (connection.IsClosed) {
            await MessageBoxes.ClosedConnection.ShowMessage();
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
        NumericDisplayType ndt = lastResult.NumericDisplayType;
        StringType strType = lastResult.StringType;
        IDataValue initialDataValue = lastResult.Value ?? IDataValue.CreateDefault(lastResult.DataType, lastResult.StringType);
        SingleUserInputInfo input = new SingleUserInputInfo(
            $"Change {count} value{Lang.S(count)}",
            $"Immediately change the value at {Lang.ThisThese(count)} address{Lang.Es(count)}", "Value",
            count > 1 ? "v" : DataValueUtils.GetStringFromDataValue(lastResult, initialDataValue)) {
            Validate = (args) => {
                if (dataType.IsNumeric()) {
                    DataValueUtils.TryParseNumericExpressionAsDataValue(args, dataType, ndt, out _, out _, initialDataValue);
                }
                else {
                    DataValueUtils.TryParseTextAsDataValue(args, dataType, ndt, strType, out _, canParseAsExpression: true);
                }
            },
            DebounceErrorsDelay = 300 // add a delay between parsing, to reduce typing lag due to expression parsing
        };

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input) != true) {
            return;
        }

        if ((connection = engine.Connection) == null || connection.IsClosed) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Console was disconnected while trying to edit values. Nothing was modified. Please reconnect.");
            return;
        }

        IDataValue? directValue = dataType.IsNumeric() ? null : DataValueUtils.ParseTextAsDataValue(input.Text, dataType, ndt, strType, canParseAsExpression: true);
        if (directValue == null) {
            directValue = DataValueUtils.ParseNumericExpressionAsDataValue(input.Text, dataType, ndt, out bool isInitialValueReferenced, initialDataValue);
            if (isInitialValueReferenced) {
                directValue = null;
            }
        }

        // TODO: use ConnectionAction

        ITopLevel? parentTopLevel = ITopLevel.FromContext(e.ContextData);
        using CancellationTokenSource cts = new CancellationTokenSource();
        Result<Optional<int>> result = await ActivityManager.Instance.RunTask(async () => {
            IActivityProgress progress = ActivityTask.Current.Progress;
            progress.Caption = "Edit Saved Result value";

            using IBusyToken? token = await engine.BusyLock.BeginBusyOperationFromActivity(new BusyTokenRequestFromActivity() {
                QuickReleaseIntention = true,
                ForegroundInfo = parentTopLevel != null ? new InForegroundInfo(parentTopLevel) : null
            });

            List<string> tmpErrors = new List<string>();
            if (token != null && engine.Connection != null) {
                ActivityTask.Current.Progress.SetCaptionAndText("Edit value", "Editing values");
                int success = 0;
                foreach (AddressTableEntry scanResult in savedList) {
                    ActivityManager.Instance.CurrentTask.ThrowIfCancellationRequested();
                    uint? address = await scanResult.MemoryAddress.TryResolveAddress(engine.Connection);
                    if (!address.HasValue)
                        continue; // pointer could not be resolved

                    success++;

                    IDataValue? setValue = null;
                    if (directValue != null) {
                        await MemoryEngine.WriteDataValue(engine.Connection, address.Value, directValue);
                        setValue = directValue;
                    }
                    else if (scanResult.Value != null && scanResult.DataType == dataType) {
                        tmpErrors.Clear();
                        if (DataValueUtils.TryParseNumericExpressionAsDataValue(new ValidationArgs(input.Text, tmpErrors, false), dataType, lastResult.NumericDisplayType, out IDataValue? value, out _, scanResult.Value)) {
                            await MemoryEngine.WriteDataValue(engine.Connection, address.Value, value);
                            setValue = value;
                        }
                    }

                    if (setValue != null) {
                        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                            switch (setValue) {
                                case DataValueString str:
                                    lastResult.StringType = str.StringType;
                                    lastResult.StringLength = str.Value.Length;
                                    break;
                                case DataValueByteArray arr: lastResult.ArrayLength = arr.Value.Length; break;
                            }

                            scanResult.DataType = setValue.DataType;
                            scanResult.Value = setValue;
                        }, token: CancellationToken.None);
                    }
                }

                return success;
            }

            return Optional<int>.Empty;
        }, cts);

        // result.Value will be Empty if we couldn't get the busy token
        if (!result.HasException && result.Value.HasValue && result.Value.Value != count) {
            await IMessageDialogService.Instance.ShowMessage("Not all values updated",
                $"Only {result.Value}/{count} were updated. The others' addresses could not be resolved",
                defaultButton: MessageBoxResult.OK,
                persistentDialogName: "dialog.CouldNotSetAllAddressValues");
        }
    }
}