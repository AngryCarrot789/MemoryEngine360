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
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Connections.Impl;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.ComboBoxes;
using PFXToolKitUI.Avalonia.Bindings.Enums;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Contexts;
using PFXToolKitUI.Avalonia.Interactivity.Selecting;
using PFXToolKitUI.Avalonia.Themes.Controls;
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils.Commands;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.Avalonia;

public partial class MainWindow : WindowEx, IMemEngineUI, ILatestActivityView {
    #region BINDERS

    // PFX framework uses binders to simplify "binding" model values to controls
    // and vice versa. There's a bunch of different binders that exist for us to use.

    private readonly IBinder<MemoryEngine360> connectedHostNameBinder =
        new EventPropertyBinder<MemoryEngine360>(
            nameof(MemoryEngine360.ConnectionChanged),
            (b) => {
                string text;

                // TODO: maybe implement a custom control that represents the connection state
                // Though I don't see a point ATM since RTM is the only thing we will probably use, 
                // since what else is there?
                // Unless a custom circuit that probes the memory exists and connects via serial port,
                // then I suppose we could just show COM5 or whatever
                if (b.Model.Connection is PhantomRTMConsoleConnection c) {
                    text = c.EndPoint is IPEndPoint endPoint ? endPoint.Address.MapToIPv4().ToString() : c.EndPoint!.ToString()!;
                }
                else {
                    text = "Disconnected";
                }

                b.Control.SetValue(TextBlock.TextProperty, text);
            }, null /* UI changes do not reflect back into models, so no updateModel */);

    // private readonly IBinder<MemoryEngine360> isBusyBinder = 
    //     new EventPropertyBinder<MemoryEngine360>(
    //         nameof(MemoryEngine360.IsBusyChanged), 
    //         (b) => b.Control.SetValue(IsVisibleProperty, b.Model.IsBusy), 
    //         null /* UI changes do not reflect back into models, so no updateModel */);

    private readonly IBinder<ScanningProcessor> isScanningBinder =
        new EventPropertyBinder<ScanningProcessor>(
            nameof(ScanningProcessor.IsScanningChanged),
            (b) => {
                MainWindow w = (MainWindow) b.Control;
                w.PART_Grid_ScanInput.IsEnabled = !b.Model.IsScanning;
                w.PART_Grid_ScanOptions.IsEnabled = !b.Model.IsScanning;
            });

    private readonly IBinder<ScanningProcessor> inputValueBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(TextBox.TextProperty, nameof(ScanningProcessor.InputAChanged), (b) => ((TextBox) b.Control).Text = b.Model.InputA, (b) => b.Model.InputA = ((TextBox) b.Control).Text ?? "");
    private readonly IBinder<ScanningProcessor> inputBetweenABinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(TextBox.TextProperty, nameof(ScanningProcessor.InputAChanged), (b) => ((TextBox) b.Control).Text = b.Model.InputA, (b) => b.Model.InputA = ((TextBox) b.Control).Text ?? "");
    private readonly IBinder<ScanningProcessor> inputBetweenBBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(TextBox.TextProperty, nameof(ScanningProcessor.InputBChanged), (b) => ((TextBox) b.Control).Text = b.Model.InputB, (b) => b.Model.InputB = ((TextBox) b.Control).Text ?? "");
    private readonly IBinder<ScanningProcessor> startAddressBinder = new EventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.StartAddressChanged), (b) => ((MainWindow) b.Control).PART_ScanOption_StartAddress.Content = b.Model.StartAddress.ToString("X"));
    private readonly IBinder<ScanningProcessor> addrLengthBinder = new EventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.ScanLengthChanged), (b) => ((MainWindow) b.Control).PART_ScanOption_Length.Content = b.Model.ScanLength.ToString("X"));
    private readonly IBinder<ScanningProcessor> pauseXboxBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.PauseConsoleDuringScanChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.PauseConsoleDuringScan, (b) => b.Model.PauseConsoleDuringScan = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> int_isHexBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.IsIntInputHexadecimalChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.IsIntInputHexadecimal, (b) => b.Model.IsIntInputHexadecimal = ((ToggleButton) b.Control).IsChecked == true);
    private readonly EventPropertyEnumBinder<FloatScanOption> floatScanModeBinder = new EventPropertyEnumBinder<FloatScanOption>(typeof(ScanningProcessor), nameof(ScanningProcessor.FloatScanModeChanged), (x) => ((ScanningProcessor) x).FloatScanOption, (x, v) => ((ScanningProcessor) x).FloatScanOption = v);
    private readonly EventPropertyEnumBinder<StringScanOption> stringScanModeBinder = new EventPropertyEnumBinder<StringScanOption>(typeof(ScanningProcessor), nameof(ScanningProcessor.StringScanModeChanged), (x) => ((ScanningProcessor) x).StringScanOption, (x, v) => ((ScanningProcessor) x).StringScanOption = v);
    private readonly ComboBoxToEventPropertyEnumBinder<DataType> dataTypeBinder = new ComboBoxToEventPropertyEnumBinder<DataType>(typeof(ScanningProcessor), nameof(ScanningProcessor.DataTypeChanged), (x) => ((ScanningProcessor) x).DataType, (x, y) => ((ScanningProcessor) x).DataType = y);
    private readonly ComboBoxToEventPropertyEnumBinder<NumericScanType> scanTypeBinder1 = new ComboBoxToEventPropertyEnumBinder<NumericScanType>(typeof(ScanningProcessor), nameof(ScanningProcessor.NumericScanTypeChanged), (x) => ((ScanningProcessor) x).NumericScanType, (x, y) => ((ScanningProcessor) x).NumericScanType = y);
    private readonly ComboBoxToEventPropertyEnumBinder<NumericScanType> scanTypeBinder2 = new ComboBoxToEventPropertyEnumBinder<NumericScanType>(typeof(ScanningProcessor), nameof(ScanningProcessor.NumericScanTypeChanged), (x) => ((ScanningProcessor) x).NumericScanType, (x, y) => ((ScanningProcessor) x).NumericScanType = y);
    private readonly AsyncRelayCommand editAddressRangeCommand;

    #endregion

    public MemoryEngine360 MemoryEngine360 { get; }

    public IListSelectionManager<ScanResultViewModel> ScanResultSelectionManager { get; }
    
    public IListSelectionManager<SavedAddressViewModel> SavedAddressesSelectionManager { get; }

    public string Activity {
        get => this.latestActivityText;
        set {
            this.latestActivityText = value;
            this.updateActivityText.InvokeAsync();
        }
    }

    private volatile string latestActivityText = "";
    private readonly RateLimitedDispatchAction updateActivityText;
    private ActivityTask? primaryActivity;

    public MainWindow() {
        this.InitializeComponent();
        this.updateActivityText = RateLimitedDispatchActionBase.ForDispatcherSync(() => {
            this.PART_LatestActivity.Text = this.latestActivityText;
        }, TimeSpan.FromMilliseconds(50));

        this.MemoryEngine360 = new MemoryEngine360();
        this.ScanResultSelectionManager = new DataGridSelectionManager<ScanResultViewModel>(this.PART_ScanListResults);
        this.SavedAddressesSelectionManager = new DataGridSelectionManager<SavedAddressViewModel>(this.PART_SavedAddressList);

        using (MultiChangeToken change = DataManager.GetContextData(this).BeginChange())
            change.Context.Set(MemoryEngine360.DataKey, this.MemoryEngine360).Set(IMemEngineUI.DataKey, this).Set(ILatestActivityView.DataKey, this);

        this.Activity = "Welcome to MemEngine360.";
        this.PART_SavedAddressList.ItemsSource = this.MemoryEngine360.ScanningProcessor.SavedAddresses;
        this.PART_ScanListResults.ItemsSource = this.MemoryEngine360.ScanningProcessor.ScanResults;
        this.floatScanModeBinder.Assign(this.PART_DTFloat_UseExactValue, FloatScanOption.UseExactValue);
        this.floatScanModeBinder.Assign(this.PART_DTFloat_Truncate, FloatScanOption.TruncateToQuery);
        this.floatScanModeBinder.Assign(this.PART_DTFloat_RoundToQuery, FloatScanOption.RoundToQuery);

        this.stringScanModeBinder.Assign(this.PART_DTString_ASCII, StringScanOption.ASCII);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF8, StringScanOption.UTF8);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF16, StringScanOption.UTF16);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF32, StringScanOption.UTF32);

        this.MemoryEngine360.ScanningProcessor.DataTypeChanged += p => {
            switch (p.DataType) {
                case DataType.Byte:
                case DataType.Int16:
                case DataType.Int32:
                case DataType.Int64: {
                    this.PART_ScanSettingsTabControl.SelectedIndex = 0;
                    break;
                }
                case DataType.Float:
                case DataType.Double: {
                    this.PART_ScanSettingsTabControl.SelectedIndex = 1;
                    break;
                }
                case DataType.String: {
                    this.PART_ScanSettingsTabControl.SelectedIndex = 2;
                    break;
                }
                default: throw new ArgumentOutOfRangeException();
            }

            this.UpdateUIForScanTypeAndDataType();
        };

        this.MemoryEngine360.ScanningProcessor.NumericScanTypeChanged += p => {
            this.UpdateUIForScanTypeAndDataType();
        };

        this.MemoryEngine360.ScanningProcessor.ScanResults.CollectionChanged += (sender, args) => {
            this.PART_Run_CountResults.Text = ((ObservableCollection<ScanResultViewModel>) sender!).Count.ToString();
        };

        this.editAddressRangeCommand = new AsyncRelayCommand(async () => {
            ScanningProcessor p = this.MemoryEngine360.ScanningProcessor;

            DoubleUserInputInfo info = new DoubleUserInputInfo(p.StartAddress.ToString("X"), p.ScanLength.ToString("X")) {
                Caption = "Edit start and end addresses",
                LabelA = "Start Address (hex)",
                LabelB = "Bytes to scan (hex)"
            };

            info.ValidateA = (e) => {
                if (!uint.TryParse(e.Input, NumberStyles.HexNumber, null, out _))
                    e.Errors.Add("Invalid memory address");
            };

            info.ValidateB = (e) => {
                if (!uint.TryParse(e.Input, NumberStyles.HexNumber, null, out _))
                    e.Errors.Add("Invalid unsigned integer");
            };

            // We don't need to unregister the handler because info will
            // get garbage collected soon, since it's not accessed outside this command
            DataParameterValueChangedEventHandler change = (_, owner) => UpdateMemoryRangeFooterText((DoubleUserInputInfo) owner);
            DoubleUserInputInfo.TextAParameter.AddValueChangedHandler(info, change);
            DoubleUserInputInfo.TextBParameter.AddValueChangedHandler(info, change);

            UpdateMemoryRangeFooterText(info);
            if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
                p.StartAddress = uint.Parse(info.TextA, NumberStyles.HexNumber);
                p.ScanLength = uint.Parse(info.TextB, NumberStyles.HexNumber);
                BasicApplicationConfiguration.Instance.StorageManager.SaveArea(BasicApplicationConfiguration.Instance);
            }
        });
    }

    private static void UpdateMemoryRangeFooterText(DoubleUserInputInfo theInfo) {
        const string prefix = "Scan Range (inclusive):";
        if (theInfo.TextErrorsA != null || theInfo.TextErrorsB != null) {
            theInfo.Footer = $"{prefix} <errors present>";
        }
        else {
            uint addr = uint.Parse(theInfo.TextA, NumberStyles.HexNumber);
            uint len = uint.Parse(theInfo.TextB, NumberStyles.HexNumber);
            theInfo.Footer = $"{prefix} {addr:X8} -> {(addr + (len - 1)):X8}";
        }
    }

    private void UpdateUIForScanTypeAndDataType() {
        ScanningProcessor sp = this.MemoryEngine360.ScanningProcessor;
        if (sp.NumericScanType == NumericScanType.Between && sp.DataType.IsNumeric()) {
            this.PART_Input_Value1.IsVisible = false;
            this.PART_Grid_Input_Between.IsVisible = true;
            this.PART_ValueOrBetweenTextBlock.Text = "Between";
            if (this.inputValueBinder.IsFullyAttached)
                this.inputValueBinder.Detach();

            if (!this.inputBetweenABinder.IsFullyAttached)
                this.inputBetweenABinder.Attach(this.PART_Input_BetweenA, sp);
            if (!this.inputBetweenBBinder.IsFullyAttached)
                this.inputBetweenBBinder.Attach(this.PART_Input_BetweenB, sp);
        }
        else {
            this.PART_Input_Value1.IsVisible = true;
            this.PART_Grid_Input_Between.IsVisible = false;
            this.PART_ValueOrBetweenTextBlock.Text = "Value";
            if (this.inputBetweenABinder.IsFullyAttached)
                this.inputBetweenABinder.Detach();

            if (this.inputBetweenBBinder.IsFullyAttached)
                this.inputBetweenBBinder.Detach();

            if (!this.inputValueBinder.IsFullyAttached)
                this.inputValueBinder.Attach(this.PART_Input_Value1, this.MemoryEngine360.ScanningProcessor);
        }
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);
        this.connectedHostNameBinder.Attach(this.PART_ConnectedHostName, this.MemoryEngine360);
        // this.isBusyBinder.Attach(this.PART_BusyIndicator, this.MemoryEngine360);
        this.isScanningBinder.Attach(this, this.MemoryEngine360.ScanningProcessor);
        this.startAddressBinder.Attach(this, this.MemoryEngine360.ScanningProcessor);
        this.addrLengthBinder.Attach(this, this.MemoryEngine360.ScanningProcessor);
        this.pauseXboxBinder.Attach(this.PART_ScanOption_PauseConsole, this.MemoryEngine360.ScanningProcessor);
        this.int_isHexBinder.Attach(this.PART_DTInt_IsHex, this.MemoryEngine360.ScanningProcessor);
        this.floatScanModeBinder.Attach(this.MemoryEngine360.ScanningProcessor);
        this.stringScanModeBinder.Attach(this.MemoryEngine360.ScanningProcessor);
        this.dataTypeBinder.Attach(this.PART_DataTypeCombo, this.MemoryEngine360.ScanningProcessor);
        this.scanTypeBinder1.Attach(this.PART_ScanTypeCombo1, this.MemoryEngine360.ScanningProcessor);
        this.scanTypeBinder2.Attach(this.PART_ScanTypeCombo2, this.MemoryEngine360.ScanningProcessor);
        this.PART_ActiveBackgroundTaskGrid.IsVisible = false;
        this.MemoryEngine360.ConnectionChanged += this.OnConnectionChanged;

        this.UpdateUIForScanTypeAndDataType();

        ActivityManager activityManager = ActivityManager.Instance;
        activityManager.TaskStarted += this.OnTaskStarted;
        activityManager.TaskCompleted += this.OnTaskCompleted;
    }

    private void OnConnectionChanged(MemoryEngine360 sender, IConsoleConnection? oldConn, IConsoleConnection? newConn, ConnectionChangeCause cause) {
        switch (cause) {
            case ConnectionChangeCause.User:
            case ConnectionChangeCause.Custom: {
                this.Activity = newConn != null ? "Connected to console" : "Disconnected from console";
                break;
            }
            case ConnectionChangeCause.ClosingWindow:  break;
            case ConnectionChangeCause.LostConnection: this.Activity = "Lost connection to console"; break;
            default:                                   throw new ArgumentOutOfRangeException(nameof(cause), cause, null);
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e) {
        base.OnUnloaded(e);
        this.connectedHostNameBinder.Detach();
        // this.isBusyBinder.Detach();
        this.isScanningBinder.Detach();
        this.startAddressBinder.Detach();
        this.addrLengthBinder.Detach();
        this.pauseXboxBinder.Detach();
        this.int_isHexBinder.Detach();
        this.floatScanModeBinder.Detach();
        this.stringScanModeBinder.Detach();
        this.dataTypeBinder.Detach();
        this.scanTypeBinder1.Detach();
        this.scanTypeBinder2.Detach();

        if (this.inputValueBinder.IsFullyAttached)
            this.inputValueBinder.Detach();
        if (this.inputBetweenABinder.IsFullyAttached)
            this.inputBetweenABinder.Detach();
        if (this.inputBetweenBBinder.IsFullyAttached)
            this.inputBetweenBBinder.Detach();
    }

    protected override async Task<bool> OnClosingAsync(WindowCloseReason reason) {
        if (this.MemoryEngine360.ScanningProcessor.IsScanning) {
            ActivityTask? activity = this.MemoryEngine360.ScanningProcessor.ScanningActivity;
            if (activity != null && activity.TryCancel()) {
                await activity;
                
                if (this.MemoryEngine360.ScanningProcessor.IsScanning) {
                    await IMessageDialogService.Instance.ShowMessage("Busy", "Rare: still busy. Please wait for scan to complete");
                    return true;
                }
            }
        }
        
        IConsoleConnection? connection = this.MemoryEngine360.Connection;
        if (connection != null && this.MemoryEngine360.IsConnectionBusy) {
            using CancellationTokenSource cts = new CancellationTokenSource();
            ActivityTask task = ActivityManager.Instance.RunTask(async () => {
                ActivityTask activity = ActivityManager.Instance.CurrentTask;
                activity.Progress.Caption = "Busy";
                activity.Progress.Text = "Waiting for operations to complete";
                activity.Progress.IsIndeterminate = true;
                
                await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    return this.MemoryEngine360.WaitAndDisconnectAsync(ConnectionChangeCause.ClosingWindow, activity.CancellationToken);
                });
            }, cts);

            await task;

            // there's a tiny window between cancellation signal and task actually exiting.
            // it's possible the connection was closed even when cancelled for a few microseconds or so
            if (this.MemoryEngine360.IsConnectionBusy) {
                MessageBoxInfo info = new MessageBoxInfo() {
                    Caption = "Engine busy", Message = "Engine is still busy elsewhere. Do you want to force close the window?",
                    Buttons = MessageBoxButton.YesNo, NoText = "No (do nothing)"
                };

                MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(info);
                if (result == MessageBoxResult.Yes) {
                    return false; // let TCP pipes auto-timeout
                }
                
                return true; // cancel window closing
            }
        }

        this.MemoryEngine360.SetConnection(null, ConnectionChangeCause.User);
        connection?.Dispose();
        return false;
    }

    #region Task Manager and Activity System

    private void OnTaskStarted(ActivityManager manager, ActivityTask task, int index) {
        if (this.primaryActivity == null || this.primaryActivity.IsCompleted) {
            this.SetActivityTask(task);
        }
    }

    private void OnTaskCompleted(ActivityManager manager, ActivityTask task, int index) {
        if (task == this.primaryActivity) {
            // try to access next task
            this.SetActivityTask(manager.ActiveTasks.Count > 0 ? manager.ActiveTasks[0] : null);
        }
    }

    private void SetActivityTask(ActivityTask? task) {
        IActivityProgress? prog = null;
        if (this.primaryActivity != null) {
            prog = this.primaryActivity.Progress;
            prog.TextChanged -= this.OnPrimaryActivityTextChanged;
            prog.CompletionState.CompletionValueChanged -= this.OnPrimaryActionCompletionValueChanged;
            prog.IsIndeterminateChanged -= this.OnPrimaryActivityIndeterminateChanged;
            if (this.primaryActivity.IsDirectlyCancellable)
                this.PART_CancelActivityButton.IsVisible = false;

            prog = null;
        }

        this.primaryActivity = task;
        if (task != null) {
            prog = task.Progress;
            prog.TextChanged += this.OnPrimaryActivityTextChanged;
            prog.CompletionState.CompletionValueChanged += this.OnPrimaryActionCompletionValueChanged;
            prog.IsIndeterminateChanged += this.OnPrimaryActivityIndeterminateChanged;
            if (task.IsDirectlyCancellable)
                this.PART_CancelActivityButton.IsVisible = true;

            this.PART_ActiveBackgroundTaskGrid.IsVisible = true;
        }
        else {
            this.PART_ActiveBackgroundTaskGrid.IsVisible = false;
        }

        this.PART_CancelActivityButton.IsEnabled = true;
        this.OnPrimaryActivityTextChanged(prog);
        this.OnPrimaryActionCompletionValueChanged(prog?.CompletionState);
        this.OnPrimaryActivityIndeterminateChanged(prog);
    }

    private void OnPrimaryActivityTextChanged(IActivityProgress? tracker) {
        ApplicationPFX.Instance.Dispatcher.Invoke(() => this.PART_TaskCaption.Text = tracker?.Text ?? "", DispatchPriority.Loaded);
    }

    private void OnPrimaryActionCompletionValueChanged(CompletionState? state) {
        ApplicationPFX.Instance.Dispatcher.Invoke(() => this.PART_ActiveBgProgress.Value = state?.TotalCompletion ?? 0.0, DispatchPriority.Loaded);
    }

    private void OnPrimaryActivityIndeterminateChanged(IActivityProgress? tracker) {
        ApplicationPFX.Instance.Dispatcher.Invoke(() => this.PART_ActiveBgProgress.IsIndeterminate = tracker?.IsIndeterminate ?? false, DispatchPriority.Loaded);
    }

    #endregion

    private void PART_ScanOption_StartAddress_OnDoubleTapped(object? sender, TappedEventArgs e) {
        this.editAddressRangeCommand.Execute(null);
    }

    private void PART_ScanOption_Length_OnDoubleTapped(object? sender, TappedEventArgs e) {
        this.editAddressRangeCommand.Execute(null);
    }

    private void PART_CancelActivityButton_OnClick(object? sender, RoutedEventArgs e) {
        this.primaryActivity?.TryCancel();
        ((Button) sender!).IsEnabled = false;
    }
}