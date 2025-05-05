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

using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Interactivity;
using AvaloniaHex.Core.Document;
using AvaloniaHex.Editing;
using AvaloniaHex.Rendering;
using MemEngine360.Connections;
using MemEngine360.Engine.HexDisplay;
using MemEngine360.Engine.Scanners;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.Enums;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Contexts;
using PFXToolKitUI.Avalonia.Services;
using PFXToolKitUI.Avalonia.Shortcuts.Avalonia;
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.Services.HexDisplay;

public partial class HexDisplayControl : WindowingContentControl, IHexDisplayView {
    public static readonly StyledProperty<HexDisplayInfo?> HexDisplayInfoProperty = AvaloniaProperty.Register<HexDisplayControl, HexDisplayInfo?>("HexDisplayInfo");

    public HexDisplayInfo? HexDisplayInfo {
        get => this.GetValue(HexDisplayInfoProperty);
        set => this.SetValue(HexDisplayInfoProperty, value);
    }

    public BitLocation CaretLocation {
        get => this.PART_HexEditor.Caret.Location;
        set {
            this.PART_HexEditor.ResetCursorAnchor();
            this.PART_HexEditor.Caret.Location = value;
        }
    }

    public BitRange SelectionRange {
        get => this.PART_HexEditor.Selection.Range;
        set {
            this.PART_HexEditor.ResetCursorAnchor();
            this.PART_HexEditor.Selection.Range = value;
        }
    }

    public ulong DocumentLength => this.PART_HexEditor.Document?.Length ?? 0;

    public uint CurrentStartOffset => this.actualStartAddress;

    private readonly OffsetColumn myOffsetColumn;

    public delegate void HexDisplayControlTheEndiannessChangedEventHandler(HexDisplayControl sender);

    private enum Endianness {
        LittleEndian,
        BigEndian,
    }

    private Endianness lastEndianness, theEndianness = Endianness.BigEndian;

    private Endianness TheEndianness {
        get => this.theEndianness;
        set {
            if (this.theEndianness != value) {
                this.theEndianness = value;
                this.TheEndiannessChanged?.Invoke(this);
                this.UpdateDataInspector();
            }
        }
    }

    public event HexDisplayControlTheEndiannessChangedEventHandler? TheEndiannessChanged;

    private readonly AvaloniaPropertyToDataParameterBinder<HexDisplayInfo> captionBinder = new AvaloniaPropertyToDataParameterBinder<HexDisplayInfo>(WindowTitleProperty, HexDisplayInfo.CaptionParameter);

    private readonly IBinder<HexDisplayInfo> addrBinder = new TextBoxToDataParameterBinder<HexDisplayInfo, uint>(HexDisplayInfo.StartAddressParameter, (p) => p!.ToString("X8"), async (t, x) => {
        if (uint.TryParse(x, NumberStyles.HexNumber, null, out uint value)) {
            return value;
        }
        else if (ulong.TryParse(x, NumberStyles.HexNumber, null, out _)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Address is too long. It can only be 4 bytes", defaultButton: MessageBoxResult.OK);
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Start address is invalid", defaultButton: MessageBoxResult.OK);
        }

        return default;
    });

    private readonly IBinder<HexDisplayInfo> lenBinder = new TextBoxToDataParameterBinder<HexDisplayInfo, uint>(HexDisplayInfo.LengthParameter, (p) => p!.ToString("X8"), async (t, x) => {
        if (uint.TryParse(x, NumberStyles.HexNumber, null, out uint value)) {
            return value;
        }
        else if (ulong.TryParse(x, NumberStyles.HexNumber, null, out _)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Address is too long. It can only be 4 bytes", defaultButton: MessageBoxResult.OK);
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Length address is invalid", defaultButton: MessageBoxResult.OK);
        }

        return default;
    });


    private readonly IBinder<HexDisplayInfo> autoRefreshAddrBinder;
    private readonly IBinder<HexDisplayInfo> autoRefreshLenBinder;

    private readonly EventPropertyEnumBinder<Endianness> endiannessBinder = new EventPropertyEnumBinder<Endianness>(typeof(HexDisplayControl), nameof(TheEndiannessChanged), (x) => ((HexDisplayControl) x).TheEndianness, (x, y) => ((HexDisplayControl) x).TheEndianness = y);

    private readonly AsyncRelayCommand readAllCommand, refreshDataCommand, uploadDataCommand;

    private uint actualStartAddress;
    private byte[]? myCurrData;

    private CancellationTokenSource? autoRefreshCts;
    private ActivityTask? currentAutoRefresh;

    private readonly AutoRefreshLayer autoRefreshLayer;
    private IDisposable? currBusyToken;

    public HexDisplayControl() {
        this.InitializeComponent();
        this.captionBinder.AttachControl(this);
        this.addrBinder.AttachControl(this.PART_AddressTextBox);
        this.lenBinder.AttachControl(this.PART_LengthTextBox);

        HexView view = this.PART_HexEditor.HexView;
        view.BytesPerLine = 32;
        view.Columns.Add(this.myOffsetColumn = new OffsetColumn());
        view.Columns.Add(new HexColumn());
        view.Columns.Add(new AsciiColumn());
        view.Layers.InsertBefore<TextLayer>(this.autoRefreshLayer = new AutoRefreshLayer(this.PART_HexEditor.Caret));

        this.PART_DisplayIntAsHex.IsCheckedChanged += (sender, args) => this.UpdateDataInspector();

        this.endiannessBinder.Assign(this.PART_LittleEndian, Endianness.LittleEndian);
        this.endiannessBinder.Assign(this.PART_BigEndian, Endianness.BigEndian);
        this.endiannessBinder.Attach(this);

        this.PART_CancelButton.Click += this.OnCancelButtonClicked;
        this.readAllCommand = new AsyncRelayCommand(async () => {
            await this.ReadAllFromConsoleCommand();
        }, () => this.currentAutoRefresh == null);

        this.refreshDataCommand = new AsyncRelayCommand(async () => {
            await this.ReloadSelectionFromConsole();
        }, () => {
            if (this.currentAutoRefresh != null)
                return false;

            BitRange selection = this.SelectionRange;
            return selection.ByteLength > 0 && this.myCurrData != null && !this.PART_HexEditor.Document!.IsReadOnly;
        });

        this.uploadDataCommand = new AsyncRelayCommand(async () => {
            await this.UploadSelectionToConsoleCommand();
        }, () => {
            if (this.currentAutoRefresh != null)
                return false;

            BitRange selection = this.SelectionRange;
            return selection.ByteLength > 0 && this.myCurrData != null && !this.PART_HexEditor.Document!.IsReadOnly;
        });

        this.PART_Read.Command = this.readAllCommand;
        this.PART_Refresh.Command = this.refreshDataCommand;
        this.PART_Upload.Command = this.uploadDataCommand;
        this.PART_HexEditor.Caret.LocationChanged += (sender, args) => this.UpdateCaretText();
        this.PART_HexEditor.Caret.ModeChanged += (sender, args) => this.UpdateCaretText();
        this.PART_HexEditor.Caret.PrimaryColumnChanged += (sender, args) => this.UpdateCaretText();
        this.PART_HexEditor.Selection.RangeChanged += (sender, args) => this.UpdateSelectionText();

        this.PART_BtnFwdInt8.Click += (s, e) => this.MoveCursorForDataType(1);
        this.PART_BtnFwdInt16.Click += (s, e) => this.MoveCursorForDataType(2);
        this.PART_BtnFwdInt32.Click += (s, e) => this.MoveCursorForDataType(4);
        this.PART_BtnFwdInt64.Click += (s, e) => this.MoveCursorForDataType(8);
        this.PART_BtnBackInt8.Click += (s, e) => this.MoveCursorForDataType(-1);
        this.PART_BtnBackInt16.Click += (s, e) => this.MoveCursorForDataType(-2);
        this.PART_BtnBackInt32.Click += (s, e) => this.MoveCursorForDataType(-4);
        this.PART_BtnBackInt64.Click += (s, e) => this.MoveCursorForDataType(-8);

        this.PART_BtnGoToPointerInt32.Click += (s, e) => this.NavigateToPointer();

        this.PART_SetAutoRefreshRangeAsSelection.Click += (s, e) => {
            BitRange selection = this.SelectionRange;
            this.HexDisplayInfo!.AutoRefreshStartAddress = (uint) (this.actualStartAddress + selection.Start.ByteIndex);
            this.HexDisplayInfo!.AutoRefreshLength = (uint) selection.ByteLength;
        };

        this.PART_ClearAutoRefreshRange.Click += (s, e) => {
            this.HexDisplayInfo!.AutoRefreshStartAddress = 0;
            this.HexDisplayInfo!.AutoRefreshLength = 0;
        };
        
        this.PART_ToggleAutoRefreshButton.Command = new AsyncRelayCommand(async () => {
            if (this.currentAutoRefresh != null) {
                // We are running, so stop it
                if (!this.currentAutoRefresh.TryCancel()) {
                    Debug.Fail("!!! couldn't cancel");
                    return;
                }

                await this.currentAutoRefresh;
            }
            else {
                if (this.autoRefreshCts != null)
                    throw new Exception("App state is weird...");

                HexDisplayInfo? info = this.HexDisplayInfo;
                if (info == null)
                    return;

                uint startAddr = info.AutoRefreshStartAddress;
                uint startAddrRel2Doc = startAddr - this.actualStartAddress;
                uint countBytes = info.AutoRefreshLength;
                if (startAddr < this.actualStartAddress || (startAddrRel2Doc + countBytes) >= this.DocumentLength) {
                    await IMessageDialogService.Instance.ShowMessage("Start address", $"Auto refresh span is out of range. Document contains {this.actualStartAddress:X8} to {(this.actualStartAddress + this.DocumentLength - 1):X8}");
                    this.UpdateAutoRefreshButtonsAndTextBoxes();
                    return;
                }

                this.currBusyToken = await info.MemoryEngine360.BeginBusyOperationActivityAsync("Starting auto refresh");
                if (this.currBusyToken == null) {
                    this.UpdateAutoRefreshButtonsAndTextBoxes();
                    return;
                }

                this.autoRefreshCts = new CancellationTokenSource();
                this.currentAutoRefresh = ActivityManager.Instance.RunTask(async () => {
                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                        this.OnAutoRefreshStartedOnMainThread(startAddrRel2Doc, countBytes);
                    });

                    ActivityTask task = ActivityManager.Instance.CurrentTask;
                    task.Progress.Caption = "Auto refresh";
                    task.Progress.Text = "Auto refresh in progress";
                    task.Progress.IsIndeterminate = true;
                    while (true) {
                        if (task.CancellationToken.IsCancellationRequested) {
                            break;
                        }

                        IConsoleConnection? connection = info.MemoryEngine360.Connection;
                        if (info.MemoryEngine360.IsShuttingDown || connection?.IsConnected != true) {
                            break;
                        }

                        if (countBytes < 1 || this.myCurrData == null) {
                            break;
                        }

                        try {
                            // if (connection is IHaveIceCubes)
                            //     await ((IHaveIceCubes) connection).DebugFreeze();

                            byte[] buffer = new byte[countBytes];
                            await connection.ReadBytes(startAddr, buffer, 0, countBytes, 0x10000, null, task.CancellationToken);

                            // if (connection is IHaveIceCubes)
                            //     await ((IHaveIceCubes) connection).DebugUnFreeze();

                            await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                                if (!this.PART_HexEditor.Document!.IsReadOnly) {
                                    this.PART_HexEditor.Document!.WriteBytes(startAddrRel2Doc, buffer);
                                }
                                else {
                                    task.TryCancel();
                                }

                                this.UpdateSelectionText();
                                this.UpdateCaretText();
                                return Task.CompletedTask;
                            });
                        }
                        catch {
                            Debugger.Break();
                            break;
                        }

                        try {
                            await Task.Delay(50, task.CancellationToken);
                        }
                        catch (OperationCanceledException) {
                            break;
                        }
                    }

                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(this.OnAutoRefreshStoppedOnMainThread);
                }, this.autoRefreshCts);
            }
        });

        this.autoRefreshAddrBinder = new TextBoxToDataParameterBinder<HexDisplayInfo, uint>(HexDisplayInfo.AutoRefreshStartAddressParameter, (p) => p!.ToString("X8"), async (t, x) => {
            if (uint.TryParse(x, NumberStyles.HexNumber, null, out uint value)) {
                int addrRel2Doc = (int) value - (int) this.actualStartAddress;
                if (addrRel2Doc < 0 || (ulong) addrRel2Doc >= this.DocumentLength) {
                    await IMessageDialogService.Instance.ShowMessage("Start address", $"Address out of range. Document contains {this.actualStartAddress:X8} to {(this.actualStartAddress + this.DocumentLength - 1):X8}");
                    return default;
                }

                uint endAddress = (uint) addrRel2Doc + t.Model.AutoRefreshLength;
                if (endAddress >= this.DocumentLength) {
                    await IMessageDialogService.Instance.ShowMessage("Bytes count", $"Address causes scan to exceed document length. Document contains {this.actualStartAddress:X8} to {(this.actualStartAddress + this.DocumentLength - 1):X8}");
                    return default;
                }

                return value;
            }
            else if (ulong.TryParse(x, NumberStyles.HexNumber, null, out _)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid value", "Address is too long. It can only be 4 bytes", defaultButton: MessageBoxResult.OK);
            }
            else {
                await IMessageDialogService.Instance.ShowMessage("Invalid value", "Start address is invalid", defaultButton: MessageBoxResult.OK);
            }

            return default;
        });

        this.autoRefreshLenBinder = new TextBoxToDataParameterBinder<HexDisplayInfo, uint>(HexDisplayInfo.AutoRefreshLengthParameter, (p) => p!.ToString("X8"), async (t, x) => {
            if (uint.TryParse(x, NumberStyles.HexNumber, null, out uint value)) {
                if (!uint.TryParse(x, NumberStyles.HexNumber, null, out uint countBytes)) {
                    await IMessageDialogService.Instance.ShowMessage("Bytes count", "Invalid byte count for auto refresh");
                    return default;
                }

                uint endAddress = (t.Model.AutoRefreshStartAddress - this.actualStartAddress) + countBytes;
                if (endAddress >= this.DocumentLength) {
                    await IMessageDialogService.Instance.ShowMessage("Bytes count", $"Byte count causes scan to exceed document length. Document contains {this.actualStartAddress:X8} to {(this.actualStartAddress + this.DocumentLength - 1):X8}");
                    return default;
                }

                return value;
            }
            else if (ulong.TryParse(x, NumberStyles.HexNumber, null, out _)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid value", "Address is too long. It can only be 4 bytes", defaultButton: MessageBoxResult.OK);
            }
            else {
                await IMessageDialogService.Instance.ShowMessage("Invalid value", "Length address is invalid", defaultButton: MessageBoxResult.OK);
            }

            return default;
        });

        this.autoRefreshAddrBinder.AttachControl(this.PART_AutoRefresh_From);
        this.autoRefreshLenBinder.AttachControl(this.PART_AutoRefresh_Count);
    }

    // 8303A000

    private void OnAutoRefreshStartedOnMainThread(uint startAddressRel2Doc, uint count) {
        this.PART_ProgressGrid.IsVisible = true;
        this.PART_ControlsGrid.IsEnabled = false;
        this.PART_Progress.IsIndeterminate = true;

        Debug.Assert(this.currBusyToken != null, "Token should not be null");

        this.autoRefreshLayer.SetRange(new BitRange(startAddressRel2Doc, startAddressRel2Doc + count));
        this.autoRefreshLayer.IsActive = true;
        this.UpdateAutoRefreshButtonsAndTextBoxes();

        this.readAllCommand.RaiseCanExecuteChanged();
        this.refreshDataCommand.RaiseCanExecuteChanged();
        this.uploadDataCommand.RaiseCanExecuteChanged();
    }

    private void OnAutoRefreshStoppedOnMainThread() {
        this.PART_ProgressGrid.IsVisible = false;
        this.PART_ControlsGrid.IsEnabled = true;
        this.PART_Progress.IsIndeterminate = false;

        this.currBusyToken!.Dispose();
        this.currBusyToken = null;
        this.autoRefreshLayer.IsActive = false;
        this.currentAutoRefresh = null;
        this.autoRefreshCts!.Dispose();
        this.autoRefreshCts = null;
        this.UpdateAutoRefreshButtonsAndTextBoxes();

        this.readAllCommand.RaiseCanExecuteChanged();
        this.refreshDataCommand.RaiseCanExecuteChanged();
        this.uploadDataCommand.RaiseCanExecuteChanged();
    }

    private bool IsPointerInRange(uint value) {
        return value >= this.actualStartAddress && value < (this.actualStartAddress + this.DocumentLength);
    }

    private void NavigateToPointer() {
        if (this.myCurrData == null) {
            return;
        }

        ulong caretIndex = this.SelectionRange.Start.ByteIndex;
        bool displayAsLE = this.TheEndianness == Endianness.LittleEndian;
        int cbRemaining = this.myCurrData.Length - (int) caretIndex;
        uint val32 = cbRemaining >= 4 ? MemoryMarshal.Read<UInt32>(new ReadOnlySpan<byte>(this.myCurrData, (int) caretIndex, 4)) : 0;
        if (displayAsLE != BitConverter.IsLittleEndian) {
            val32 = BinaryPrimitives.ReverseEndianness(val32);
        }

        if (this.IsPointerInRange(val32)) {
            this.MoveCursor(val32 - this.actualStartAddress, 4);
        }
    }

    private void MoveCursorForDataType(int incr) {
        int len = incr < 0 ? -incr : incr;
        BitLocation caret = this.CaretLocation;
        this.MoveCursor((long) caret.ByteIndex + incr, len);
    }

    private void MoveCursor(long location, long selectionLength) {
        BitLocation caret = new BitLocation((ulong) Math.Clamp(location, 0, (long) this.DocumentLength));
        this.CaretLocation = caret;
        this.SelectionRange = new BitRange(caret, caret.AddBytes((ulong) selectionLength));
    }

    public async Task ReadAllFromConsoleCommand() {
        HexDisplayInfo? info = this.HexDisplayInfo;
        if (info == null || this.currentAutoRefresh != null) {
            return;
        }

        if (info.Length < 1) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Length is zero.", defaultButton: MessageBoxResult.OK);
            return;
        }

        this.PART_ProgressGrid.IsVisible = true;
        this.PART_ControlsGrid.IsEnabled = false;
        this.PART_Progress.IsIndeterminate = true;

        BitRange selection = this.SelectionRange;
        byte[]? bytes = await info.MemoryEngine360.BeginBusyOperationActivityAsync(async (t, c) => {
            using CancellationTokenSource cts = new CancellationTokenSource();
            return await ActivityManager.Instance.RunTask(async () => {
                if (c is IHaveIceCubes)
                    await ((IHaveIceCubes) c).DebugFreeze();

                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = "Read data for Hex Editor";

                SimpleCompletionState completion = new SimpleCompletionState();
                completion.CompletionValueChanged += state => {
                    task.Progress.CompletionState.TotalCompletion = state.TotalCompletion;
                    task.Progress.Text = $"Reading {ValueScannerUtils.ByteFormatter.ToString(info.Length * state.TotalCompletion, false)}/{ValueScannerUtils.ByteFormatter.ToString(info.Length, false)}";
                };

                // Update initial text
                completion.OnCompletionValueChanged();
                byte[] buffer = new byte[info.Length];
                await c.ReadBytes(info.StartAddress, buffer, 0, info.Length, 0x10000, completion, task.CancellationToken);
                if (c is IHaveIceCubes)
                    await ((IHaveIceCubes) c).DebugUnFreeze();

                return buffer;
            }, cts);
        }, "Read data for Hex Editor");

        this.PART_ProgressGrid.IsVisible = false;
        this.PART_ControlsGrid.IsEnabled = true;
        this.PART_Progress.IsIndeterminate = false;
        if (bytes != null) {
            Vector scroll = this.PART_HexEditor.HexView.ScrollOffset;
            BitLocation location = this.CaretLocation;

            this.actualStartAddress = info.StartAddress;
            this.myOffsetColumn.AdditionalOffset = info.StartAddress;
            this.myCurrData = bytes;
            this.PART_HexEditor.Document = new MemoryBinaryDocument(this.myCurrData, info.IsReadOnly);
            await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                this.PART_HexEditor.HexView.ScrollOffset = scroll;
                this.CaretLocation = location;
                this.SelectionRange = selection;
            }, DispatchPriority.INTERNAL_BeforeRender);

            this.UpdateSelectionText();
            this.UpdateCaretText();
        }
    }

    public Task ReloadSelectionFromConsole() {
        BitRange selection = this.SelectionRange;
        uint count = (uint) selection.ByteLength;
        uint start = (uint) selection.Start.ByteIndex;
        return this.ReloadSelectionFromConsole(start, count);
    }

    public async Task ReloadSelectionFromConsole(uint startRel2Doc, uint length) {
        HexDisplayInfo? info = this.HexDisplayInfo;
        if (info == null || this.currentAutoRefresh != null) {
            return;
        }
        
        if (length < 1 || this.myCurrData == null || this.PART_HexEditor.Document!.IsReadOnly) {
            return;
        }

        this.PART_ProgressGrid.IsVisible = true;
        this.PART_ControlsGrid.IsEnabled = false;
        this.PART_Progress.IsIndeterminate = true;

        byte[]? readBuffer = await info.MemoryEngine360.BeginBusyOperationActivityAsync(async (t, c) => {
            using CancellationTokenSource cts = new CancellationTokenSource();
            return await ActivityManager.Instance.RunTask(async () => {
                if (c is IHaveIceCubes)
                    await ((IHaveIceCubes) c).DebugFreeze();

                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = "Refresh data for Hex Editor";

                SimpleCompletionState completion = new SimpleCompletionState();
                completion.CompletionValueChanged += state => {
                    task.Progress.CompletionState.TotalCompletion = state.TotalCompletion;
                    task.Progress.Text = $"Reading {ValueScannerUtils.ByteFormatter.ToString(length * state.TotalCompletion, false)}/{ValueScannerUtils.ByteFormatter.ToString(length, false)}";
                };

                // Update initial text
                completion.OnCompletionValueChanged();

                byte[] buffer = new byte[length];
                await c.ReadBytes(this.actualStartAddress + startRel2Doc, buffer, 0, length, 0x10000, completion, task.CancellationToken);
                if (c is IHaveIceCubes)
                    await ((IHaveIceCubes) c).DebugUnFreeze();
                return buffer;
            }, cts);
        }, "Read data for Hex Editor");

        this.PART_ProgressGrid.IsVisible = false;
        this.PART_ControlsGrid.IsEnabled = true;
        this.PART_Progress.IsIndeterminate = false;

        if (readBuffer != null) {
            this.PART_HexEditor.Document!.WriteBytes(startRel2Doc, readBuffer);
        }

        this.UpdateSelectionText();
        this.UpdateCaretText();
    }

    public async Task UploadSelectionToConsoleCommand() {
        HexDisplayInfo? info = this.HexDisplayInfo;
        if (info == null || this.currentAutoRefresh != null) {
            return;
        }

        byte[]? buffer = this.myCurrData;
        if (buffer == null) {
            return;
        }

        BitRange selection = this.SelectionRange;
        uint count = (uint) selection.ByteLength;
        if (count < 1) {
            await IMessageDialogService.Instance.ShowMessage("No selection", "Please make a selection to upload. Click CTRL+A to select all.", defaultButton: MessageBoxResult.OK);
            return;
        }

        this.PART_ProgressGrid.IsVisible = true;
        this.PART_ControlsGrid.IsEnabled = false;
        this.PART_Progress.IsIndeterminate = true;
        uint start = (uint) selection.Start.ByteIndex;
        await info.MemoryEngine360.BeginBusyOperationActivityAsync(async (t, c) => {
            using CancellationTokenSource cts = new CancellationTokenSource();
            await ActivityManager.Instance.RunTask(async () => {
                if (c is IHaveIceCubes)
                    await ((IHaveIceCubes) c).DebugFreeze();

                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = "Write data from Hex Editor";

                SimpleCompletionState completion = new SimpleCompletionState();
                completion.CompletionValueChanged += state => {
                    task.Progress.CompletionState.TotalCompletion = state.TotalCompletion;
                    task.Progress.Text = $"Writing {ValueScannerUtils.ByteFormatter.ToString(selection.ByteLength * state.TotalCompletion, false)}/{ValueScannerUtils.ByteFormatter.ToString(selection.ByteLength, false)}";
                };

                // Update initial text
                completion.OnCompletionValueChanged();
                await c.WriteBytes(this.actualStartAddress + start, buffer, (int) start, count, 0x10000, completion, task.CancellationToken);
                if (c is IHaveIceCubes)
                    await ((IHaveIceCubes) c).DebugUnFreeze();
            }, cts);
        }, "Write Hex Editor Data");

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

        this.UpdateDataInspector();
        this.UpdateAutoRefreshSelectionDependentShit();
    }

    private void UpdateCaretText() {
        Caret caret = this.PART_HexEditor.Caret;
        BitLocation pos = caret.Location;
        this.PART_CaretText.Text = $"{(this.actualStartAddress + pos.ByteIndex):X8} ({pos.ByteIndex:X} from start)";
        this.UpdateDataInspector();
        this.UpdateAutoRefreshSelectionDependentShit();
    }

    static HexDisplayControl() {
        HexDisplayInfoProperty.Changed.AddClassHandler<HexDisplayControl, HexDisplayInfo?>((o, e) => o.OnInfoChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    protected override void OnWindowOpened() {
        base.OnWindowOpened();
        this.Window!.Control.MinWidth = 800;
        this.Window!.Control.MinHeight = 480;
        this.Window!.Control.Width = 1280;
        this.Window!.Control.Height = 720;

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
        this.autoRefreshAddrBinder.SwitchModel(newData);
        this.autoRefreshLenBinder.SwitchModel(newData);

        if (oldData != null) {
            HexDisplayInfo.AutoRefreshStartAddressParameter.RemoveValueChangedHandler(oldData, this.OnARStartOrCountParamChanged);
            HexDisplayInfo.AutoRefreshLengthParameter.RemoveValueChangedHandler(oldData, this.OnARStartOrCountParamChanged);
        }
        
        if (newData != null) {
            HexDisplayInfo.AutoRefreshStartAddressParameter.AddValueChangedHandler(newData, this.OnARStartOrCountParamChanged);
            HexDisplayInfo.AutoRefreshLengthParameter.AddValueChangedHandler(newData, this.OnARStartOrCountParamChanged);
        }
        
        if (newData != null) {
            this.PART_CancelButton.Focus();
        }
    }

    private void OnARStartOrCountParamChanged(DataParameter parameter, ITransferableData owner) {
        this.UpdateARLRange();
    }

    private void OnCancelButtonClicked(object? sender, RoutedEventArgs e) {
        this.Window!.Close();
    }

    private void UpdateARLRange() {
        HexDisplayInfo? info = this.HexDisplayInfo;
        if (info != null) {
            BitRange range = new BitRange(info.AutoRefreshStartAddress - this.actualStartAddress, (info.AutoRefreshStartAddress + info.AutoRefreshLength) - this.actualStartAddress);
            this.autoRefreshLayer.SetRange(range);
        }
    }

    private void UpdateDataInspector() {
        BitRange selection = this.SelectionRange;
        ulong caretIndex = selection.Start.ByteIndex;
        if (this.myCurrData == null) {
            return;
        }

        // Word/int32:
        // 00        C0        FF        EE
        // 0000 0000 1100 0000 1111 1111 1110 1110
        // ^(bit 31)                      (bit 0)^
        // MSB                                 LSB

        this.lastEndianness = this.theEndianness;

        // The console is big-endian. If we want to display as little endian, we need to reverse the bytes
        bool displayAsLE = this.TheEndianness == Endianness.LittleEndian;
        int cbRemaining = this.myCurrData.Length - (int) caretIndex;

        byte val08 = cbRemaining >= 1 ? this.myCurrData[caretIndex] : default;
        ushort val16 = cbRemaining >= 2 ? MemoryMarshal.Read<UInt16>(new ReadOnlySpan<byte>(this.myCurrData, (int) caretIndex, 2)) : default;
        uint val32 = cbRemaining >= 4 ? MemoryMarshal.Read<UInt32>(new ReadOnlySpan<byte>(this.myCurrData, (int) caretIndex, 4)) : 0;
        ulong val64 = cbRemaining >= 8 ? MemoryMarshal.Read<UInt64>(new ReadOnlySpan<byte>(this.myCurrData, (int) caretIndex, 8)) : 0;

        // On LE systems, the LSB is on the right side of a value in the hex editor.
        // Therefore, we have to flip the bytes (unless the user wants to see them as LE).
        // The hex editor displays 0xF894, but on LE, val16 would actually be read as 0x94F8.
        if (displayAsLE != BitConverter.IsLittleEndian) {
            val16 = BinaryPrimitives.ReverseEndianness(val16);
            val32 = BinaryPrimitives.ReverseEndianness(val32);
            val64 = BinaryPrimitives.ReverseEndianness(val64);
        }

        bool asHex = this.PART_DisplayIntAsHex.IsChecked == true;
        this.PART_Binary8.Text = val08.ToString("B8");
        this.PART_Int8.Text = asHex ? ((sbyte) val08).ToString("X2") : ((sbyte) val08).ToString();
        this.PART_UInt8.Text = asHex ? val08.ToString("X2") : val08.ToString();
        this.PART_Int16.Text = asHex ? ((short) val16).ToString("X4") : ((short) val16).ToString();
        this.PART_UInt16.Text = asHex ? val16.ToString("X4") : val16.ToString();
        this.PART_Int32.Text = asHex ? ((int) val32).ToString("X8") : ((int) val32).ToString();
        this.PART_UInt32.Text = asHex ? val32.ToString("X8") : val32.ToString();
        this.PART_Int64.Text = asHex ? ((long) val64).ToString("X16") : ((long) val64).ToString();
        this.PART_UInt64.Text = asHex ? val64.ToString("X16") : val64.ToString();
        this.PART_Float.Text = Unsafe.As<uint, float>(ref val32).ToString();
        this.PART_Double.Text = Unsafe.As<ulong, double>(ref val64).ToString();
        this.PART_CharUTF8.Text = ((char) val08).ToString();
        this.PART_CharUTF16.Text = ((char) val16).ToString();
        this.PART_CharUTF32.Text = Encoding.UTF32.GetString(new ReadOnlySpan<byte>(ref Unsafe.As<uint, byte>(ref val32)));

        this.PART_BtnGoToPointerInt32.IsEnabled = this.IsPointerInRange(val32);
    }

    private void UpdateAutoRefreshButtonsAndTextBoxes() {
        bool isRunning = this.currentAutoRefresh != null;

        this.PART_ToggleAutoRefreshButton.Content = isRunning ? "Stop Auto Refresh" : "Start Auto Refresh";
        this.PART_ToggleAutoRefreshButton.IsChecked = isRunning;
        this.PART_AutoRefresh_From.IsEnabled = !isRunning;
        this.PART_AutoRefresh_Count.IsEnabled = !isRunning;
        ((AsyncRelayCommand) this.PART_ToggleAutoRefreshButton.Command!).RaiseCanExecuteChanged();
        this.UpdateAutoRefreshSelectionDependentShit();
    }

    private void UpdateAutoRefreshSelectionDependentShit() {
        this.PART_SetAutoRefreshRangeAsSelection.IsEnabled = this.currentAutoRefresh == null && this.SelectionRange.ByteLength > 0;
        this.PART_ClearAutoRefreshRange.IsEnabled = this.currentAutoRefresh == null;
    }
}