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
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.View;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils;

namespace MemEngine360.Commands;

public class EditScanResultValueCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        ScanningProcessor processor;
        if (!ScanResultViewModel.DataKey.TryGetContext(e.ContextData, out ScanResultViewModel? result)) {
            if (!MemoryEngine.EngineDataKey.TryGetContext(e.ContextData, out MemoryEngine? engine)) {
                return Executability.Invalid;
            }

            processor = engine.ScanningProcessor;
        }
        else {
            processor = result.ScanningProcessor;
        }

        if (processor.MemoryEngine.Connection == null) {
            return Executability.ValidButCannotExecute;
        }

        return Executability.Valid;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        List<ScanResultViewModel> scanResults = new List<ScanResultViewModel>();
        if (MemoryEngine.EngineDataKey.TryGetContext(e.ContextData, out MemoryEngine? engine)) {
            scanResults.AddRange(MemoryEngineViewState.GetInstance(engine).SelectedScanResults.SelectedItems);
        }

        if (ScanResultViewModel.DataKey.TryGetContext(e.ContextData, out ScanResultViewModel? theResult)) {
            engine ??= theResult.ScanningProcessor.MemoryEngine;
            if (!scanResults.Contains(theResult)) {
                scanResults.Add(theResult);
            }
        }

        if (engine == null || scanResults.Count < 1) {
            return;
        }

        if (engine.Connection == null) {
            await IMessageDialogService.Instance.ShowMessage(StandardEngineMessages.Caption_NoConnection, StandardEngineMessages.Message_NoConnection);
            return;
        }

        if (engine.Connection.IsClosed) {
            await IMessageDialogService.Instance.ShowMessage(StandardEngineMessages.Caption_ConnectionClosed, StandardEngineMessages.Message_ConnectionClosed);
            return;
        }

        DataType dataType = scanResults[0].DataType;
        for (int i = 1; i < scanResults.Count; i++) {
            if (dataType != scanResults[i].DataType) {
                await IMessageDialogService.Instance.ShowMessage("Error", "Data types for the selected results are not all the same");
                return;
            }
        }

        int c = scanResults.Count;
        ScanResultViewModel lastResult = scanResults[scanResults.Count - 1];
        SingleUserInputInfo input = new SingleUserInputInfo(
            $"Change {c} value{Lang.S(c)}",
            $"Immediately change the value at {Lang.ThisThese(c)} address{Lang.Es(c)}", "Value",
            DataValueUtils.GetStringFromDataValue(lastResult, lastResult.CurrentValue)) {
            Validate = (args) => {
                DataValueUtils.TryParseTextAsDataValue(args, dataType, lastResult.NumericDisplayType, lastResult.StringType, out _, canParseAsExpression: true);
            },
            DebounceErrorsDelay = 300 // add a delay between parsing, to reduce typing lag due to expression parsing
        };

        ITopLevel? topLevel = TopLevelContextUtils.GetTopLevelFromContext();
        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(input) != true) {
            return;
        }

        IDataValue value = DataValueUtils.ParseTextAsDataValue(input.Text, dataType, lastResult.NumericDisplayType, lastResult.StringType, canParseAsExpression: true);
        Debug.Assert(dataType == value.DataType);

        using CancellationTokenSource cts = new CancellationTokenSource();
        await ActivityManager.Instance.RunTask(async () => {
            ActivityTask.Current.Progress.SetCaptionAndText("Edit scan result value", "Editing values");
            using IBusyToken? token = await engine.BusyLock.BeginBusyOperationFromActivity(new BusyTokenRequestFromActivity() {
                QuickReleaseIntention = true,
                ForegroundInfo = topLevel != null ? new InForegroundInfo(topLevel) : null
            });
            
            if (token == null || engine.Connection == null) {
                return;
            }

            foreach (ScanResultViewModel scanResult in scanResults) {
                ActivityManager.Instance.CurrentTask.ThrowIfCancellationRequested();
                await MemoryEngine.WriteDataValue(engine.Connection, scanResult.Address, value!);
                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    scanResult.CurrentValue = value;
                });
            }
        }, cts);
    }
}