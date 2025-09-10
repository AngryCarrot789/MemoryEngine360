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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Contexts;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Avalonia;

public partial class EngineWindow : DesktopWindow {
    public EngineWindow() {
        this.InitializeComponent();
    }

    protected override void OnOpenedCore() {
        base.OnOpenedCore();

        this.PART_MemEngineView.IsActivtyListVisible = false;
        using MultiChangeToken change = DataManager.GetContextData(this).BeginChange();
        change.Context.
               Set(MemoryEngine.EngineDataKey, this.PART_MemEngineView.MemoryEngine).
               Set(IEngineUI.DataKey, this.PART_MemEngineView);

        ((MemoryEngineManagerImpl) ApplicationPFX.GetService<MemoryEngineManager>()).OnEngineOpened(this.PART_MemEngineView);
    }

    protected override void OnClosed(EventArgs e) {
        base.OnClosed(e);

        ((MemoryEngineManagerImpl) ApplicationPFX.GetService<MemoryEngineManager>()).OnEngineClosed(this.PART_MemEngineView);

        DataManager.GetContextData(this).Remove(MemoryEngine.EngineDataKey, IEngineUI.DataKey);
    }

    protected override async Task<bool> OnClosingAsync(WindowCloseReason reason) {
        if (await base.OnClosingAsync(reason)) {
            return true;
        }

        MemoryEngine engine = this.PART_MemEngineView.MemoryEngine;
        engine.IsShuttingDown = true;
        ulong frame = engine.GetNextConnectionChangeFrame();
        await engine.BroadcastConnectionAboutToChange(frame);

        List<ActivityTask> tasks = ActivityManager.Instance.ActiveTasks.ToList();
        foreach (ActivityTask task in tasks) {
            task.TryCancel();
        }

        if (engine.ScanningProcessor.IsScanning) {
            ActivityTask? activity = engine.ScanningProcessor.ScanningActivity;
            if (activity != null && activity.TryCancel()) {
                await activity;

                if (engine.ScanningProcessor.IsScanning) {
                    await IMessageDialogService.Instance.ShowMessage("Busy", "Rare: scan still busy");
                    return true;
                }
            }
        }

        using (CancellationTokenSource cts = new CancellationTokenSource()) {
            // Grace period for all activities to become cancelled
            try {
                await Task.WhenAny(Task.Delay(1000, cts.Token), Task.Run(() => Task.WhenAll(tasks.Select(x => x.Task)), cts.Token));
                await cts.CancelAsync();
            }
            catch (OperationCanceledException) {
                // ignored
            }
        }

        IDisposable? token = await engine.BeginBusyOperationAsync(500);
        while (token == null) {
            MessageBoxInfo info = new MessageBoxInfo() {
                Caption = "Engine busy", Message = $"Cannot close window yet because the engine is still busy and cannot be shutdown safely.{Environment.NewLine}" +
                                                   "What do you want to do?",
                Buttons = MessageBoxButton.YesNo,
                DefaultButton = MessageBoxResult.Yes,
                YesOkText = "Wait for operations",
                NoText = "Force Close"
            };

            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(info);
            if (result != MessageBoxResult.Yes /* Yes == wait for ops */) {
                return false; // force close - let tcp things timeout
            }

            token = await engine.BeginBusyOperationActivityAsync("Safely closing window");
        }

        IConsoleConnection? connection = engine.Connection;
        try {
            if (connection != null) {
                engine.SetConnection(token, frame, null, ConnectionChangeCause.User);
                connection.Close();
            }
        }
        catch {
            // ignored
        }
        finally {
            token.Dispose();
        }

        {
            IDisposable? debuggerToken = await engine.ConsoleDebugger.BusyLock.BeginBusyOperationAsync(1000);
            while (debuggerToken == null) {
                MessageBoxInfo info = new MessageBoxInfo() {
                    Caption = "Debugger busy", Message = $"Cannot close window yet because the debugger is still busy and cannot be shutdown safely.{Environment.NewLine}" +
                                                         "What do you want to do?",
                    Buttons = MessageBoxButton.YesNo,
                    DefaultButton = MessageBoxResult.Yes,
                    YesOkText = "Wait for operations",
                    NoText = "Force Close"
                };

                MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(info);
                if (result != MessageBoxResult.Yes /* Yes == wait for ops */) {
                    return false; // force close - let tcp things timeout
                }

                debuggerToken = await engine.ConsoleDebugger.BusyLock.BeginBusyOperationActivityAsync("Safely closing window");
            }
            
            IConsoleConnection? debugConnection = engine.ConsoleDebugger.Connection;
            try {
                if (debugConnection != null) {
                    engine.ConsoleDebugger.SetConnection(debuggerToken, null);
                    debugConnection.Close();
                }
            }
            catch {
                // ignored
            }
            finally {
                debuggerToken.Dispose();
            }
        }
        
        return false;
    }
}