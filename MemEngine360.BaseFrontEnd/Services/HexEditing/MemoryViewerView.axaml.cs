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
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaHex.Async.Editing;
using AvaloniaHex.Async.Rendering;
using AvaloniaHex.Base.Document;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using MemEngine360.Engine;
using MemEngine360.Engine.HexEditing;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.Scanners;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Activities.Pausable;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Avalonia.Shortcuts.Avalonia;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Commands;
using AsciiColumn = AvaloniaHex.Async.Rendering.AsciiColumn;
using HexColumn = AvaloniaHex.Async.Rendering.HexColumn;
using OffsetColumn = AvaloniaHex.Async.Rendering.OffsetColumn;
using TextLayer = AvaloniaHex.Async.Rendering.TextLayer;

namespace MemEngine360.BaseFrontEnd.Services.HexEditing;

public partial class MemoryViewerView : UserControl, IHexEditorUI {
    public static readonly StyledProperty<MemoryViewer?> HexDisplayInfoProperty = AvaloniaProperty.Register<MemoryViewerView, MemoryViewer?>("HexDisplayInfo");

    #region Binders

    private readonly TextBoxToEventPropertyBinder<MemoryViewer> offsetBinder = new TextBoxToEventPropertyBinder<MemoryViewer>(nameof(MemoryViewer.OffsetChanged), (b) => b.Model.Offset.ToString("X8"), async (b, x) => {
        if (!AddressParsing.TryParse32(x, out uint value, out string? error, canParseAsExpression: true)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", error, defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
            return false;
        }

        b.Model.Offset = value;
        return true;
    });

    private readonly IBinder<MemoryViewer> bytesPerRowBinder = new TextBoxToEventPropertyBinder<MemoryViewer>(nameof(MemoryViewer.BytesPerRowChanged), (b) => b.Model.BytesPerRow.ToString(), async (b, x) => {
        // will probably cause the computer so implode or something if there's too many or little bpr
        if (!uint.TryParse(x, out uint value)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Invalid integer value", defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
        }
        else if (value < MemoryViewer.MinimumBytesPerRow || value > MemoryViewer.MaximumBytesPerRow) {
            await IMessageDialogService.Instance.ShowMessage("Out of range", $"Bytes Per Row must be between {MemoryViewer.MinimumBytesPerRow} and {MemoryViewer.MaximumBytesPerRow}", defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
        }
        else {
            b.Model.BytesPerRow = value;
            return true;
        }

        return false;
    });

    private readonly IBinder<MemoryViewer> autoRefreshAddrBinder = new TextBoxToEventPropertyBinder<MemoryViewer>(nameof(MemoryViewer.AutoRefreshStartAddressChanged), (p) => p.Model.AutoRefreshStartAddress.ToString("X8"), async (b, x) => {
        if (!AddressParsing.TryParse32(x, out uint value, out string? error, canParseAsExpression: true)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", error, defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
            return false;
        }

        if ((ulong) value + b.Model.AutoRefreshLength > uint.MaxValue) {
            await IMessageDialogService.Instance.ShowMessage("Bytes count", $"Address causes scan to exceed applicable memory range", icon: MessageBoxIcons.ErrorIcon);
            return false;
        }

        b.Model.AutoRefreshStartAddress = value;
        return true;
    });

    private readonly IBinder<MemoryViewer> autoRefreshLenBinder = new TextBoxToEventPropertyBinder<MemoryViewer>(nameof(MemoryViewer.AutoRefreshLengthChanged), (p) => p.Model.AutoRefreshLength.ToString("X8"), async (b, x) => {
        if (!AddressParsing.TryParse32(x, out uint value, out string? error, canParseAsExpression: true)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", error, defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
            return false;
        }

        if ((ulong) b.Model.AutoRefreshStartAddress + value > uint.MaxValue) {
            await IMessageDialogService.Instance.ShowMessage("Bytes count", $"Byte count causes scan to exceed applicable memory range", icon: MessageBoxIcons.ErrorIcon);
            return false;
        }

        b.Model.AutoRefreshLength = value;
        return true;
    });

    #endregion

    private readonly AsyncRelayCommand refreshDataCommand, uploadDataCommand;

    private AutoRefreshTask? autoRefreshTask;
    private bool flagRestartAutoRefresh;

    private ConsoleHexBinarySource? myBinarySource;

    private readonly AutoRefreshLayer autoRefreshLayer;
    private readonly HexEditorChangeManager changeManager;
    private readonly AsyncRelayCommand runAutoRefreshCommand;

    public MemoryViewer? HexDisplayInfo {
        get => this.GetValue(HexDisplayInfoProperty);
        set => this.SetValue(HexDisplayInfoProperty, value);
    }

    public BitLocation CaretLocation {
        get => this.PART_HexEditor.Caret.Location;
        set {
            this.PART_HexEditor.ResetSelection();
            this.PART_HexEditor.Caret.Location = value;
        }
    }

    public BitRange SelectionRange {
        get => this.PART_HexEditor.Selection.Range;
        set {
            this.PART_HexEditor.ResetSelection();
            this.PART_HexEditor.Selection.Range = value;
        }
    }

    /// <summary>
    /// Gets the window that this memory viewer is open in
    /// </summary>
    public IDesktopWindow? Window { get; private set; }

    public MemoryViewerView() {
        this.InitializeComponent();
        this.offsetBinder.AttachControl(this.PART_AddressTextBox);
        this.offsetBinder.ValueConfirmed += (b, oldText) => {
            this.PART_HexEditor.HexView.ScrollToByteOffset(b.Model.Offset, out _);
            this.PART_HexEditor.Caret.Location = new BitLocation(b.Model.Offset);
            this.PART_HexEditor.Selection.Range = new BitRange(b.Model.Offset, b.Model.Offset + 1);
        };

        this.bytesPerRowBinder.AttachControl(this.PART_BytesPerRowTextBox);
        this.bytesPerRowBinder.ControlUpdated += b => {
            this.PART_HexEditor.HexView.BytesPerLine = (int) this.HexDisplayInfo!.BytesPerRow;
        };

        // Lazy way to hook into the model's value change
        this.autoRefreshAddrBinder.ControlUpdated += (b) => this.UpdateAutoRefreshRange();
        this.autoRefreshLenBinder.ControlUpdated += (b) => this.UpdateAutoRefreshRange();

        this.autoRefreshAddrBinder.AttachControl(this.PART_AutoRefresh_From);
        this.autoRefreshLenBinder.AttachControl(this.PART_AutoRefresh_Count);

        AsyncHexView view = this.PART_HexEditor.HexView;
        view.BytesPerLine = 32;
        view.Columns.Add(new OffsetColumn());
        view.Columns.Add(new HexColumn());
        view.Columns.Add(new AsciiColumn());
        view.Layers.InsertBefore<TextLayer>(this.autoRefreshLayer = new AutoRefreshLayer(this.PART_HexEditor.Caret));
        this.changeManager = new HexEditorChangeManager(this.PART_HexEditor);

        this.PART_CancelButton.Command = new AsyncRelayCommand(() => this.Window!.RequestCloseAsync());

        this.refreshDataCommand = new AsyncRelayCommand(async () => {
            await this.ReloadSelectionFromConsole();
        }, () => {
            if (this.autoRefreshTask != null) {
                return false;
            }

            BitRange selection = this.SelectionRange;
            return selection.ByteLength > 0 && this.myBinarySource != null;
        });

        this.uploadDataCommand = new AsyncRelayCommand(async () => {
            await this.UploadSelectionToConsoleCommand();
        }, () => {
            if (this.autoRefreshTask != null) {
                return false;
            }

            BitRange selection = this.SelectionRange;
            return selection.ByteLength > 0 && this.myBinarySource != null;
        });

        this.PART_Refresh.Command = this.refreshDataCommand;
        this.PART_Upload.Command = this.uploadDataCommand;
        this.PART_HexEditor.Caret.LocationChanged += (sender, args) => this.UpdateCaretText();
        this.PART_HexEditor.Caret.PrimaryColumnChanged += (sender, args) => this.UpdateCaretText();
        this.PART_HexEditor.Selection.RangeChanged += (sender, args) => this.UpdateSelectionText();

        this.PART_ToggleAutoRefreshButton.Command = this.runAutoRefreshCommand = new AsyncRelayCommand(async () => {
            if (this.autoRefreshTask != null) {
                // We are running, so stop it
                await this.autoRefreshTask.CancelAsync();
            }
            else {
                MemoryViewer? info = this.HexDisplayInfo;
                if (info == null || this.myBinarySource == null) {
                    this.UpdateAutoRefreshButtonsAndTextBoxes();
                    return;
                }

                uint arStartAddress = info.AutoRefreshStartAddress;
                uint arCountBytes = info.AutoRefreshLength;
                if (arCountBytes == 0) {
                    BitRange selection = this.SelectionRange;
                    if (selection.ByteLength > 0) {
                        MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Auto refresh", "Auto refresh span is empty. Set span as selection and run?", MessageBoxButtons.OKCancel, MessageBoxResult.OK);
                        if (result != MessageBoxResult.OK) {
                            this.UpdateAutoRefreshButtonsAndTextBoxes();
                            return;
                        }

                        info.AutoRefreshStartAddress = arStartAddress = (uint) selection.Start.ByteIndex;
                        info.AutoRefreshLength = arCountBytes = (uint) selection.ByteLength;
                    }
                    else {
                        this.UpdateAutoRefreshButtonsAndTextBoxes();
                        await IMessageDialogService.Instance.ShowMessage("Auto refresh", "Auto refresh span is empty", defaultButton: MessageBoxResult.OK);
                        return;
                    }
                }

                if (arCountBytes > 1000000) {
                    this.UpdateAutoRefreshButtonsAndTextBoxes();
                    await IMessageDialogService.Instance.ShowMessage("Range too large", "Auto-refresh range must be less than 1MB. Ideally it shouldn't be any more than 8KB");
                    return;
                }

                if ((ulong) arStartAddress + arCountBytes > uint.MaxValue) {
                    this.UpdateAutoRefreshButtonsAndTextBoxes();
                    await IMessageDialogService.Instance.ShowMessage("Start address", "Auto refresh span is outside the applicable memory range");
                    return;
                }

                this.autoRefreshTask = new AutoRefreshTask(this, arStartAddress, arCountBytes);
                this.autoRefreshTask.Run();
            }
        });

        this.UpdateAutoRefreshButtonsAndTextBoxes();

        this.PART_Inspector.ReadDataProcedure = array => {
            if (this.myBinarySource != null)
                return this.myBinarySource.ReadAvailableData(this.SelectionRange.Start.ByteIndex, array);
            return 0;
        };

        this.PART_Inspector.GoToAddressProcedure = (address, length) => {
            BitLocation caret = new BitLocation(address);
            this.CaretLocation = caret;
            this.SelectionRange = new BitRange(caret, new BitLocation(Maths.SumAndClampOverflow(address, length)));
        };

        this.PART_Inspector.MoveCaretProcedure = incr => {
            BitLocation caret = new BitLocation(Maths.SumAndClampOverflow(this.CaretLocation.ByteIndex, incr));
            this.CaretLocation = caret;
            this.SelectionRange = new BitRange(caret, new BitLocation(Maths.SumAndClampOverflow(caret.ByteIndex, (ulong) Math.Abs(incr))));
        };

        this.PART_Inspector.UploadTextBoxText = this.ParseTextBoxAndUpload;
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);
        this.PART_HexEditor.HexView.ScrollToByteOffset(0x82600000, out _);
    }

    private async Task ParseTextBoxAndUpload(UploadTextBoxInfo info) {
        Debug.Assert(info.DataType.IsNumeric(), "Cannot upload non-numeric data as of yet");

        MemoryEngine engine = this.HexDisplayInfo!.MemoryEngine;
        if (engine.Connection == null) {
            await IMessageDialogService.Instance.ShowMessage("No connection", "Not connected to any console", defaultButton: MessageBoxResult.OK);
            return;
        }

        NumericDisplayType intNdt = info.DataType.IsInteger() && this.PART_Inspector.DisplayIntegersAsHex ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;

        string input = info.TextBox.Text ?? "";
        // Custom case for signed byte. Why TF did I add a signed byte row to the data inspector???
        if (info.DataType == DataType.Byte && !info.IsUnsigned) {
            if (!sbyte.TryParse(input, intNdt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer, null, out sbyte sb)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid text", "Invalid signed byte", defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
                return;
            }

            // cheese it
            input = ((byte) sb).ToString();
        }

        ValidationArgs args = new ValidationArgs(input, new List<string>(), false);
        if (!DataValueUtils.TryParseTextAsDataValue(args, info.DataType, intNdt, StringType.ASCII, out IDataValue? value)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid text", args.Errors.Count > 0 ? args.Errors[0] : "Could not parse value as " + info.DataType, defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
            return;
        }

        ulong caretIndex = this.SelectionRange.Start.ByteIndex;
        await this.PerformOperationBetweenAutoRefresh(async () => {
            IConsoleConnection connection;
            using IBusyToken? token = await engine.BeginBusyOperationUsingActivityAsync("Upload DI value");
            if (token != null && (connection = engine.Connection) != null && !connection.IsClosed) {
                await MemoryEngine.WriteDataValue(connection, (uint) caretIndex, value);
                if (this.PART_ToggleShowChanges.IsChecked == true && this.myBinarySource != null) {
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
                        byte[] buffer = await connection.ReadBytes((uint) caretIndex, dataLength);

                        if (this.PART_ToggleShowChanges.IsChecked == true) {
                            this.changeManager.ProcessChanges((uint) caretIndex, buffer, buffer.Length);
                        }

                        this.myBinarySource!.WriteBytesToCache((uint) caretIndex, buffer);
                    }
                }
            }
        });

        this.PART_Inspector.UpdateFields();
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

    public void SetBinarySource(IConnectionLockPair? lockPair) {
        if (this.myBinarySource != null)
            this.myBinarySource.ValidRanges.IndicesChanged -= this.OnValidRangesChanged;

        this.PART_HexEditor.BinarySource = this.myBinarySource = lockPair != null ? new ConsoleHexBinarySource(lockPair) : null;

        if (this.myBinarySource != null)
            this.myBinarySource.ValidRanges.IndicesChanged += this.OnValidRangesChanged;

        this.changeManager.Clear();
        this.changeManager.OnBinarySourceChanged(this.myBinarySource);
        this.UpdateSelectionText();
        this.UpdateCaretText();
        this.PART_Inspector.UpdateFields();
    }

    private void OnValidRangesChanged(IObservableULongRangeUnion sender, IList<ULongRange> added, IList<ULongRange> removed) {
        ApplicationPFX.Instance.Dispatcher.Post(() => {
            ULongRange range = ULongRange.FromStartAndLength(this.SelectionRange.Start.ByteIndex, 8);
            // ignore removed ranges to maintain previous data in the inspector
            if (added.Any(x => x.Overlaps(range))) {
                this.PART_Inspector.UpdateFields();
            }
        });
    }

    public Task ReloadSelectionFromConsole() {
        BitRange selection = this.SelectionRange;
        int count = (int) Math.Min(selection.ByteLength, int.MaxValue);
        uint start = (uint) Math.Min(selection.Start.ByteIndex, uint.MaxValue);
        return this.ReloadSelectionFromConsole(start, count);
    }

    public async Task ReloadSelectionFromConsole(uint address, int length) {
        MemoryViewer? info = this.HexDisplayInfo;
        if (info == null || this.autoRefreshTask != null || length < 1 || this.myBinarySource == null) {
            return;
        }

        if (length > 0x10000) {
            await IMessageDialogService.Instance.ShowMessage("Selection too large", "Cannot reload " + Math.Round(length / 1000000.0, 2) + " MB. Maximum is 64KB");
            return;
        }

        this.PART_ControlsGrid.IsEnabled = false;

        using CancellationTokenSource cts = new CancellationTokenSource();
        Result<Optional<byte[]>> result = await ActivityManager.Instance.RunTask(() => {
            ActivityTask.Current.Progress.Caption = "Refresh data for Hex Editor";
            return info.MemoryEngine.BeginBusyOperationFromActivityAsync(async (_, connection) => {
                ActivityTask task = ActivityTask.Current;

                IFeatureIceCubes? iceCubes = connection.GetFeatureOrDefault<IFeatureIceCubes>();

                bool isAlreadyFrozen = false;
                if (iceCubes != null && info.MemoryEngine.ScanningProcessor.PauseConsoleDuringScan) {
                    task.Progress.Text = "Freezing console...";
                    isAlreadyFrozen = await iceCubes.DebugFreeze() == FreezeResult.AlreadyFrozen;
                }

                SimpleCompletionState completion = new SimpleCompletionState();
                completion.CompletionValueChanged += state => {
                    task.Progress.CompletionState.TotalCompletion = state.TotalCompletion;
                    task.Progress.Text = $"Reading {ValueScannerUtils.ByteFormatter.ToString(length * state.TotalCompletion, false)}/{ValueScannerUtils.ByteFormatter.ToString(length, false)}";
                };

                // Update initial text
                completion.OnCompletionValueChanged();
                byte[] buffer = new byte[length];
                await connection.ReadBytes(address, buffer, 0, length, connection.GetRecommendedReadChunkSize(length), completion, task.CancellationToken);

                if (!isAlreadyFrozen && iceCubes != null && info.MemoryEngine.ScanningProcessor.PauseConsoleDuringScan) {
                    task.Progress.Text = "Unfreezing console...";
                    await iceCubes.DebugUnFreeze();
                }

                return buffer;
            }, CancellationToken.None);
        }, cts);

        if (result.Exception != null) {
            if (result.Exception is TimeoutException || result.Exception is IOException) {
                await IMessageDialogService.Instance.ShowMessage(result.Exception is IOException ? "Connection IO Error" : "Connection Timed Out", "Error uploading selection to console", result.Exception.Message);
            }
            else {
                await LogExceptionHelper.ShowMessageAndPrintToLogs("Connection Error", "Error uploading selection to console", result.Exception);
            }

            return;
        }

        this.PART_ControlsGrid.IsEnabled = true;
        if (result.GetValueOrDefault().GetValueOrDefault() is byte[] readBuffer) {
            if (this.PART_ToggleShowChanges.IsChecked == true) {
                this.changeManager.ProcessChanges(address, readBuffer);
            }

            this.myBinarySource!.WriteBytesToCache(address, readBuffer);
        }

        this.UpdateSelectionText();
        this.UpdateCaretText();
    }

    public async Task UploadSelectionToConsoleCommand() {
        MemoryViewer? info = this.HexDisplayInfo;
        if (this.myBinarySource == null || info == null || this.autoRefreshTask != null) {
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

        using CancellationTokenSource cts = new CancellationTokenSource();
        ActivityTask activity = ActivityManager.Instance.RunTask(async () => {
            IActivityProgress progress = ActivityManager.Instance.CurrentTask.Progress;
            progress.SetCaptionAndText("Write data from Hex Editor");
            await info.MemoryEngine.BeginBusyOperationFromActivityAsync(async (_, c) => {
                bool isAlreadyFrozen = false;
                IFeatureIceCubes? iceCubes = c.GetFeatureOrDefault<IFeatureIceCubes>();
                if (iceCubes != null && info.MemoryEngine.ScanningProcessor.PauseConsoleDuringScan) {
                    progress.Text = "Freezing console...";
                    isAlreadyFrozen = await iceCubes.DebugFreeze() == FreezeResult.AlreadyFrozen;
                }

                SimpleCompletionState completion = new SimpleCompletionState();
                completion.CompletionValueChanged += state => {
                    progress.CompletionState.TotalCompletion = state.TotalCompletion;
                    progress.Text = $"Writing {ValueScannerUtils.ByteFormatter.ToString(selection.ByteLength * state.TotalCompletion, false)}/{ValueScannerUtils.ByteFormatter.ToString(selection.ByteLength, false)}";
                };

                // Update initial text
                completion.OnCompletionValueChanged();

                byte[] buffer = new byte[count];
                int read = this.myBinarySource!.ReadAvailableData(start, buffer);
                await c.WriteBytes(start, buffer, 0, read, c.GetRecommendedReadChunkSize(read), completion, ActivityManager.Instance.CurrentTask.CancellationToken);

                if (!isAlreadyFrozen && iceCubes != null && info.MemoryEngine.ScanningProcessor.PauseConsoleDuringScan) {
                    progress.Text = "Unfreezing console...";
                    await iceCubes.DebugUnFreeze();
                }
            }, CancellationToken.None);
        });

        await activity;
        if (activity.Exception != null) {
            if (activity.Exception is TimeoutException || activity.Exception is IOException) {
                await IMessageDialogService.Instance.ShowMessage(activity.Exception is IOException ? "Connection IO Error" : "Connection Timed Out", "Error uploading selection to console", activity.Exception.Message);
            }
            else {
                await LogExceptionHelper.ShowMessageAndPrintToLogs("Connection Error", "Error uploading selection to console", activity.Exception);
            }
        }

        this.PART_ControlsGrid.IsEnabled = true;
    }

    public void ScrollToCaret() {
        BitLocation caret = this.PART_HexEditor.Caret.Location;
        this.PART_HexEditor.HexView.ScrollToByteOffset(caret.ByteIndex, out _);
    }

    private void UpdateSelectionText() {
        this.uploadDataCommand.RaiseCanExecuteChanged();
        this.refreshDataCommand.RaiseCanExecuteChanged();

        Selection sel = this.PART_HexEditor.Selection;
        if (sel.Range.IsEmpty) {
            this.PART_SelectionText.Text = "<none>";
        }
        else {
            ulong end = sel.Range.End.ByteIndex;
            this.PART_SelectionText.Text = $"{sel.Range.ByteLength} bytes ({sel.Range.Start.ByteIndex:X8} -> {(end > 0 ? (end - 1) : 0):X8})";
        }

        this.PART_Inspector.UpdateFields();
        this.UpdateAutoRefreshSelectionDependentShit();
    }

    private void UpdateCaretText() {
        Caret caret = this.PART_HexEditor.Caret;
        BitLocation pos = caret.Location;
        this.PART_CaretText.Text = $"{pos.ByteIndex:X8} ({pos.ByteIndex:X} from start)";
        this.PART_Inspector.UpdateFields();
        this.UpdateAutoRefreshSelectionDependentShit();
    }

    static MemoryViewerView() {
        HexDisplayInfoProperty.Changed.AddClassHandler<MemoryViewerView, MemoryViewer?>((s, e) => s.OnHexDisplayInfoChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnHexDisplayInfoChanged(MemoryViewer? oldData, MemoryViewer? newData) {
        this.offsetBinder.SwitchModel(newData);
        this.bytesPerRowBinder.SwitchModel(newData);
        this.autoRefreshAddrBinder.SwitchModel(newData);
        this.autoRefreshLenBinder.SwitchModel(newData);
        if (oldData != null) {
            oldData.RestartAutoRefresh -= this.OnRestartAutoRefresh;
            oldData.MemoryEngine.ConnectionAboutToChange -= this.OnConnectionAboutToChange;
            oldData.MemoryEngine.ConnectionChanged -= this.OnConnectionChanged;
            oldData.InspectorEndiannessChanged -= this.OnEndiannessModeChanged;
            this.SetBinarySource(null);
            oldData.BinarySource = null;
        }

        if (newData != null) {
            newData.RestartAutoRefresh += this.OnRestartAutoRefresh;
            newData.MemoryEngine.ConnectionAboutToChange += this.OnConnectionAboutToChange;
            newData.MemoryEngine.ConnectionChanged += this.OnConnectionChanged;
            newData.InspectorEndiannessChanged += this.OnEndiannessModeChanged;
            this.PART_CancelButton.Focus();
            this.SetBinarySource(new ConnectionLockPair(newData.MemoryEngine.BusyLock, newData.MemoryEngine.Connection));
            newData.BinarySource = this.myBinarySource;

            this.PART_Inspector.IsLittleEndian = newData.InspectorEndianness == Endianness.LittleEndian;
        }
    }

    private void OnConnectionChanged(MemoryEngine sender, ulong frame, IConsoleConnection? oldconnection, IConsoleConnection? newconnection, ConnectionChangeCause cause) {
        this.SetBinarySource(this.HexDisplayInfo != null ? new ConnectionLockPair(sender.BusyLock, newconnection) : null);
    }

    private void OnEndiannessModeChanged(MemoryViewer sender) {
        this.PART_Inspector.IsLittleEndian = sender.InspectorEndianness == Endianness.LittleEndian;
    }

    internal void OnWindowOpened(IDesktopWindow sender) {
        this.Window = sender;
        UIInputManager.SetFocusPath(this.PART_HexEditor, "HexDisplayWindow/HexEditor");
        DataManager.GetContextData(this).Set(IHexEditorUI.DataKey, this);
    }

    internal void OnWindowClosed() {
        this.autoRefreshTask?.RequestCancellation();
        this.HexDisplayInfo = null;
        this.Window = null;
    }

    private void OnRestartAutoRefresh(MemoryViewer memoryViewer) {
        if (this.autoRefreshTask != null && !this.autoRefreshTask.IsCompleted && this.autoRefreshTask.RequestCancellation()) {
            this.flagRestartAutoRefresh = true;
        }
    }

    private async Task OnConnectionAboutToChange(MemoryEngine sender, ulong frame, IActivityProgress progress) {
        progress.Caption = progress.Text = "Stopping auto-refresh";
        if (this.autoRefreshTask != null) {
            await this.autoRefreshTask.CancelAsync();
        }
    }

    private void UpdateAutoRefreshRange() {
        MemoryViewer? info = this.HexDisplayInfo;
        if (info != null) {
            this.autoRefreshLayer.SetRange(new BitRange(info.AutoRefreshStartAddress, info.AutoRefreshStartAddress + info.AutoRefreshLength));
        }
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
        private readonly MemoryViewerView control;
        private readonly MemoryViewer? info;
        private IBusyToken? myBusyToken;
        private readonly uint startAddress, cbRange;
        private readonly ConsoleHexBinarySource? myDocument;
        private readonly byte[] myBuffer;
        private bool isInvalidOnFirstRun;

        public AutoRefreshTask(MemoryViewerView control, uint startAddress, uint cbRange) : base(true) {
            this.control = control;
            this.info = control.HexDisplayInfo;
            this.cbRange = cbRange;
            this.startAddress = startAddress;
            this.myDocument = this.control.myBinarySource;
            this.myBuffer = new byte[this.cbRange];
        }

        protected override async Task RunOperation(CancellationToken pauseOrCancelToken, bool isFirstRun) {
            this.Activity.Progress.Caption = "Auto refresh";
            this.Activity.Progress.IsIndeterminate = true;
            if (isFirstRun) {
                if (this.info == null || this.myDocument == null) {
                    this.isInvalidOnFirstRun = true;
                    return;
                }

                this.Activity.Progress.Text = "Updating UI...";
                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    this.control.PART_ControlsGrid.IsEnabled = false;

                    this.control.autoRefreshLayer.SetRange(new BitRange(this.startAddress, this.startAddress + this.cbRange));
                    this.control.autoRefreshLayer.IsActive = true;
                    this.control.UpdateAutoRefreshButtonsAndTextBoxes();

                    this.control.refreshDataCommand.RaiseCanExecuteChanged();
                    this.control.uploadDataCommand.RaiseCanExecuteChanged();
                }, token: CancellationToken.None);
            }
            else {
                Debug.Assert(this.info != null);
            }

            if (!await this.TryObtainBusyToken()) {
                return;
            }

            this.Activity.Progress.Text = "Auto refresh in progress";

            BasicApplicationConfiguration settings = BasicApplicationConfiguration.Instance;
            while (true) {
                pauseOrCancelToken.ThrowIfCancellationRequested();
                IConsoleConnection? connection = this.info!.MemoryEngine.Connection;
                if (this.info.MemoryEngine.IsShuttingDown || connection == null || connection.IsClosed) {
                    return;
                }

                if (this.cbRange < 1 || this.control.myBinarySource != this.myDocument || this.myDocument == null) {
                    return;
                }

                TimeSpan interval = TimeSpan.FromSeconds(1.0 / settings.AutoRefreshUpdatesPerSecond);
                DateTime startTime = DateTime.Now;
                try {
                    // aprox. 50ms to fully read 1.5k bytes, based on simple benchmark with DateTime.Now
                    int read = (int) Math.Min(this.cbRange, int.MaxValue);
                    
                    await connection.ReadBytes(this.startAddress, this.myBuffer, 0, read, connection.GetRecommendedReadChunkSize(read), null, pauseOrCancelToken);

                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                        if (this.control.PART_ToggleShowChanges.IsChecked == true) {
                            this.control.changeManager.ProcessChanges(this.startAddress, this.myBuffer, this.myBuffer.Length);
                        }

                        this.control.myBinarySource?.WriteBytesToCache(this.startAddress, this.myBuffer);

                        this.control.UpdateSelectionText();
                        this.control.UpdateCaretText();
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

        private async Task<bool> TryObtainBusyToken() {
            Debug.Assert(this.myBusyToken == null);

            BusyLock busyLock = this.info!.MemoryEngine.BusyLock;
            this.myBusyToken = await busyLock.BeginBusyOperationFromActivity(this.CancellationToken);
            if (this.myBusyToken == null) {
                return false;
            }

            busyLock.UserQuickReleaseRequested += this.BusyLockOnUserQuickReleaseRequested;
            return true;
        }

        private void ReleaseBusyToken() {
            BusyLock busyLock = this.info!.MemoryEngine.BusyLock;
            Debug.Assert(this.myBusyToken != null && busyLock.IsTokenValid(this.myBusyToken));

            busyLock.UserQuickReleaseRequested -= this.BusyLockOnUserQuickReleaseRequested;
            this.myBusyToken?.Dispose();
            this.myBusyToken = null;
        }

        private void BusyLockOnUserQuickReleaseRequested(BusyLock busyLock, TaskCompletionSource tcsQuickActionFinished) {
            this.RequestPause(out _, out _);

            tcsQuickActionFinished.Task.ContinueWith(t => {
                this.RequestResume(out _, out _);
            }, this.CancellationToken);
        }

        protected override async Task OnPaused(bool isFirst) {
            ActivityTask task = this.Activity;
            task.Progress.Text = "Auto refresh paused";
            task.Progress.IsIndeterminate = false;
            task.Progress.CompletionState.TotalCompletion = 0.0;

            if (this.myBusyToken != null) {
                this.ReleaseBusyToken();
            }
        }

        protected override async Task OnCompleted() {
            if (this.isInvalidOnFirstRun) {
                return;
            }

            if (this.myBusyToken != null) {
                this.ReleaseBusyToken();
            }

            await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                this.control.PART_ControlsGrid.IsEnabled = true;

                this.control.autoRefreshLayer.IsActive = false;
                this.control.autoRefreshTask = null;
                this.control.UpdateAutoRefreshButtonsAndTextBoxes();

                this.control.refreshDataCommand.RaiseCanExecuteChanged();
                this.control.uploadDataCommand.RaiseCanExecuteChanged();

                if (this.control.flagRestartAutoRefresh) {
                    this.control.flagRestartAutoRefresh = false;
                    this.control.runAutoRefreshCommand.Execute(null);
                }
            });
        }
    }
}