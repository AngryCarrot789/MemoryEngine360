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
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using MemEngine360.Avalonia.Resources.Icons;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Connections.XBOX;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Contexts;
using PFXToolKitUI.Avalonia.Interactivity.Selecting;
using PFXToolKitUI.Avalonia.Services;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Avalonia.Shortcuts.Avalonia;
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Themes;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Commands;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.Avalonia;

public partial class MemEngineView : WindowingContentControl, IMemEngineUI, ILatestActivityView {
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
                MemEngineView w = (MemEngineView) b.Control;
                w.PART_ScanOptionsControl.IsEnabled = !b.Model.IsScanning;
                w.PART_Grid_ScanOptions.IsEnabled = !b.Model.IsScanning;
            });

    private readonly IBinder<ScanningProcessor> startAddressBinder = new EventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.StartAddressChanged), (b) => ((MemEngineView) b.Control).PART_ScanOption_StartAddress.Content = $"0x{b.Model.StartAddress:X} ({b.Model.StartAddress.ToString()})");
    private readonly IBinder<ScanningProcessor> addrLengthBinder = new EventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.ScanLengthChanged), (b) => ((MemEngineView) b.Control).PART_ScanOption_Length.Content = $"0x{b.Model.ScanLength:X} ({b.Model.ScanLength.ToString()})");
    private readonly IBinder<ScanningProcessor> alignmentBinder = new EventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.AlignmentChanged), (b) => ((MemEngineView) b.Control).PART_ScanOption_Alignment.Content = b.Model.Alignment.ToString());
    private readonly IBinder<ScanningProcessor> pauseXboxBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.PauseConsoleDuringScanChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.PauseConsoleDuringScan, (b) => b.Model.PauseConsoleDuringScan = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> scanMemoryPagesBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.ScanMemoryPagesChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.ScanMemoryPages, (b) => b.Model.ScanMemoryPages = ((ToggleButton) b.Control).IsChecked == true);
    private readonly AsyncRelayCommand editAddressRangeCommand;
    private readonly AsyncRelayCommand editAlignmentCommand;

    #endregion

    public MemoryEngine360 MemoryEngine360 { get; }

    public TopLevelMenuRegistry TopLevelMenuRegistry { get; }

    public ContextEntryGroup RemoteCommandsContextEntry { get; }

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

    // used to remember the last selected data type when changing via the tab control
    private readonly ContextEntryGroup themesSubList;
    private ObservableItemProcessorIndexing<Theme>? themeListHandler;

    public MemEngineView() {
        this.InitializeComponent();

        this.TopLevelMenuRegistry = new TopLevelMenuRegistry();
        {
            ContextEntryGroup entry = new ContextEntryGroup("File");
            entry.Items.Add(new CommandContextEntry("commands.memengine.ConnectToConsoleCommand", "Connect to console...", icon: SimpleIcons.ConnectToConsoleIcon));
            entry.Items.Add(new CommandContextEntry("commands.memengine.TestShowMemoryCommand", "Test Hex editor"));
            entry.Items.Add(new SeparatorEntry());
            entry.Items.Add(new CommandContextEntry("commands.mainWindow.OpenEditorSettings", "Preferences"));
            this.TopLevelMenuRegistry.Items.Add(entry);
        }

        {
            this.RemoteCommandsContextEntry = new ContextEntryGroup("Remote Controls");
            this.TopLevelMenuRegistry.Items.Add(this.RemoteCommandsContextEntry);
        }

        this.themesSubList = new ContextEntryGroup("Themes");
        this.TopLevelMenuRegistry.Items.Add(this.themesSubList);

        {
            ContextEntryGroup entry = new ContextEntryGroup("About");
            entry.Items.Add(new CommandContextEntry("commands.application.AboutApplicationCommand", "About MemEngine360"));
            this.TopLevelMenuRegistry.Items.Add(entry);
        }

        this.PART_TopLevelMenu.TopLevelMenuRegistry = this.TopLevelMenuRegistry;

        this.updateActivityText = RateLimitedDispatchActionBase.ForDispatcherSync(() => {
            this.PART_LatestActivity.Text = this.latestActivityText;
        }, TimeSpan.FromMilliseconds(50));

        this.MemoryEngine360 = new MemoryEngine360();
        this.ScanResultSelectionManager = new DataGridSelectionManager<ScanResultViewModel>(this.PART_ScanListResults);
        this.SavedAddressesSelectionManager = new DataGridSelectionManager<SavedAddressViewModel>(this.PART_SavedAddressList);

        this.Activity = "Welcome to MemEngine360.";
        this.PART_SavedAddressList.ItemsSource = this.MemoryEngine360.ScanningProcessor.SavedAddresses;
        this.PART_ScanListResults.ItemsSource = this.MemoryEngine360.ScanningProcessor.ScanResults;

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

        this.editAlignmentCommand = new AsyncRelayCommand(async () => {
            ScanningProcessor p = this.MemoryEngine360.ScanningProcessor;
            SingleUserInputInfo info = new SingleUserInputInfo(p.Alignment.ToString("X")) {
                Caption = "Edit alignment",
                Message = "Alignment is the offset added to each memory address",
                Label = "Alignment (prefix with '0x' to parse as hex)",
                Validate = (e) => {
                    if (!NumberUtils.TryParseHexOrRegular<uint>(e.Input, out uint number)) {
                        e.Errors.Add("-".StartsWith(e.Input) ? "Alignment cannot be negative" : "Invalid unsigned integer");
                    }
                    else if (number == 0)
                        e.Errors.Add("Alignment cannot be zero!");
                }
            };

            static void UpdateFooter(SingleUserInputInfo inf) {
                if (inf.TextErrors != null) {
                    inf.Footer = "Cannot show examples: invalid alignment";
                }
                else {
                    int align = (int) NumberUtils.ParseHexOrRegular<uint>(inf.Text);
                    StringBuilder sb = new StringBuilder().Append(0);
                    for (int i = 1, j = align; i < 5; i++, j += align)
                        sb.Append(", ").Append(j);
                    inf.Footer = "We will scan " + sb.Append(", etc.");
                }
            }

            SingleUserInputInfo.TextParameter.AddValueChangedHandler(info, (parameter, owner) => UpdateFooter((SingleUserInputInfo) owner));
            UpdateFooter(info);
            if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
                p.Alignment = NumberUtils.ParseHexOrRegular<uint>(info.Text);
            }
        });

        this.PART_ScanOptionsControl.MemoryEngine360 = this.MemoryEngine360;
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

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);
        this.connectedHostNameBinder.Attach(this.PART_ConnectedHostName, this.MemoryEngine360);
        // this.isBusyBinder.Attach(this.PART_BusyIndicator, this.MemoryEngine360);
        this.isScanningBinder.Attach(this, this.MemoryEngine360.ScanningProcessor);
        this.startAddressBinder.Attach(this, this.MemoryEngine360.ScanningProcessor);
        this.addrLengthBinder.Attach(this, this.MemoryEngine360.ScanningProcessor);
        this.alignmentBinder.Attach(this, this.MemoryEngine360.ScanningProcessor);
        this.pauseXboxBinder.Attach(this.PART_ScanOption_PauseConsole, this.MemoryEngine360.ScanningProcessor);
        this.scanMemoryPagesBinder.Attach(this.PART_ScanOption_ScanMemoryPages, this.MemoryEngine360.ScanningProcessor);
        this.PART_ActiveBackgroundTaskGrid.IsVisible = false;
        this.MemoryEngine360.ConnectionChanged += this.OnConnectionChanged;

        this.themeListHandler = ObservableItemProcessor.MakeIndexable(ThemeManager.Instance.Themes, (sender, index, item) => {
            this.themesSubList.Items.Add(new SetThemeContextEntry(item));
        }, (sender, index, item) => {
            this.themesSubList.Items.RemoveAt(index);
        }, (sender, oldIndex, newIndex, item) => {
            this.themesSubList.Items.Move(oldIndex, newIndex);
        }).AddExistingItems();

        ActivityManager activityManager = ActivityManager.Instance;
        activityManager.TaskStarted += this.OnTaskStarted;
        activityManager.TaskCompleted += this.OnTaskCompleted;
    }

    private class SetThemeContextEntry : CustomContextEntry {
        private readonly Theme theme;

        public SetThemeContextEntry(Theme theme, Icon? icon = null) : base(theme.Name, $"Sets the application's theme to '{theme.Name}'", icon) {
            this.theme = theme;
        }

        public override Task OnExecute(IContextData context) {
            this.theme.ThemeManager.SetTheme(this.theme);
            return Task.CompletedTask;
        }
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

        this.themeListHandler?.Dispose();
        this.themeListHandler = null;

        this.connectedHostNameBinder.Detach();
        // this.isBusyBinder.Detach();
        this.isScanningBinder.Detach();
        this.startAddressBinder.Detach();
        this.addrLengthBinder.Detach();
        this.alignmentBinder.Detach();
        this.pauseXboxBinder.Detach();
        this.scanMemoryPagesBinder.Detach();
    }

    protected override void OnWindowOpened() {
        base.OnWindowOpened();

        UIInputManager.SetFocusPath(this.Window!.Control, "MemEngineWindow");

        this.Window.Control.MinWidth = 560;
        this.Window.Control.MinHeight = 480;
        this.Window.Width = 600;
        this.Window.Height = 600;
        this.Window.Title = "MemEngine360 (Cheat Engine for Xbox 360) v1.1.2";
        this.Window.WindowClosing += this.MyWindowOnWindowClosing;

        using MultiChangeToken change = DataManager.GetContextData(this.Window.Control).BeginChange();
        change.Context.Set(MemoryEngine360.DataKey, this.MemoryEngine360).Set(IMemEngineUI.DataKey, this).Set(ILatestActivityView.DataKey, this);
    }

    protected override void OnWindowClosed() {
        base.OnWindowClosed();
        this.Window!.WindowClosing -= this.MyWindowOnWindowClosing;

        using MultiChangeToken change = DataManager.GetContextData(this.Window.Control).BeginChange();
        change.Context.Remove(MemoryEngine360.DataKey, IMemEngineUI.DataKey, ILatestActivityView.DataKey);
    }

    private async Task<bool> MyWindowOnWindowClosing(IWindow sender, WindowCloseReason reason, bool isCancelled) {
        if (isCancelled) {
            return isCancelled;
        }

        foreach (ActivityTask task in ActivityManager.Instance.ActiveTasks.ToList()) {
            task.TryCancel();
        }

        this.MemoryEngine360.IsShuttingDown = true;
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
        
        IDisposable? token = this.MemoryEngine360.BeginBusyOperation();
        while (token == null) {
            MessageBoxInfo info = new MessageBoxInfo() {
                Caption = "Engine busy", Message = "Cannot close window yet because the engine is still busy and cannot be shutdown safely. What do you want to do?",
                Buttons = MessageBoxButton.YesNoCancel,
                DefaultButton = MessageBoxResult.Yes,
                YesOkText = "Wait for operations",
                NoText = "Force Close",
                CancelText = "Cancel"
            };

            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(info);
            switch (result) {
                case MessageBoxResult.Cancel: return true; // stop window closing
                case MessageBoxResult.No:     return false; // let TCP pipes auto-timeout
                default:                      break; // continue loop
            }

            token = await this.MemoryEngine360.BeginBusyOperationActivityAsync();
        }

        IConsoleConnection? connection = this.MemoryEngine360.Connection;
        try {
            if (connection != null) {
                connection.Dispose();
                this.MemoryEngine360.SetConnection(token, null, ConnectionChangeCause.User);
            }
        }
        finally {
            token.Dispose();
        }

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

    private void PART_ScanOption_Alignment_OnDoubleTapped(object? sender, TappedEventArgs e) {
        this.editAlignmentCommand.Execute(null);
    }

    private void PART_CancelActivityButton_OnClick(object? sender, RoutedEventArgs e) {
        this.primaryActivity?.TryCancel();
        ((Button) sender!).IsEnabled = false;
    }
}