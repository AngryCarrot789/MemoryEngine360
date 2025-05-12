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

using System;
using System.Linq;
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

public partial class MemEngineWindow : DesktopWindow {
    public MemEngineWindow() {
        this.InitializeComponent();
    }

    protected override void OnOpenedCore() {
        base.OnOpenedCore();
        
        this.PART_MemEngineView.IsActivtyListVisible = false;
        using MultiChangeToken change = DataManager.GetContextData(this).BeginChange();
        change.Context.
               Set(MemoryEngine360.DataKey, this.PART_MemEngineView.MemoryEngine360).
               Set(IMemEngineUI.MemUIDataKey, this.PART_MemEngineView).
               Set(ILatestActivityView.LatestActivityDataKey, this.PART_MemEngineView);

        ((MemoryEngineManagerImpl) ApplicationPFX.Instance.ServiceManager.GetService<MemoryEngineManager>()).OnEngineOpened(this.PART_MemEngineView);
    }

    protected override void OnClosed(EventArgs e) {
        base.OnClosed(e);
        
        ((MemoryEngineManagerImpl) ApplicationPFX.Instance.ServiceManager.GetService<MemoryEngineManager>()).OnEngineClosed(this.PART_MemEngineView);

        using MultiChangeToken change = DataManager.GetContextData(this).BeginChange();
        change.Context.Remove(MemoryEngine360.DataKey, IMemEngineUI.MemUIDataKey, ILatestActivityView.LatestActivityDataKey);
    }

    protected override async Task<bool> OnClosingAsync(WindowCloseReason reason) {
        if (await base.OnClosingAsync(reason)) {
            return true;
        }
        
        foreach (ActivityTask task in ActivityManager.Instance.ActiveTasks.ToList()) {
            task.TryCancel();
        }

        MemoryEngine360 engine = this.PART_MemEngineView.MemoryEngine360;
        engine.IsShuttingDown = true;
        if (engine.ScanningProcessor.IsScanning) {
            ActivityTask? activity = engine.ScanningProcessor.ScanningActivity;
            if (activity != null && activity.TryCancel()) {
                await activity;

                if (engine.ScanningProcessor.IsScanning) {
                    await IMessageDialogService.Instance.ShowMessage("Busy", "Rare: still busy. Please wait for scan to complete");
                    return true;
                }
            }
        }

        // Grace period for all activities to become cancelled
        await Task.Delay(100);

        IDisposable? token = engine.BeginBusyOperation();
        while (token == null) {
            MessageBoxInfo info = new MessageBoxInfo() {
                Caption = "Engine busy", Message = $"Cannot close window yet because the engine is still busy and cannot be shutdown safely.{Environment.NewLine}" +
                                                   "What do you want to do?",
                Buttons = MessageBoxButton.YesNoCancel,
                DefaultButton = MessageBoxResult.Yes,
                YesOkText = "Wait for operations",
                NoText = "Force Close",
                CancelText = "Cancel"
            };

            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(info);
            switch (result) {
                case MessageBoxResult.None:
                case MessageBoxResult.Cancel:
                    return true; // stop window closing
                case MessageBoxResult.No: return false; // let TCP pipes auto-timeout
                default:                  break; // continue loop
            }

            token = await engine.BeginBusyOperationActivityAsync("Safely closing window");
        }

        IConsoleConnection? connection = engine.Connection;
        try {
            if (connection != null) {
                connection.Close();
                engine.SetConnection(token, null, ConnectionChangeCause.User);
            }
        }
        finally {
            token.Dispose();
        }

        return false;
    }
}