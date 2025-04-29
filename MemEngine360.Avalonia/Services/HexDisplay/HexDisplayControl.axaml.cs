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
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaHex.Core.Document;
using AvaloniaHex.Editing;
using AvaloniaHex.Rendering;
using MemEngine360.Connections;
using MemEngine360.Engine.HexDisplay;
using MemEngine360.Engine.Scanners;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Contexts;
using PFXToolKitUI.Avalonia.Services;
using PFXToolKitUI.Avalonia.Shortcuts.Avalonia;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.Avalonia.Services.HexDisplay;

public partial class HexDisplayControl : WindowingContentControl, IHexDisplayView {
    public static readonly StyledProperty<HexDisplayInfo?> HexDisplayInfoProperty = AvaloniaProperty.Register<HexDisplayControl, HexDisplayInfo?>("HexDisplayInfo");

    public HexDisplayInfo? HexDisplayInfo {
        get => this.GetValue(HexDisplayInfoProperty);
        set => this.SetValue(HexDisplayInfoProperty, value);
    }

    public BitLocation CaretLocation {
        get => this.PART_HexEditor.Caret.Location;
        set => this.PART_HexEditor.Caret.Location = value;
    }

    public BitRange SelectionRange {
        get => this.PART_HexEditor.Selection.Range;
        set => this.PART_HexEditor.Selection.Range = value;
    }

    public ulong DocumentRange => this.PART_HexEditor.Document?.Length ?? 0;

    private readonly OffsetColumn myOffsetColumn;

    private readonly AvaloniaPropertyToDataParameterBinder<HexDisplayInfo> captionBinder = new AvaloniaPropertyToDataParameterBinder<HexDisplayInfo>(WindowTitleProperty, HexDisplayInfo.CaptionParameter);
    private readonly AvaloniaPropertyToDataParameterBinder<HexDisplayInfo> addrBinder = new AvaloniaPropertyToDataParameterBinder<HexDisplayInfo>(TextBox.TextProperty, HexDisplayInfo.StartAddressParameter, (p) => "0x" + ((uint) p!).ToString("X8")) { CanUpdateModel = false };
    private readonly AvaloniaPropertyToDataParameterBinder<HexDisplayInfo> lenBinder = new AvaloniaPropertyToDataParameterBinder<HexDisplayInfo>(TextBox.TextProperty, HexDisplayInfo.LengthParameter, (p) => "0x" + ((uint) p!).ToString("X8")) { CanUpdateModel = false };

    private readonly AsyncRelayCommand updateAddressCommand, updateLengthCommand;
    private readonly AsyncRelayCommand readAllCommand, refreshDataCommand, uploadDataCommand;

    private uint actualStartAddress;
    private byte[]? myCurrData;

    public HexDisplayControl() {
        this.InitializeComponent();
        this.captionBinder.AttachControl(this);
        this.addrBinder.AttachControl(this.PART_AddressTextBox);
        this.lenBinder.AttachControl(this.PART_LengthTextBox);
        this.PART_HexEditor.HexView.BytesPerLine = 32;
        this.PART_HexEditor.HexView.Columns.Add(this.myOffsetColumn = new OffsetColumn());
        this.PART_HexEditor.HexView.Columns.Add(new HexColumn());
        this.PART_HexEditor.HexView.Columns.Add(new AsciiColumn());

        this.PART_CancelButton.Click += this.OnCancelButtonClicked;
        this.updateAddressCommand = new AsyncRelayCommand(async () => {
            HexDisplayInfo? info = this.HexDisplayInfo;
            if (info != null) {
                if (!NumberUtils.TryParseHexOrRegular(this.PART_AddressTextBox.Text ?? "", out uint value)) {
                    this.PART_AddressTextBox.Text = "0x" + info.StartAddress.ToString("X8");
                    BugFix.TextBox_UpdateSelection(this.PART_AddressTextBox);
                    await IMessageDialogService.Instance.ShowMessage("Invalid value", "Start address is invalid", defaultButton: MessageBoxResult.OK);
                }
                else {
                    info.StartAddress = value;
                }
            }
        });

        this.updateLengthCommand = new AsyncRelayCommand(async () => {
            HexDisplayInfo? info = this.HexDisplayInfo;
            if (info != null) {
                if (!NumberUtils.TryParseHexOrRegular(this.PART_LengthTextBox.Text ?? "", out uint value)) {
                    this.PART_LengthTextBox.Text = "0x" + info.Length.ToString("X8");
                    BugFix.TextBox_UpdateSelection(this.PART_LengthTextBox);
                    await IMessageDialogService.Instance.ShowMessage("Invalid value", "Length value is invalid", defaultButton: MessageBoxResult.OK);
                }
                else {
                    info.Length = value;
                }
            }
        });

        this.readAllCommand = new AsyncRelayCommand(async () => {
            await this.ReadAllFromConsoleCommand();
        });

        this.refreshDataCommand = new AsyncRelayCommand(async () => {
            await this.ReloadSelectionFromConsole();
        }, () => {
            BitRange selection = this.PART_HexEditor.Selection.Range;
            return selection.ByteLength > 1 && this.myCurrData != null && !this.PART_HexEditor.Document!.IsReadOnly;
        });

        this.uploadDataCommand = new AsyncRelayCommand(async () => {
            await this.UploadSelectionToConsoleCommand();
        }, () => {
            BitRange selection = this.PART_HexEditor.Selection.Range;
            return selection.ByteLength > 1 && this.myCurrData != null && !this.PART_HexEditor.Document!.IsReadOnly;
        });

        this.PART_Read.Command = this.readAllCommand;
        this.PART_Refresh.Command = this.refreshDataCommand;
        this.PART_Upload.Command = this.uploadDataCommand;
        this.PART_AddressTextBox.LostFocus += (sender, args) => this.updateAddressCommand.Execute(null);
        this.PART_LengthTextBox.LostFocus += (sender, args) => this.updateLengthCommand.Execute(null);

        this.PART_AddressTextBox.KeyDown += (sender, args) => {
            if (args.Key == Key.Enter) {
                args.Handled = true;
                this.updateAddressCommand.Execute(null);
            }
            else if (args.Key == Key.Escape && this.HexDisplayInfo is HexDisplayInfo info) {
                args.Handled = true;
                this.PART_AddressTextBox.Text = "0x" + info.StartAddress.ToString("X8");
                BugFix.TextBox_UpdateSelection((TextBox) sender!);
            }
        };

        this.PART_LengthTextBox.KeyDown += (sender, args) => {
            if (args.Key == Key.Enter) {
                args.Handled = true;
                this.updateLengthCommand.Execute(null);
            }
            else if (args.Key == Key.Escape && this.HexDisplayInfo is HexDisplayInfo info) {
                args.Handled = true;
                this.PART_LengthTextBox.Text = "0x" + info.Length.ToString("X8");
                BugFix.TextBox_UpdateSelection((TextBox) sender!);
            }
        };

        this.PART_HexEditor.Caret.LocationChanged += (sender, args) => this.UpdateCaretText();
        this.PART_HexEditor.Caret.ModeChanged += (sender, args) => this.UpdateCaretText();
        this.PART_HexEditor.Caret.PrimaryColumnChanged += (sender, args) => this.UpdateCaretText();
        this.PART_HexEditor.Selection.RangeChanged += (sender, args) => this.UpdateSelectionText();
    }
    
    public async Task ReadAllFromConsoleCommand() {
        HexDisplayInfo? info = this.HexDisplayInfo;
        if (info == null) {
            return;
        }

        if (info.Length < 1) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Length is zero.", defaultButton: MessageBoxResult.OK);
            return;
        }

        this.PART_ProgressGrid.IsVisible = true;
        this.PART_ControlsGrid.IsEnabled = false;
        this.PART_Progress.IsIndeterminate = true;

        BitRange selection = this.PART_HexEditor.Selection.Range;
        byte[]? bytes = await info.MemoryEngine360.BeginBusyOperationActivityAsync(async (t, c) => {
            using CancellationTokenSource cts = new CancellationTokenSource();
            return await ActivityManager.Instance.RunTask(async () => {
                if (c is IFreezableConsole)
                    await ((IFreezableConsole) c).DebugFreeze();

                ActivityTask task = ActivityManager.Instance.CurrentTask;
                SimpleCompletionState completion = new SimpleCompletionState();
                completion.CompletionValueChanged += state => {
                    task.Progress.CompletionState.TotalCompletion = state.TotalCompletion;
                    task.Progress.Text = $"Reading {IValueScanner.ByteFormatter.ToString(info.Length * state.TotalCompletion, false)}/{IValueScanner.ByteFormatter.ToString(info.Length, false)}";
                };

                // Update initial text
                completion.OnCompletionValueChanged();
                byte[] buffer = new byte[info.Length];
                await c.ReadBytes(info.StartAddress, buffer, 0, info.Length, 0x10000, completion, task.CancellationToken);
                if (c is IFreezableConsole)
                    await ((IFreezableConsole) c).DebugUnFreeze();

                return buffer;
            }, cts);
        });

        this.PART_ProgressGrid.IsVisible = false;
        this.PART_ControlsGrid.IsEnabled = true;
        this.PART_Progress.IsIndeterminate = false;
        if (bytes != null) {
            Vector scroll = this.PART_HexEditor.HexView.ScrollOffset;
            BitLocation location = this.PART_HexEditor.Caret.Location;

            this.actualStartAddress = info.StartAddress;
            this.myOffsetColumn.AdditionalOffset = info.StartAddress;
            this.myCurrData = bytes;
            this.PART_HexEditor.Document = new MemoryBinaryDocument(this.myCurrData, info.IsReadOnly);
            await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                this.PART_HexEditor.HexView.ScrollOffset = scroll;
                this.PART_HexEditor.Caret.Location = location;
                this.PART_HexEditor.Selection.Range = selection;
            }, DispatchPriority.INTERNAL_BeforeRender);

            this.UpdateSelectionText();
            this.UpdateCaretText();
        }
    }

    public async Task ReloadSelectionFromConsole() {
        HexDisplayInfo? info = this.HexDisplayInfo;
        if (info == null) {
            return;
        }
        
        BitRange selection = this.PART_HexEditor.Selection.Range;
        if (selection.ByteLength < 2 || this.myCurrData == null || this.PART_HexEditor.Document!.IsReadOnly) {
            return;
        }

        this.PART_ProgressGrid.IsVisible = true;
        this.PART_ControlsGrid.IsEnabled = false;
        this.PART_Progress.IsIndeterminate = true;
        
        uint start = (uint) selection.Start.ByteIndex;
        uint count = (uint) selection.ByteLength;
        byte[]? readBuffer = await info.MemoryEngine360.BeginBusyOperationActivityAsync(async (t, c) => {
            using CancellationTokenSource cts = new CancellationTokenSource();
            return await ActivityManager.Instance.RunTask(async () => {
                if (c is IFreezableConsole)
                    await ((IFreezableConsole) c).DebugFreeze();

                ActivityTask task = ActivityManager.Instance.CurrentTask;
                SimpleCompletionState completion = new SimpleCompletionState();
                completion.CompletionValueChanged += state => {
                    task.Progress.CompletionState.TotalCompletion = state.TotalCompletion;
                    task.Progress.Text = $"Reading {IValueScanner.ByteFormatter.ToString(info.Length * state.TotalCompletion, false)}/{IValueScanner.ByteFormatter.ToString(info.Length, false)}";
                };

                // Update initial text
                completion.OnCompletionValueChanged();

                byte[] buffer = new byte[count];
                await c.ReadBytes(this.actualStartAddress + start, buffer, 0, count, 0x10000, completion, task.CancellationToken);
                if (c is IFreezableConsole)
                    await ((IFreezableConsole) c).DebugUnFreeze();
                return buffer;
            }, cts);
        });

        this.PART_ProgressGrid.IsVisible = false;
        this.PART_ControlsGrid.IsEnabled = true;
        this.PART_Progress.IsIndeterminate = false;

        if (readBuffer != null) {
            this.PART_HexEditor.Document!.WriteBytes(start, readBuffer);
        }

        this.UpdateSelectionText();
        this.UpdateCaretText();
    }

    public async Task UploadSelectionToConsoleCommand() {
        HexDisplayInfo? info = this.HexDisplayInfo;
        if (info == null) {
            return;
        }

        byte[]? buffer = this.myCurrData;
        if (buffer == null) {
            return;
        }

        BitRange selection = this.SelectionRange;
        if (selection.ByteLength < 1) {
            await IMessageDialogService.Instance.ShowMessage("No selection", "Please make a selection to upload. Click CTRL+A to select all.", defaultButton: MessageBoxResult.OK);
            return;
        }

        uint address;
        if (info.StartAddress == this.actualStartAddress) {
            address = this.actualStartAddress;
        }
        else {
            MessageBoxInfo msgInfo = new MessageBoxInfo("Different Start", $"The Start address field (current) is different from the last refreshed address (original){Environment.NewLine}{info.StartAddress:X8} != {this.actualStartAddress:X8}{Environment.NewLine}What do you want to do?") {
                Buttons = MessageBoxButton.YesNoCancel, DefaultButton = MessageBoxResult.No,
                YesOkText = "Write at current",
                NoText = "Write at original",
                CancelText = "Cancel"
            };

            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(msgInfo);
            if (result == MessageBoxResult.Cancel) {
                return;
            }

            address = result == MessageBoxResult.Yes ? info.StartAddress : this.actualStartAddress;
        }

        this.PART_ProgressGrid.IsVisible = true;
        this.PART_ControlsGrid.IsEnabled = false;
        this.PART_Progress.IsIndeterminate = true;
        await info.MemoryEngine360.BeginBusyOperationActivityAsync(async (t, c) => {
            using CancellationTokenSource cts = new CancellationTokenSource();
            await ActivityManager.Instance.RunTask(async () => {
                if (c is IFreezableConsole)
                    await ((IFreezableConsole) c).DebugFreeze();

                ActivityTask task = ActivityManager.Instance.CurrentTask;
                SimpleCompletionState completion = new SimpleCompletionState();
                completion.CompletionValueChanged += state => {
                    task.Progress.CompletionState.TotalCompletion = state.TotalCompletion;
                    task.Progress.Text = $"Writing {IValueScanner.ByteFormatter.ToString(selection.ByteLength * state.TotalCompletion, false)}/{IValueScanner.ByteFormatter.ToString(selection.ByteLength, false)}";
                };

                uint start = (uint) selection.Start.ByteIndex;
                uint count = (uint) selection.ByteLength;

                // Update initial text
                completion.OnCompletionValueChanged();
                await c.WriteBytes(address, buffer, (int) start, count, completion, task.CancellationToken);
                if (c is IFreezableConsole)
                    await ((IFreezableConsole) c).DebugUnFreeze();
            }, cts);
        });

        this.PART_ProgressGrid.IsVisible = false;
        this.PART_ControlsGrid.IsEnabled = true;
        this.PART_Progress.IsIndeterminate = false;
    }

    private void UpdateSelectionText() {
        this.uploadDataCommand.RaiseCanExecuteChanged();
        this.refreshDataCommand.RaiseCanExecuteChanged();

        Selection sel = this.PART_HexEditor.Selection;
        if (sel.Range.IsEmpty) {
            this.PART_SelectionText.Text = "<none>";
        }
        else {
            this.PART_SelectionText.Text = $"{sel.Range.ByteLength} bytes ({(this.actualStartAddress + sel.Range.Start.ByteIndex):X8} -> {(this.actualStartAddress + sel.Range.End.ByteIndex):X8})";
        }
    }

    private void UpdateCaretText() {
        Caret caret = this.PART_HexEditor.Caret;
        BitLocation pos = caret.Location;
        this.PART_CaretText.Text = $"{(this.actualStartAddress + pos.ByteIndex):X8}";
    }

    static HexDisplayControl() {
        HexDisplayInfoProperty.Changed.AddClassHandler<HexDisplayControl, HexDisplayInfo?>((o, e) => o.OnInfoChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    protected override void OnWindowOpened() {
        base.OnWindowOpened();
        this.Window!.Control.MinWidth = 1024;
        this.Window!.Control.MinHeight = 640;
        this.Window!.CanAutoSizeToContent = false;

        UIInputManager.SetFocusPath(this.Window!.Control, "HexDisplayWindow");
        UIInputManager.SetFocusPath(this.PART_HexEditor, "HexDisplayWindow/HexEditor");
        using MultiChangeToken change = DataManager.GetContextData(this.Window.Control).BeginChange();
        change.Context.Set(IHexDisplayView.DataKey, this);
    }

    protected override void OnWindowClosed() {
        base.OnWindowClosed();
        this.HexDisplayInfo = null;
    }

    private void OnInfoChanged(HexDisplayInfo? oldData, HexDisplayInfo? newData) {
        this.captionBinder.SwitchModel(newData);
        this.addrBinder.SwitchModel(newData);
        this.lenBinder.SwitchModel(newData);
        if (newData != null) {
            this.PART_CancelButton.Focus();
        }
    }

    private void OnCancelButtonClicked(object? sender, RoutedEventArgs e) {
        this.Window!.Close();
    }
}