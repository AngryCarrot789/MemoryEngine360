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
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaHex.Core.Document;
using AvaloniaHex.Editing;
using AvaloniaHex.Rendering;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.HexEditing;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.Scanners;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.Enums;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Contexts;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Avalonia.Shortcuts.Avalonia;
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Tasks.Pausable;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.Services.HexEditing;

public partial class HexEditorWindow : DesktopWindow, IHexEditorUI {
    public static readonly StyledProperty<HexEditorInfo?> HexDisplayInfoProperty = AvaloniaProperty.Register<HexEditorWindow, HexEditorInfo?>("HexDisplayInfo");

    public HexEditorInfo? HexDisplayInfo {
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

    public ulong DocumentLength => this.myDocument?.Length ?? 0;

    public uint CurrentStartOffset => this.actualStartAddress;

    private readonly OffsetColumn myOffsetColumn;

    public delegate void HexDisplayControlTheEndiannessChangedEventHandler(HexEditorWindow sender);

    private readonly AvaloniaPropertyToDataParameterBinder<HexEditorInfo> captionBinder = new AvaloniaPropertyToDataParameterBinder<HexEditorInfo>(TitleProperty, HexEditorInfo.CaptionParameter);

    private readonly TextBoxToDataParameterBinder<HexEditorInfo, uint> addrBinder = new TextBoxToDataParameterBinder<HexEditorInfo, uint>(HexEditorInfo.StartAddressParameter, (p) => p!.ToString("X8"), async (t, x) => {
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

    private readonly TextBoxToDataParameterBinder<HexEditorInfo, uint> lenBinder = new TextBoxToDataParameterBinder<HexEditorInfo, uint>(HexEditorInfo.LengthParameter, (p) => p!.ToString("X8"), async (t, x) => {
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

    private readonly TextBoxToDataParameterBinder<HexEditorInfo, uint> bytesPerRowBinder = new TextBoxToDataParameterBinder<HexEditorInfo, uint>(HexEditorInfo.BytesPerRowParameter, (p) => p!.ToString(), async (t, x) => {
        if (uint.TryParse(x, out uint value)) {
            DataParameterNumber<uint> p = HexEditorInfo.BytesPerRowParameter;
            if (value == 0) {
                await IMessageDialogService.Instance.ShowMessage("Invalid value", "Cannot display 0 bytes per row. Are you crazy??!?", defaultButton: MessageBoxResult.OK);
            }
            else if (p.IsValueOutOfRange(value)) {
                // will probably cause the computer so implode or something if there's too many or little bpr
                await IMessageDialogService.Instance.ShowMessage("Out of range", $"Cannot display less than {p.Minimum} or more than {p.Maximum} bytes per row", defaultButton: MessageBoxResult.OK);
            }
            else {
                return value;
            }
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Start address is invalid", defaultButton: MessageBoxResult.OK);
        }

        return default;
    });

    private readonly IBinder<HexEditorInfo> autoRefreshAddrBinder;
    private readonly IBinder<HexEditorInfo> autoRefreshLenBinder;

    private readonly DataParameterEnumBinder<Endianness> endiannessBinder = new DataParameterEnumBinder<Endianness>(HexEditorInfo.InspectorEndiannessParameter);

    private readonly AsyncRelayCommand readAllCommand, refreshDataCommand, uploadDataCommand;

    private readonly record struct UploadTextBoxInfo(TextBox TextBox, DataType DataType, bool IsUnsigned);

    private readonly AsyncRelayCommand<UploadTextBoxInfo> parseTextBoxAndUploadCommand;

    private uint actualStartAddress;
    private AutoRefreshTask? autoRefreshTask;

    private MemoryBinaryDocument? myDocument;

    private readonly AutoRefreshLayer autoRefreshLayer;
    private readonly HexEditorChangeManager changeManager;
    private readonly AsyncRelayCommand runAutoRefreshCommand;

    public HexEditorWindow() {
        this.InitializeComponent();
        this.captionBinder.AttachControl(this);
        this.addrBinder.AttachControl(this.PART_AddressTextBox);
        this.lenBinder.AttachControl(this.PART_LengthTextBox);

        this.bytesPerRowBinder.AttachControl(this.PART_BytesPerRowTextBox);
        this.bytesPerRowBinder.PostUpdateControl += b => {
            this.PART_HexEditor.HexView.BytesPerLine = (int) this.HexDisplayInfo!.BytesPerRow;
        };

        HexView view = this.PART_HexEditor.HexView;
        view.BytesPerLine = 32;
        view.Columns.Add(this.myOffsetColumn = new OffsetColumn());
        view.Columns.Add(new HexColumn());
        view.Columns.Add(new AsciiColumn());
        view.Layers.InsertBefore<TextLayer>(this.autoRefreshLayer = new AutoRefreshLayer(this.PART_HexEditor.Caret));
        this.changeManager = new HexEditorChangeManager(this.PART_HexEditor);

        this.PART_DisplayIntAsHex.IsCheckedChanged += (sender, args) => this.UpdateDataInspector();

        this.endiannessBinder.Assign(this.PART_LittleEndian, Endianness.LittleEndian);
        this.endiannessBinder.Assign(this.PART_BigEndian, Endianness.BigEndian);

        this.PART_CancelButton.Click += this.OnCancelButtonClicked;
        this.readAllCommand = new AsyncRelayCommand(async () => {
            await this.ReadAllFromConsoleCommand();
        }, () => this.autoRefreshTask == null);

        this.refreshDataCommand = new AsyncRelayCommand(async () => {
            await this.ReloadSelectionFromConsole();
        }, () => {
            if (this.autoRefreshTask != null)
                return false;

            BitRange selection = this.SelectionRange;
            return selection.ByteLength > 0 && this.myDocument != null && !this.myDocument!.IsReadOnly;
        });

        this.uploadDataCommand = new AsyncRelayCommand(async () => {
            await this.UploadSelectionToConsoleCommand();
        }, () => {
            if (this.autoRefreshTask != null)
                return false;

            BitRange selection = this.SelectionRange;
            return selection.ByteLength > 0 && this.myDocument != null && !this.myDocument!.IsReadOnly;
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

        this.PART_Int8.KeyDown += this.OnDataInspectorNumericTextBoxKeyDown;
        this.PART_UInt8.KeyDown += this.OnDataInspectorNumericTextBoxKeyDown;
        this.PART_Int16.KeyDown += this.OnDataInspectorNumericTextBoxKeyDown;
        this.PART_UInt16.KeyDown += this.OnDataInspectorNumericTextBoxKeyDown;
        this.PART_Int32.KeyDown += this.OnDataInspectorNumericTextBoxKeyDown;
        this.PART_UInt32.KeyDown += this.OnDataInspectorNumericTextBoxKeyDown;
        this.PART_Int64.KeyDown += this.OnDataInspectorNumericTextBoxKeyDown;
        this.PART_UInt64.KeyDown += this.OnDataInspectorNumericTextBoxKeyDown;
        this.PART_Float.KeyDown += this.OnDataInspectorNumericTextBoxKeyDown;
        this.PART_Double.KeyDown += this.OnDataInspectorNumericTextBoxKeyDown;

        this.parseTextBoxAndUploadCommand = new AsyncRelayCommand<UploadTextBoxInfo>(this.ParseTextBoxAndUpload, isParamRequired: true);

        this.PART_BtnGoToPointerInt32.Click += (s, e) => this.NavigateToPointer();
        this.PART_ToggleAutoRefreshButton.Command = this.runAutoRefreshCommand = new AsyncRelayCommand(async () => {
            if (this.autoRefreshTask != null) {
                // We are running, so stop it
                await this.autoRefreshTask.CancelAsync();
            }
            else {
                HexEditorInfo? info = this.HexDisplayInfo;
                if (info == null)
                    return;

                uint arStartAddress = info.AutoRefreshStartAddress;
                uint arCountBytes = info.AutoRefreshLength;
                if (arStartAddress == 0 && arCountBytes == 0) {
                    BitRange selection = this.SelectionRange;
                    if (selection.ByteLength > 0) {
                        MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Auto refresh", "Auto refresh span is empty. Set span as selection and run?", MessageBoxButton.OKCancel, MessageBoxResult.OK);
                        if (result != MessageBoxResult.OK) {
                            return;
                        }

                        info.AutoRefreshStartAddress = arStartAddress = (uint) selection.Start.ByteIndex + this.actualStartAddress;
                        info.AutoRefreshLength = arCountBytes = (uint) selection.ByteLength;
                    }
                    else {
                        await IMessageDialogService.Instance.ShowMessage("Auto refresh", "Auto refresh span is empty", defaultButton: MessageBoxResult.OK);
                        return;
                    }
                }

                uint startAddrRel2Doc = arStartAddress - this.actualStartAddress;
                if (arStartAddress < this.actualStartAddress || (startAddrRel2Doc + arCountBytes) > this.DocumentLength) {
                    await IMessageDialogService.Instance.ShowMessage("Start address", $"Auto refresh span is out of range. Document contains {this.actualStartAddress:X8} to {(this.actualStartAddress + this.DocumentLength - 1):X8}");
                    this.UpdateAutoRefreshButtonsAndTextBoxes();
                    return;
                }

                this.autoRefreshTask = new AutoRefreshTask(this, startAddrRel2Doc, arCountBytes);
                this.autoRefreshTask.Run();
            }
        });

        this.autoRefreshAddrBinder = new TextBoxToDataParameterBinder<HexEditorInfo, uint>(HexEditorInfo.AutoRefreshStartAddressParameter, (p) => p.ToString("X8"), async (t, x) => {
            if (uint.TryParse(x, NumberStyles.HexNumber, null, out uint newStartAddress)) {
                int addrRel2Doc = (int) newStartAddress - (int) this.actualStartAddress;
                if (addrRel2Doc < 0 || (ulong) addrRel2Doc >= this.DocumentLength) {
                    await IMessageDialogService.Instance.ShowMessage("Start address", $"Address out of range. Document contains {this.actualStartAddress:X8} to {(this.actualStartAddress + this.DocumentLength - 1):X8}");
                    return default;
                }

                uint endAddress = (uint) addrRel2Doc + t.Model.AutoRefreshLength;
                if (endAddress >= this.DocumentLength) {
                    await IMessageDialogService.Instance.ShowMessage("Bytes count", $"Address causes scan to exceed document length. Document contains {this.actualStartAddress:X8} to {(this.actualStartAddress + this.DocumentLength - 1):X8}");
                    return default;
                }

                return newStartAddress;
            }
            else if (ulong.TryParse(x, NumberStyles.HexNumber, null, out _)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid value", "Address is too long. Maximum is technically 0xFFFFFFFF", defaultButton: MessageBoxResult.OK);
            }
            else {
                await IMessageDialogService.Instance.ShowMessage("Invalid value", "Start address is invalid", defaultButton: MessageBoxResult.OK);
            }

            return default;
        }, (b) => this.UpdateAutoRefreshRange());

        this.autoRefreshLenBinder = new TextBoxToDataParameterBinder<HexEditorInfo, uint>(HexEditorInfo.AutoRefreshLengthParameter, (p) => p.ToString("X8"), async (t, x) => {
            if (uint.TryParse(x, NumberStyles.HexNumber, null, out uint newByteCount)) {
                uint endAddress = (t.Model.AutoRefreshStartAddress - this.actualStartAddress) + newByteCount;
                if (endAddress >= this.DocumentLength) {
                    await IMessageDialogService.Instance.ShowMessage("Bytes count", $"Byte count causes scan to exceed document length. Document contains {this.actualStartAddress:X8} to {(this.actualStartAddress + this.DocumentLength - 1):X8}");
                    return default;
                }

                return newByteCount;
            }
            else if (ulong.TryParse(x, NumberStyles.HexNumber, null, out _)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid value", "Length is too long. Maximum is technically 0xFFFFFFFF", defaultButton: MessageBoxResult.OK);
            }
            else {
                await IMessageDialogService.Instance.ShowMessage("Invalid value", "Length address is invalid", defaultButton: MessageBoxResult.OK);
            }

            return default;
        }, (b) => this.UpdateAutoRefreshRange());

        this.autoRefreshAddrBinder.AttachControl(this.PART_AutoRefresh_From);
        this.autoRefreshLenBinder.AttachControl(this.PART_AutoRefresh_Count);

        this.UpdateAutoRefreshButtonsAndTextBoxes();
    }

    private void OnDataInspectorNumericTextBoxKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key != Key.Enter) {
            return;
        }

        TextBox tb = (TextBox) sender!;
        switch (tb.Name) {
            case nameof(this.PART_Int8):   this.parseTextBoxAndUploadCommand.Execute(new UploadTextBoxInfo(tb, DataType.Byte, false)); break;
            case nameof(this.PART_UInt8):  this.parseTextBoxAndUploadCommand.Execute(new UploadTextBoxInfo(tb, DataType.Byte, true)); break;
            case nameof(this.PART_Int16):  this.parseTextBoxAndUploadCommand.Execute(new UploadTextBoxInfo(tb, DataType.Int16, false)); break;
            case nameof(this.PART_UInt16): this.parseTextBoxAndUploadCommand.Execute(new UploadTextBoxInfo(tb, DataType.Int16, true)); break;
            case nameof(this.PART_Int32):  this.parseTextBoxAndUploadCommand.Execute(new UploadTextBoxInfo(tb, DataType.Int32, false)); break;
            case nameof(this.PART_UInt32): this.parseTextBoxAndUploadCommand.Execute(new UploadTextBoxInfo(tb, DataType.Int32, true)); break;
            case nameof(this.PART_Int64):  this.parseTextBoxAndUploadCommand.Execute(new UploadTextBoxInfo(tb, DataType.Int64, false)); break;
            case nameof(this.PART_UInt64): this.parseTextBoxAndUploadCommand.Execute(new UploadTextBoxInfo(tb, DataType.Int64, true)); break;
            case nameof(this.PART_Float):  this.parseTextBoxAndUploadCommand.Execute(new UploadTextBoxInfo(tb, DataType.Float, false)); break;
            case nameof(this.PART_Double): this.parseTextBoxAndUploadCommand.Execute(new UploadTextBoxInfo(tb, DataType.Double, false)); break;
        }
    }

    private async Task ParseTextBoxAndUpload(UploadTextBoxInfo info) {
        Debug.Assert(info.DataType.IsNumeric(), "Cannot upload non-numeric data as of yet");

        MemoryEngine360 engine = this.HexDisplayInfo!.MemoryEngine360;
        if (engine.Connection == null) {
            await IMessageDialogService.Instance.ShowMessage("No connection", "Not connected to any console", defaultButton: MessageBoxResult.OK);
            return;
        }

        NumericDisplayType intNdt = info.DataType.IsInteger() && this.PART_DisplayIntAsHex.IsChecked == true ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;

        string input = info.TextBox.Text ?? "";
        // Custom case for signed byte. Why TF did I add a signed byte row to the data inspector???
        if (info.DataType == DataType.Byte && !info.IsUnsigned) {
            if (!sbyte.TryParse(input, intNdt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer, null, out sbyte sb)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid text", "Invalid signed byte", defaultButton: MessageBoxResult.OK);
                return;
            }

            // cheese it
            input = ((byte) sb).ToString();
        }

        ValidationArgs args = new ValidationArgs(input, new List<string>(), false);
        if (!MemoryEngine360.TryParseTextAsDataValue(args, info.DataType, intNdt, StringType.ASCII, out IDataValue? value)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid text", args.Errors.Count > 0 ? args.Errors[0] : "Could not parse value as " + info.DataType, defaultButton: MessageBoxResult.OK);
            return;
        }

        ulong caretIndex = this.SelectionRange.Start.ByteIndex;
        await this.PerformOperationBetweenAutoRefresh(async () => {
            IConsoleConnection connection;
            using IDisposable? token = await engine.BeginBusyOperationActivityAsync("Upload DI value");
            if (token != null && (connection = engine.Connection) != null && connection.IsConnected) {
                await MemoryEngine360.WriteAsDataValue(connection, (uint) caretIndex + this.actualStartAddress, value);
                if (this.PART_ToggleShowChanges.IsChecked == true && this.myDocument != null && !this.myDocument.IsReadOnly) {
                    int dataLength = 0;
                    switch (info.DataType) {
                        case DataType.Byte:   dataLength = 1; break;
                        case DataType.Int16:  dataLength = 2; break;
                        case DataType.Int32:  dataLength = 4; break;
                        case DataType.Int64:  dataLength = 8; break;
                        case DataType.Float:  dataLength = 4; break;
                        case DataType.Double: dataLength = 8; break;
                    }

                    if (dataLength > 0) {
                        byte[] buffer = await connection.ReadBytes((uint) (this.actualStartAddress + caretIndex), dataLength);

                        if (this.PART_ToggleShowChanges.IsChecked == true)
                            this.changeManager.ProcessChanges((uint) caretIndex, buffer, buffer.Length);
                        this.myDocument!.WriteBytes((uint) caretIndex, buffer);

                        this.UpdateDataInspector();
                    }
                }
            }
        });
    }

    private async Task PerformOperationBetweenAutoRefresh(Func<Task> operation) {
        if (this.autoRefreshTask == null) {
            await operation();
        }
        else {
            await this.autoRefreshTask.OperateWhilePaused(operation);
            this.runAutoRefreshCommand.RaiseCanExecuteChanged();
        }
    }

    private bool IsPointerInRange(uint value) {
        return value >= this.actualStartAddress && value < (this.actualStartAddress + this.DocumentLength);
    }

    private void NavigateToPointer() {
        HexEditorInfo? info = this.HexDisplayInfo;
        if (this.myDocument == null || info == null) {
            return;
        }

        ulong caretIndex = this.SelectionRange.Start.ByteIndex;
        int cbRemaining = (int) (this.myDocument.Length - caretIndex);
        if (cbRemaining < 4) {
            return;
        }

        Span<byte> buffer = stackalloc byte[4];
        this.myDocument.ReadBytes(caretIndex, buffer);
        uint val32 = MemoryMarshal.Read<UInt32>(buffer);
        bool displayAsLE = info.InspectorEndianness == Endianness.LittleEndian;
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
        HexEditorInfo? info = this.HexDisplayInfo;
        if (info == null || this.autoRefreshTask != null) {
            return;
        }

        if (info.Length < 1) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Length is zero.", defaultButton: MessageBoxResult.OK);
            return;
        }

        this.PART_ControlsGrid.IsEnabled = false;
        BitRange selection = this.SelectionRange;
        byte[]? bytes = await info.MemoryEngine360.BeginBusyOperationActivityAsync(async (t, c) => {
            using CancellationTokenSource cts = new CancellationTokenSource();
            return await ActivityManager.Instance.RunTask(async () => {
                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = "Read data for Hex Editor";
                task.Progress.Text = "Freezing console...";
                if (c is IHaveIceCubes)
                    await ((IHaveIceCubes) c).DebugFreeze();

                SimpleCompletionState completion = new SimpleCompletionState();
                completion.CompletionValueChanged += state => {
                    task.Progress.CompletionState.TotalCompletion = state.TotalCompletion;
                    task.Progress.Text = $"Reading {ValueScannerUtils.ByteFormatter.ToString(info.Length * state.TotalCompletion, false)}/{ValueScannerUtils.ByteFormatter.ToString(info.Length, false)}";
                };

                // Update initial text
                completion.OnCompletionValueChanged();
                byte[] buffer = new byte[info.Length];
                await c.ReadBytes(info.StartAddress, buffer, 0, buffer.Length, 0x10000, completion, task.CancellationToken);

                task.Progress.Text = "Unfreezing console...";
                if (c is IHaveIceCubes)
                    await ((IHaveIceCubes) c).DebugUnFreeze();

                return buffer;
            }, cts);
        }, "Read data for Hex Editor");

        this.PART_ControlsGrid.IsEnabled = true;
        if (bytes != null) {
            Vector scroll = this.PART_HexEditor.HexView.ScrollOffset;
            BitLocation location = this.CaretLocation;

            this.actualStartAddress = info.StartAddress;
            this.myOffsetColumn.AdditionalOffset = info.StartAddress;

            if (this.myDocument != null && this.PART_ToggleShowChanges.IsChecked == true)
                this.changeManager.ProcessChanges(0, bytes, bytes.Length);

            this.PART_HexEditor.Document = info.Document = this.myDocument = new MemoryBinaryDocument(bytes, info.IsReadOnly);
            this.changeManager.OnDocumentChanged(this.myDocument);
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
        int count = (int) Math.Min(selection.ByteLength, int.MaxValue);
        uint start = (uint) Math.Min(selection.Start.ByteIndex, uint.MaxValue);
        return this.ReloadSelectionFromConsole(start, count);
    }

    public async Task ReloadSelectionFromConsole(uint startRel2Doc, int length) {
        HexEditorInfo? info = this.HexDisplayInfo;
        if (info == null || this.autoRefreshTask != null) {
            return;
        }

        if (length < 1 || this.myDocument == null || this.myDocument!.IsReadOnly) {
            return;
        }

        if (this.actualStartAddress + startRel2Doc < this.actualStartAddress) {
            return; // integer overflow
        }

        this.PART_ControlsGrid.IsEnabled = false;
        byte[]? readBuffer = await info.MemoryEngine360.BeginBusyOperationActivityAsync(async (t, c) => {
            using CancellationTokenSource cts = new CancellationTokenSource();
            return await ActivityManager.Instance.RunTask(async () => {
                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = "Refresh data for Hex Editor";
                task.Progress.Text = "Freezing console...";

                if (c is IHaveIceCubes)
                    await ((IHaveIceCubes) c).DebugFreeze();

                SimpleCompletionState completion = new SimpleCompletionState();
                completion.CompletionValueChanged += state => {
                    task.Progress.CompletionState.TotalCompletion = state.TotalCompletion;
                    task.Progress.Text = $"Reading {ValueScannerUtils.ByteFormatter.ToString(length * state.TotalCompletion, false)}/{ValueScannerUtils.ByteFormatter.ToString(length, false)}";
                };

                // Update initial text
                completion.OnCompletionValueChanged();
                byte[] buffer = new byte[length];
                await c.ReadBytes(this.actualStartAddress + startRel2Doc, buffer, 0, length, 0x10000, completion, task.CancellationToken);

                task.Progress.Text = "Unfreezing console...";
                if (c is IHaveIceCubes)
                    await ((IHaveIceCubes) c).DebugUnFreeze();
                return buffer;
            }, cts);
        }, "Read data for Hex Editor");

        this.PART_ControlsGrid.IsEnabled = true;
        if (readBuffer != null) {
            if (this.PART_ToggleShowChanges.IsChecked == true)
                this.changeManager.ProcessChanges(startRel2Doc, readBuffer, readBuffer.Length);
            this.myDocument!.WriteBytes(startRel2Doc, readBuffer);
        }

        this.UpdateSelectionText();
        this.UpdateCaretText();
    }

    public async Task UploadSelectionToConsoleCommand() {
        HexEditorInfo? info = this.HexDisplayInfo;
        if (this.myDocument == null || info == null || this.autoRefreshTask != null) {
            return;
        }

        BitRange selection = this.SelectionRange;
        int count = (int) Math.Min(selection.ByteLength, int.MaxValue);
        if (count < 1) {
            await IMessageDialogService.Instance.ShowMessage("No selection", "Please make a selection to upload. Click CTRL+A to select all.", defaultButton: MessageBoxResult.OK);
            return;
        }

        this.PART_ControlsGrid.IsEnabled = false;
        uint start = (uint) selection.Start.ByteIndex;
        await info.MemoryEngine360.BeginBusyOperationActivityAsync(async (t, c) => {
            using CancellationTokenSource cts = new CancellationTokenSource();
            await ActivityManager.Instance.RunTask(async () => {
                ActivityTask task = ActivityManager.Instance.CurrentTask;
                task.Progress.Caption = "Write data from Hex Editor";
                task.Progress.Text = "Freezing console...";
                if (c is IHaveIceCubes)
                    await ((IHaveIceCubes) c).DebugFreeze();

                SimpleCompletionState completion = new SimpleCompletionState();
                completion.CompletionValueChanged += state => {
                    task.Progress.CompletionState.TotalCompletion = state.TotalCompletion;
                    task.Progress.Text = $"Writing {ValueScannerUtils.ByteFormatter.ToString(selection.ByteLength * state.TotalCompletion, false)}/{ValueScannerUtils.ByteFormatter.ToString(selection.ByteLength, false)}";
                };

                // Update initial text
                completion.OnCompletionValueChanged();

                byte[] buffer = new byte[count];
                this.myDocument!.ReadBytes(start, buffer);
                await c.WriteBytes(this.actualStartAddress + start, buffer, 0, count, 0x10000, completion, task.CancellationToken);

                task.Progress.Text = "Unfreezing console...";
                if (c is IHaveIceCubes)
                    await ((IHaveIceCubes) c).DebugUnFreeze();
            }, cts);
        }, "Write Hex Editor Data");

        this.PART_ControlsGrid.IsEnabled = true;
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

    static HexEditorWindow() {
        HexDisplayInfoProperty.Changed.AddClassHandler<HexEditorWindow, HexEditorInfo?>((o, e) => o.OnInfoChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    protected override void OnOpenedCore() {
        base.OnOpenedCore();

        UIInputManager.SetFocusPath(this, "HexDisplayWindow");
        UIInputManager.SetFocusPath(this.PART_HexEditor, "HexDisplayWindow/HexEditor");
        using MultiChangeToken change = DataManager.GetContextData(this).BeginChange();
        change.Context.Set(IHexEditorUI.DataKey, this);
    }

    protected override void OnClosed(EventArgs e) {
        base.OnClosed(e);
        this.autoRefreshTask?.RequestCancellation();
        this.HexDisplayInfo = null;
    }

    private void OnInfoChanged(HexEditorInfo? oldData, HexEditorInfo? newData) {
        this.captionBinder.SwitchModel(newData);
        this.addrBinder.SwitchModel(newData);
        this.lenBinder.SwitchModel(newData);
        this.bytesPerRowBinder.SwitchModel(newData);
        this.autoRefreshAddrBinder.SwitchModel(newData);
        this.autoRefreshLenBinder.SwitchModel(newData);
        if (oldData != null) {
            oldData.MemoryEngine360.ConnectionAboutToChange -= this.OnConnectionAboutToChange;
            this.endiannessBinder.Detach();
        }

        if (newData != null) {
            newData.MemoryEngine360.ConnectionAboutToChange += this.OnConnectionAboutToChange;
            this.endiannessBinder.Attach(newData);
            this.PART_CancelButton.Focus();
        }
    }

    private async Task OnConnectionAboutToChange(MemoryEngine360 sender, ulong frame) {
        if (this.autoRefreshTask != null) {
            await this.autoRefreshTask.CancelAsync();
        }
    }

    private void OnCancelButtonClicked(object? sender, RoutedEventArgs e) {
        this.Close();
    }

    private void UpdateAutoRefreshRange() {
        HexEditorInfo? info = this.HexDisplayInfo;
        if (info != null) {
            BitRange range = new BitRange(info.AutoRefreshStartAddress - this.actualStartAddress, (info.AutoRefreshStartAddress + info.AutoRefreshLength) - this.actualStartAddress);
            this.autoRefreshLayer.SetRange(range);
        }
    }

    private void UpdateDataInspector() {
        ulong caretIndex = this.SelectionRange.Start.ByteIndex;
        HexEditorInfo? info = this.HexDisplayInfo;
        if (this.myDocument == null || info == null) {
            return;
        }

        ulong cbRemaining = this.myDocument.Length - caretIndex;

        byte[] daBuf = new byte[8];
        if (cbRemaining > 0)
            this.myDocument.ReadBytes(caretIndex, new Span<byte>(daBuf, 0, (int) Math.Min(8, cbRemaining)));

        // The console is big-endian. If we want to display as little endian, we need to reverse the bytes
        byte val08 = cbRemaining >= 1 ? daBuf[0] : default;
        ushort val16 = cbRemaining >= 2 ? MemoryMarshal.Read<UInt16>(new ReadOnlySpan<byte>(daBuf, 0, 2)) : default;
        uint val32 = cbRemaining >= 4 ? MemoryMarshal.Read<UInt32>(new ReadOnlySpan<byte>(daBuf, 0, 4)) : 0;
        ulong val64 = cbRemaining >= 8 ? MemoryMarshal.Read<UInt64>(new ReadOnlySpan<byte>(daBuf, 0, 8)) : 0;

        // Rather than use something like BinaryPrimitives.ReadUInt32BigEndian, we just
        // reverse the endianness here so that we aren't reversing possibly twice if the user
        // wants to display in LE for some reason
        bool displayAsLE = info.InspectorEndianness == Endianness.LittleEndian;
        if (displayAsLE != BitConverter.IsLittleEndian) {
            val16 = BinaryPrimitives.ReverseEndianness(val16);
            val32 = BinaryPrimitives.ReverseEndianness(val32);
            val64 = BinaryPrimitives.ReverseEndianness(val64);
        }

        bool asHex = this.PART_DisplayIntAsHex.IsChecked == true;
        this.PART_Binary8.Text = val08.ToString("B8");
        if (!this.PART_Int8.IsKeyboardFocusWithin)
            this.PART_Int8.Text = asHex ? ((sbyte) val08).ToString("X2") : ((sbyte) val08).ToString();
        if (!this.PART_UInt8.IsKeyboardFocusWithin)
            this.PART_UInt8.Text = asHex ? val08.ToString("X2") : val08.ToString();
        if (!this.PART_Int16.IsKeyboardFocusWithin)
            this.PART_Int16.Text = asHex ? ((short) val16).ToString("X4") : ((short) val16).ToString();
        if (!this.PART_UInt16.IsKeyboardFocusWithin)
            this.PART_UInt16.Text = asHex ? val16.ToString("X4") : val16.ToString();
        if (!this.PART_Int32.IsKeyboardFocusWithin)
            this.PART_Int32.Text = asHex ? ((int) val32).ToString("X8") : ((int) val32).ToString();
        if (!this.PART_UInt32.IsKeyboardFocusWithin)
            this.PART_UInt32.Text = asHex ? val32.ToString("X8") : val32.ToString();
        if (!this.PART_Int64.IsKeyboardFocusWithin)
            this.PART_Int64.Text = asHex ? ((long) val64).ToString("X16") : ((long) val64).ToString();
        if (!this.PART_UInt64.IsKeyboardFocusWithin)
            this.PART_UInt64.Text = asHex ? val64.ToString("X16") : val64.ToString();
        if (!this.PART_Float.IsKeyboardFocusWithin)
            this.PART_Float.Text = Unsafe.As<uint, float>(ref val32).ToString();
        if (!this.PART_Double.IsKeyboardFocusWithin)
            this.PART_Double.Text = Unsafe.As<ulong, double>(ref val64).ToString();
        this.PART_CharUTF8.Text = ((char) val08).ToString();
        this.PART_CharUTF16.Text = ((char) val16).ToString();
        this.PART_CharUTF32.Text = Encoding.UTF32.GetString(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref val32), 4));
        this.PART_BtnGoToPointerInt32.IsEnabled = this.IsPointerInRange(val32);
    }

    private void UpdateAutoRefreshButtonsAndTextBoxes() {
        bool isRunning = this.autoRefreshTask != null;

        this.PART_ToggleAutoRefreshButton.Content = isRunning ? "Stop Auto Refresh" : "Start Auto Refresh";
        this.PART_ToggleAutoRefreshButton.IsChecked = isRunning;
        this.PART_AutoRefresh_From.IsEnabled = !isRunning;
        this.PART_AutoRefresh_Count.IsEnabled = !isRunning;
        this.runAutoRefreshCommand.RaiseCanExecuteChanged();
        this.UpdateAutoRefreshSelectionDependentShit();
    }

    private void UpdateAutoRefreshSelectionDependentShit() {
        this.PART_SetAutoRefreshRangeAsSelection.IsEnabled = this.autoRefreshTask == null && this.SelectionRange.ByteLength > 0;
        this.PART_ClearAutoRefreshRange.IsEnabled = this.autoRefreshTask == null;
    }

    private sealed class AutoRefreshTask : AdvancedPausableTask {
        private readonly HexEditorWindow control;
        private readonly HexEditorInfo? info;
        private IDisposable? busyToken;
        private readonly uint startAddress, startAddressInDoc, cbRange;
        private readonly MemoryBinaryDocument? myDocument;
        private readonly byte[] myBuffer;
        private bool isInvalidOnFirstRun;

        public AutoRefreshTask(HexEditorWindow control, uint startAddressInDoc, uint cbRange) : base(true) {
            this.control = control;
            this.info = control.HexDisplayInfo;
            this.startAddressInDoc = startAddressInDoc;
            this.cbRange = cbRange;
            this.startAddress = startAddressInDoc + control.actualStartAddress;
            this.myDocument = this.control.myDocument;
            this.myBuffer = new byte[this.cbRange];
        }

        protected override async Task RunFirst(CancellationToken pauseOrCancelToken) {
            if (this.info == null || this.myDocument == null) {
                this.isInvalidOnFirstRun = true;
                return;
            }

            ActivityTask task = this.Activity;
            task.Progress.Caption = "Auto refresh";
            task.Progress.Text = "Updating UI...";
            await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                this.control.PART_ControlsGrid.IsEnabled = false;

                this.control.autoRefreshLayer.SetRange(new BitRange(this.startAddressInDoc, this.startAddressInDoc + this.cbRange));
                this.control.autoRefreshLayer.IsActive = true;
                this.control.UpdateAutoRefreshButtonsAndTextBoxes();

                this.control.readAllCommand.RaiseCanExecuteChanged();
                this.control.refreshDataCommand.RaiseCanExecuteChanged();
                this.control.uploadDataCommand.RaiseCanExecuteChanged();
            });

            task.Progress.Text = "Waiting for busy operations...";
            this.busyToken = await this.info.MemoryEngine360.BeginBusyOperationAsync(this.CancellationToken);
            if (this.busyToken == null) {
                return;
            }

            task.Progress.Text = "Auto refresh in progress";
            task.Progress.IsIndeterminate = true;
            await this.RunUpdateLoop(pauseOrCancelToken);
        }

        protected override async Task Continue(CancellationToken pauseOrCancelToken) {
            ActivityTask task = this.Activity;
            task.Progress.Text = "Waiting for busy operations...";
            this.busyToken = await this.info!.MemoryEngine360.BeginBusyOperationAsync(this.CancellationToken);
            if (this.busyToken == null) {
                return;
            }

            task.Progress.Text = "Auto refresh in progress";
            task.Progress.IsIndeterminate = true;
            await this.RunUpdateLoop(pauseOrCancelToken);
        }

        protected override async Task OnPaused(bool isFirst) {
            ActivityTask task = this.Activity;
            task.Progress.Text = "Auto refresh paused";
            task.Progress.IsIndeterminate = false;
            task.Progress.CompletionState.TotalCompletion = 0.0;
            this.busyToken?.Dispose();
            this.busyToken = null;
        }

        protected override async Task OnCompleted() {
            if (this.isInvalidOnFirstRun) {
                return;
            }

            this.busyToken?.Dispose();
            this.busyToken = null;

            await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                this.control.PART_ControlsGrid.IsEnabled = true;

                this.control.autoRefreshLayer.IsActive = false;
                this.control.autoRefreshTask = null;
                this.control.UpdateAutoRefreshButtonsAndTextBoxes();

                this.control.readAllCommand.RaiseCanExecuteChanged();
                this.control.refreshDataCommand.RaiseCanExecuteChanged();
                this.control.uploadDataCommand.RaiseCanExecuteChanged();
            });
        }

        private async Task RunUpdateLoop(CancellationToken pauseOrCancelToken) {
            BasicApplicationConfiguration settings = BasicApplicationConfiguration.Instance;
            while (true) {
                pauseOrCancelToken.ThrowIfCancellationRequested();
                IConsoleConnection? connection = this.info!.MemoryEngine360.Connection;
                if (this.info.MemoryEngine360.IsShuttingDown || connection?.IsConnected != true) {
                    return;
                }

                if (this.cbRange < 1 || this.control.myDocument != this.myDocument) {
                    return;
                }

                TimeSpan interval = TimeSpan.FromSeconds(1.0 / settings.AutoRefreshUpdatesPerSecond);
                DateTime startTime = DateTime.Now;
                try {
                    // aprox. 50ms to fully read 1.5k bytes, based on simple benchmark with DateTime.Now
                    await connection.ReadBytes(this.startAddress, this.myBuffer, 0, (int) Math.Min(this.cbRange, int.MaxValue), 0x10000, null, pauseOrCancelToken);

                    await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                        if (this.control.PART_ToggleShowChanges.IsChecked == true)
                            this.control.changeManager.ProcessChanges(this.startAddressInDoc, this.myBuffer, this.myBuffer.Length);

                        if (!this.control.myDocument!.IsReadOnly) {
                            this.control.myDocument!.WriteBytes(this.startAddressInDoc, this.myBuffer);
                        }

                        this.control.UpdateSelectionText();
                        this.control.UpdateCaretText();
                        return Task.CompletedTask;
                    }, token: CancellationToken.None);
                }
                catch (OperationCanceledException) {
                    throw;
                }
                catch (Exception ex) when (ex is IOException || ex is TimeoutException) {
                    return;
                }
                catch {
                    Debugger.Break();
                    return;
                }

                TimeSpan timeTaken = DateTime.Now - startTime;

                int sleepMillis = (int) (interval - timeTaken - TimeSpan.FromMilliseconds(5)).TotalMilliseconds;
                if (sleepMillis > 0) {
                    await Task.Delay(sleepMillis, pauseOrCancelToken);
                }
                else {
                    await Task.Yield();
                }

                this.Activity.Progress.Text = $"Auto refresh in progress ({Math.Round(1.0 / (DateTime.Now - startTime).TotalSeconds, 1)} upd/s)";
            }
        }
    }
}