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

using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using MemEngine360.Engine;
using MemEngine360.PointerScanning;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.PointerScanning;

public partial class PointerScannerView : UserControl {
    public static readonly DataKey<PointerScanner> PointerScannerDataKey = DataKeys.Create<PointerScanner>(nameof(PointerScanner));

    public static readonly StyledProperty<PointerScanner?> PointerScannerProperty = AvaloniaProperty.Register<PointerScannerView, PointerScanner?>(nameof(PointerScanner));

    private readonly IBinder<PointerScanner> binder_AddressableBase =
        new TextBoxToEventPropertyBinder<PointerScanner>(
            nameof(PointerScanner.AddressableRangeChanged),
            b => b.Model.AddressableBase.ToString("X8"),
            (b, t) => ParseAddressHelper(b, t, "Invalid Base Address", (m, v) => m.AddressableBase = v));

    private readonly IBinder<PointerScanner> binder_AddressableLength =
        new TextBoxToEventPropertyBinder<PointerScanner>(
            nameof(PointerScanner.AddressableRangeChanged),
            b => b.Model.AddressableLength.ToString("X8"),
            (b, t) => ParseAddressHelper(b, t, "Invalid Length", (m, v) => m.AddressableLength = v));

    private readonly IBinder<PointerScanner> binder_MaxDepth =
        new TextBoxToEventPropertyBinder<PointerScanner>(
            nameof(PointerScanner.MaxDepthChanged),
            b => b.Model.MaxDepth.ToString(),
            (b, t) => ParseUIntHelper(b, t, "Invalid Max Depth", (string s, out byte value) => byte.TryParse(s, out value), (m, v) => m.MaxDepth = v));

    private readonly IBinder<PointerScanner> binder_MinimumOffset =
        new TextBoxToEventPropertyBinder<PointerScanner>(
            nameof(PointerScanner.MinimumOffsetChanged),
            b => b.Model.MinimumOffset.ToString("X8"),
            (b, t) => ParseAddressHelperAsync(b, t, "Invalid Offset", async (m, v) => {
                m.MinimumOffset = v;
                await QueryRegeneratePointerMap(m);
            }));

    // private readonly IBinder<PointerScanner> binder_MaximumOffset =
    //     new TextBoxToEventPropertyBinder<PointerScanner>(
    //         nameof(PointerScanner.MaximumOffsetChanged),
    //         b => b.Model.MaximumOffset.ToString("X8"),
    //         (b, t) => ParseAddressHelperAsync(b, t, "Invalid Offset", async (m, v) => {
    //             m.MaximumOffset = v;
    //             await QueryRegeneratePointerMap(m);
    //         }));

    private readonly IBinder<PointerScanner> binder_PrimaryMaximumOffset =
        new TextBoxToEventPropertyBinder<PointerScanner>(
            nameof(PointerScanner.PrimaryMaximumOffsetChanged),
            b => b.Model.PrimaryMaximumOffset.ToString("X8"),
            (b, t) => ParseAddressHelperAsync(b, t, "Invalid Offset", async (m, v) => {
                m.PrimaryMaximumOffset = v;
                await QueryRegeneratePointerMap(m);
            }));

    private readonly IBinder<PointerScanner> binder_SecondaryMaximumOffset =
        new TextBoxToEventPropertyBinder<PointerScanner>(
            nameof(PointerScanner.SecondaryMaximumOffsetChanged),
            b => b.Model.SecondaryMaximumOffset.ToString("X8"),
            (b, t) => ParseAddressHelperAsync(b, t, "Invalid Offset", async (m, v) => {
                m.SecondaryMaximumOffset = v;
                await QueryRegeneratePointerMap(m);
            }));

    private readonly IBinder<PointerScanner> binder_SearchAddress =
        new TextBoxToEventPropertyBinder<PointerScanner>(
            nameof(PointerScanner.SearchAddressChanged),
            b => b.Model.SearchAddress.ToString("X8"),
            (b, t) => ParseAddressHelper(b, t, "Invalid Search Address", (m, v) => m.SearchAddress = v));

    private readonly IBinder<PointerScanner> binder_Alignment =
        new TextBoxToEventPropertyBinder<PointerScanner>(
            nameof(PointerScanner.AlignmentChanged),
            b => b.Model.Alignment.ToString(),
            (b, t) => ParseUIntHelper<uint>(b, t, "Invalid Alignment", uint.TryParse, (m, v) => m.Alignment = v));

    private readonly IBinder<PointerScanner> binder_StatusBar =
        new EventUpdateBinder<PointerScanner>(
            nameof(PointerScanner.HasPointerMapChanged),
            b => ((TextBlock) b.Control).Text = b.Model.HasPointerMap ? $"Pointer map loaded with {b.Model.PointerMap.Count}" : "No pointer map loaded");

    private delegate bool TryParseDelegate<T>(string input, [NotNullWhen(true)] out T? value);

    private static async Task<bool> ParseUIntHelper<T>(IBinder<PointerScanner> binder, string input, string errorMessage, TryParseDelegate<T> tryParse, Action<PointerScanner, T> func) {
        if (!tryParse(input, out T? value)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid input", errorMessage, icon: MessageBoxIcons.ErrorIcon);
            return false;
        }

        func(binder.Model, value);
        return true;
    }

    private static async Task<bool> ParseAddressHelper(IBinder<PointerScanner> binder, string input, string caption, Action<PointerScanner, uint> func) {
        if (!AddressParsing.TryParse32(input, out uint value, out string? error, canParseAsExpression: true)) {
            await IMessageDialogService.Instance.ShowMessage(caption, error, defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
            return false;
        }

        func(binder.Model, value);
        return true;
    }

    private static async Task<bool> ParseAddressHelperAsync(IBinder<PointerScanner> binder, string input, string caption, Func<PointerScanner, uint, Task> func) {
        if (!AddressParsing.TryParse32(input, out uint value, out string? error, canParseAsExpression: true)) {
            await IMessageDialogService.Instance.ShowMessage(caption, error, defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
            return false;
        }

        await func(binder.Model, value);
        return true;
    }

    private static async Task QueryRegeneratePointerMap(PointerScanner scanner) {
        MessageBoxInfo info = new MessageBoxInfo(MessageBoxes.PointerScannerRequiresRegeneration) {
            Buttons = MessageBoxButtons.YesNo, DefaultButton = MessageBoxResult.Yes
        };

        if (await IMessageDialogService.Instance.ShowMessage(info) == MessageBoxResult.Yes) {
            CancellationTokenSource cts = new CancellationTokenSource();

            IActivityProgress progressTracker = new DispatcherActivityProgress() { Caption = "Generate PointerMap" };
            Task task = scanner.GenerateBasePointerMap(progressTracker, cts.Token);

            // Do not await -- we want to run it as a background task
            _ = ActivityManager.Instance.RunTask(async () => {
                using CancellationTokenSource theCts = cts;
                await task;
            }, progressTracker, cts);
        }
    }

    public PointerScanner? PointerScanner {
        get => this.GetValue(PointerScannerProperty);
        set => this.SetValue(PointerScannerProperty, value);
    }

    public PointerScannerView() {
        this.InitializeComponent();
        this.binder_AddressableBase.AttachControl(this.PART_AddressableBase);
        this.binder_AddressableLength.AttachControl(this.PART_AddressableLength);
        this.binder_MaxDepth.AttachControl(this.PART_MaxDepth);
        this.binder_MinimumOffset.AttachControl(this.PART_MinimumOffset);
        // this.binder_MaximumOffset.AttachControl(this.PART_PrimaryMaximumOffset);
        this.binder_PrimaryMaximumOffset.AttachControl(this.PART_PrimaryMaximumOffset);
        this.binder_SecondaryMaximumOffset.AttachControl(this.PART_SecondaryMaximumOffset);
        this.binder_SearchAddress.AttachControl(this.PART_SearchAddress);
        this.binder_Alignment.AttachControl(this.PART_Alignment);
        this.binder_StatusBar.AttachControl(this.PART_StatusBar);

        this.PART_OpenFile.Command = new AsyncRelayCommand(async () => {
            PointerScanner? scanner = this.PointerScanner;
            if (scanner == null) {
                return;
            }

            string? file = await IFilePickDialogService.Instance.OpenFile("Open binary file", [Filters.All]);
            if (file != null) {
                MessageBoxResult resultIsLE = await IMessageDialogService.Instance.ShowMessage("Endianness", "Is the data little endian? (For Xbox360 and PS3, select No)", MessageBoxButtons.YesNoCancel, MessageBoxResult.No, icon: MessageBoxIcons.QuestionIcon);
                if (resultIsLE != MessageBoxResult.Yes && resultIsLE != MessageBoxResult.No) {
                    return;
                }

                SingleUserInputInfo info = new SingleUserInputInfo("Base Address", "What is the base-address of the data?", "00000000") {
                    Footer = "E.g. If you ran a memory dump at 0x82600000, then specify that as the base address",
                    Validate = (a) => {
                        if (!AddressParsing.TryParse32(a.Input, out _, out string? error, canParseAsExpression: true)) {
                            a.Errors.Add(error);
                        }
                    },
                    DebounceErrorsDelay = 300 // add a delay between parsing, to reduce typing lag due to expression parsing
                };

                if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info, IDesktopWindow.DesktopWindowFromVisual(this)) != true) {
                    return;
                }

                using CancellationTokenSource cancellation = new CancellationTokenSource();

                uint baseAddress = AddressParsing.Parse32(info.Text, canParseAsExpression: true);
                scanner.DisposeMemoryDump();
                Task loadDumpTask = scanner.LoadMemoryDump(file, baseAddress, resultIsLE == MessageBoxResult.Yes, cancellation.Token);
                IActivityProgress progressTracker = new DispatcherActivityProgress();

                await ActivityManager.Instance.RunTask(async () => {
                    IActivityProgress prog = ActivityTask.Current.Progress;
                    prog.IsIndeterminate = true;
                    prog.Caption = "Load Memory Dump";
                    prog.Text = "Loading memory...";
                    await loadDumpTask;
                }, progressTracker, cancellation);

                Task task = this.PointerScanner!.GenerateBasePointerMap(progressTracker, cancellation.Token);

                await ActivityManager.Instance.RunTask(() => task, progressTracker, cancellation);
            }
        });

        this.PART_RunScan.Command = new AsyncRelayCommand(() => {
            PointerScanner? scanner = this.PointerScanner;
            if (scanner == null)
                return Task.CompletedTask;

            if (!scanner.HasPointerMap)
                return IMessageDialogService.Instance.ShowMessage("Not ready", "Memory Dump file not loaded.", icon: MessageBoxIcons.WarningIcon);

            // Set selected tab to scan results
            this.PART_TabScanResults.IsSelected = true;
            return scanner.Run();
        }, () => this.PointerScanner != null && !this.PointerScanner.IsScanRunning);

        this.PART_StopScan.Command = new AsyncRelayCommand(() => {
            this.PointerScanner?.CancelScan();
            return Task.CompletedTask;
        }, () => this.PointerScanner != null && this.PointerScanner.IsScanRunning);
    }

    static PointerScannerView() {
        PointerScannerProperty.Changed.AddClassHandler<PointerScannerView, PointerScanner?>((s, e) => s.OnPointerScannerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnPointerScannerChanged(PointerScanner? oldValue, PointerScanner? newValue) {
        this.binder_AddressableBase.SwitchModel(newValue);
        this.binder_AddressableLength.SwitchModel(newValue);
        this.binder_MaxDepth.SwitchModel(newValue);
        this.binder_MinimumOffset.SwitchModel(newValue);
        // this.binder_MaximumOffset.SwitchModel(newValue);
        this.binder_PrimaryMaximumOffset.SwitchModel(newValue);
        this.binder_SecondaryMaximumOffset.SwitchModel(newValue);
        this.binder_SearchAddress.SwitchModel(newValue);
        this.binder_Alignment.SwitchModel(newValue);
        this.binder_StatusBar.SwitchModel(newValue);
        this.PART_ScanResults.ItemsSource = newValue?.PointerChain;

        if (newValue != null)
            DataManager.GetContextData(this).Set(PointerScannerDataKey, newValue);
        else
            DataManager.GetContextData(this).Remove(PointerScannerDataKey);

        if (oldValue != null)
            oldValue.IsScanRunningChanged -= this.OnUpdateStartStopCommands;
        if (newValue != null)
            newValue.IsScanRunningChanged += this.OnUpdateStartStopCommands;
        this.OnUpdateStartStopCommands(null!, EventArgs.Empty);
    }

    private void OnUpdateStartStopCommands(object? sender, EventArgs eventArgs) {
        ApplicationPFX.Instance.Dispatcher.Post(() => ((AsyncRelayCommand) this.PART_StopScan.Command!).RaiseCanExecuteChanged());
        ApplicationPFX.Instance.Dispatcher.Post(() => ((AsyncRelayCommand) this.PART_RunScan.Command!).RaiseCanExecuteChanged());
    }
}