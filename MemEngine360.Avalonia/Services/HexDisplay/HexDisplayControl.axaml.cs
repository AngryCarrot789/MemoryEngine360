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
using AvaloniaHex.Document;
using AvaloniaHex.Editing;
using AvaloniaHex.Rendering;
using MemEngine360.Connections;
using MemEngine360.Engine.HexDisplay;
using MemEngine360.Engine.Scanners;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Services;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.Avalonia.Services.HexDisplay;

public partial class HexDisplayControl : WindowingContentControl {
    public static readonly StyledProperty<HexDisplayInfo?> HexDisplayInfoProperty = AvaloniaProperty.Register<HexDisplayControl, HexDisplayInfo?>("HexDisplayInfo");

    public HexDisplayInfo? HexDisplayInfo {
        get => this.GetValue(HexDisplayInfoProperty);
        set => this.SetValue(HexDisplayInfoProperty, value);
    }

    private readonly OffsetColumn myOffsetColumn;
    
    private readonly AvaloniaPropertyToDataParameterBinder<HexDisplayInfo> captionBinder = new AvaloniaPropertyToDataParameterBinder<HexDisplayInfo>(WindowTitleProperty, HexDisplayInfo.CaptionParameter);
    private readonly AvaloniaPropertyToDataParameterBinder<HexDisplayInfo> addrBinder = new AvaloniaPropertyToDataParameterBinder<HexDisplayInfo>(TextBox.TextProperty, HexDisplayInfo.StartAddressParameter, (p) => "0x" + ((uint) p!).ToString("X8")) { CanUpdateModel = false };
    private readonly AvaloniaPropertyToDataParameterBinder<HexDisplayInfo> lenBinder = new AvaloniaPropertyToDataParameterBinder<HexDisplayInfo>(TextBox.TextProperty, HexDisplayInfo.LengthParameter, (p) => "0x" + ((uint) p!).ToString("X8")) { CanUpdateModel = false };

    private readonly AsyncRelayCommand updateAddressCommand, updateLengthCommand;
    private readonly AsyncRelayCommand refreshDataCommand;

    private ulong actualStartAddress;
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
        this.PART_Upload.IsEnabled = false;

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

        this.refreshDataCommand = new AsyncRelayCommand(async () => {
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
                    await c.ReadBytes(info.StartAddress, buffer, 0, info.Length, 0x10000, task.CancellationToken, completion);
                    if (c is IFreezableConsole)
                        await ((IFreezableConsole) c).DebugUnFreeze();

                    return buffer;
                }, cts);
            });

            this.PART_ProgressGrid.IsVisible = false;
            this.PART_ControlsGrid.IsEnabled = true;
            this.PART_Progress.IsIndeterminate = false;

            if (bytes == null) {
                return;
            }

            Vector scroll = this.PART_HexEditor.HexView.ScrollOffset;
            BitLocation location = this.PART_HexEditor.Caret.Location;
            BitRange selection = this.PART_HexEditor.Selection.Range;

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
        });

        this.PART_Refresh.Command = this.refreshDataCommand;
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
    
    private void UpdateSelectionText() {
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