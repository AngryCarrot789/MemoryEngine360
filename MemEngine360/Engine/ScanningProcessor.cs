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

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.Engine.Scanners;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;
using PFXToolKitUI.Utils.Ranges;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.Engine;

public class ScanningProcessor {
    private string inputA, inputB;
    private bool hasDoneFirstScan;
    private DataType dataType;
    private NumericScanType numericScanType;
    private bool stringIgnoreCase;

    public ActivityTask? ScanningActivity { get; private set; }

    /// <summary>
    /// Gets the primary input
    /// </summary>
    public string InputA {
        get => this.inputA;
        set => PropertyHelper.SetAndRaiseINE(ref this.inputA, value ?? "", this, this.InputAChanged);
    }

    /// <summary>
    /// Gets the secondary input, used when <see cref="NumericScanType"/> is <see cref="Modes.NumericScanType.Between"/> or <see cref="Modes.NumericScanType.NotBetween"/>
    /// </summary>
    public string InputB {
        get => this.inputB;
        set => PropertyHelper.SetAndRaiseINE(ref this.inputB, value ?? "", this, this.InputBChanged);
    }

    /// <summary>
    /// Gets whether the first scan has been performed. <see cref=""/>
    /// </summary>
    public bool HasDoneFirstScan {
        get => this.hasDoneFirstScan;
        set => PropertyHelper.SetAndRaiseINE(ref this.hasDoneFirstScan, value, this, this.HasFirstScanChanged);
    }

    /// <summary>
    /// Gets whether the first scan was run when <see cref="ScanForAnyDataType"/> was true. This changes when <see cref="HasDoneFirstScan"/> changes
    /// </summary>
    public bool FirstScanWasUnknownDataType { get; private set; }

    /// <summary>
    /// Gets whether we're currently scanning.
    /// </summary>
    public bool IsScanning {
        get => field;
        private set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsScanningChanged);
    }

    /// <summary>
    /// Gets the address we start scanning at (inclusive)
    /// </summary>
    public uint StartAddress { get; private set; }

    /// <summary>
    /// Gets the amount of bytes we scan
    /// </summary>
    public uint ScanLength { get; private set; }

    /// <summary>
    /// Gets or sets the alignment value. This is continually added to the address when scanning.
    /// This is necessary when scanning data types bigger than 1 byte, because you don't want to
    /// scan for an int32 halfway through another int32 value or whatever
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Alignment cannot be 0</exception>
    public uint Alignment {
        get => field;
        set {
            if (value == 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Alignment cannot be zero");
            PropertyHelper.SetAndRaiseINE(ref field, value, this, this.AlignmentChanged);
        }
    }

    /// <summary>
    /// Gets or sets how many extra bytes should be read before and after each memory fragment for each scan result
    /// </summary>
    public uint NextScanOverRead {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, static (t) => {
            t.NextScanOverReadChanged?.Invoke(t, EventArgs.Empty);
            BasicApplicationConfiguration.Instance.NextScanOverRead = t.NextScanOverRead;
        });
    }

    /// <summary>
    /// Gets or sets if we should use the debug freeze/unfreeze commands while scanning.
    /// Freezing the console during a scan massively increases data transfer rates
    /// </summary>
    public bool PauseConsoleDuringScan {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.PauseConsoleDuringScanChanged?.Invoke(this, EventArgs.Empty);
                BasicApplicationConfiguration.Instance.PauseConsoleDuringScan = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets if we should read the console's registered memory pages,
    /// rather than blindly scanning the entirety of the configured memory region
    /// </summary>
    public bool ScanMemoryPages {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.ScanMemoryPagesChanged?.Invoke(this, EventArgs.Empty);
                BasicApplicationConfiguration.Instance.ScanMemoryPages = value;
            }
        }
    }

    public bool IsIntInputHexadecimal {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.IsIntInputHexadecimalChanged?.Invoke(this, EventArgs.Empty);
                BasicApplicationConfiguration.Instance.DTInt_UseHexValue = value;
            }

            if (value) {
                this.IsIntInputUnsigned = false;
            }
        }
    }

    public bool IsIntInputUnsigned {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.IsIntInputUnsignedChanged?.Invoke(this, EventArgs.Empty);
                BasicApplicationConfiguration.Instance.DTInt_UseUnsignedValue = value;

                if (value) {
                    this.IsIntInputHexadecimal = false;
                }
            }
        }
    }

    public bool UseFirstValueForNextScan {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.UseFirstValueForNextScanChanged?.Invoke(this, EventArgs.Empty);
                if (value)
                    this.UsePreviousValueForNextScan = false;
            }
        }
    }

    public bool UsePreviousValueForNextScan {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.UsePreviousValueForNextScanChanged?.Invoke(this, EventArgs.Empty);
                if (value)
                    this.UseFirstValueForNextScan = false;
            }
        }
    }

    public FloatScanOption FloatScanOption {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.FloatScanModeChanged?.Invoke(this, EventArgs.Empty);
                BasicApplicationConfiguration.Instance.DTFloat_Mode = value;
            }
        }
    }

    public StringType StringScanOption {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.StringScanModeChanged?.Invoke(this, EventArgs.Empty);
                BasicApplicationConfiguration.Instance.DTString_Mode = value;
            }
        }
    }

    public DataType DataType {
        get => this.dataType;
        set {
            if (this.dataType != value) {
                this.dataType = value;
                this.DataTypeChanged?.Invoke(this, EventArgs.Empty);
                this.Alignment = this.dataType.GetAlignmentFromDataType();
            }
        }
    }

    public bool ScanForAnyDataType {
        get => field;
        set {
            if (field != value) {
                if (value) {
                    this.UseExpressionParsing = false;
                }

                field = value;
                this.ScanForAnyDataTypeChanged?.Invoke(this, EventArgs.Empty);
                this.Alignment = value ? 1 : this.dataType.GetAlignmentFromDataType();
            }
        }
    }

    /// <summary>
    /// Gets or sets if the value field should be parsed as an expression, disabling <see cref="NumericScanType"/>
    /// </summary>
    public bool UseExpressionParsing {
        get => field;
        set {
            if (field != value) {
                if (value) {
                    this.ScanForAnyDataType = false;
                    this.IsIntInputHexadecimal = false;
                }

                if (value && (this.DataType == DataType.ByteArray || this.DataType == DataType.String)) {
                    this.DataType = DataType.Int32;
                }

                field = value;
                this.UseExpressionParsingChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets or sets the compare mode
    /// </summary>
    public NumericScanType NumericScanType {
        get => this.numericScanType;
        set => PropertyHelper.SetAndRaiseINE(ref this.numericScanType, value, this, this.NumericScanTypeChanged);
    }

    /// <summary>
    /// Gets or sets if strings should be searched as case-insensitive
    /// </summary>
    public bool StringIgnoreCase {
        get => this.stringIgnoreCase;
        set {
            if (this.stringIgnoreCase != value) {
                this.stringIgnoreCase = value;
                this.StringIgnoreCaseChanged?.Invoke(this, EventArgs.Empty);
                BasicApplicationConfiguration.Instance.DTString_IgnoreCase = value;
            }
        }
    }

    /// <summary>
    /// Returns true when we are currently in the process of refreshing the scan results and saved addresses
    /// </summary>
    public bool IsRefreshingAddresses {
        get => field;
        private set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsRefreshingAddressesChanged);
    }

    /// <summary>
    /// Returns <see cref="System.StringComparison.CurrentCulture"/> when <see cref="StringIgnoreCase"/> is false,
    /// and <see cref="System.StringComparison.CurrentCultureIgnoreCase"/> when true
    /// </summary>
    public StringComparison StringComparison => this.stringIgnoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;

    public bool CanPerformFirstScan => !this.IsScanning && !this.HasDoneFirstScan && this.MemoryEngine.Connection != null;
    public bool CanPerformNextScan => !this.IsScanning && this.HasDoneFirstScan && this.MemoryEngine.Connection != null;
    public bool CanPerformReset => !this.IsScanning && this.HasDoneFirstScan;

    public UnknownDataTypeOptions UnknownDataTypeOptions { get; } = new UnknownDataTypeOptions();

    /// <summary>
    /// Gets the visible scan results in the UI
    /// </summary>
    public ObservableList<ScanResultViewModel> ScanResults { get; } = [];

    /// <summary>
    /// Returns the actual amount of results (visible and hidden). The user may choose to cancel the
    /// "Updating result list..." operation and instead leave the remaining results as hidden.
    /// They will still be processed, just not visible
    /// </summary>
    public int ActualScanResultCount => this.ScanResults.Count + this.resultBuffer.Count;

    public MemoryEngine MemoryEngine { get; }

    public event EventHandler? InputAChanged, InputBChanged;
    public event EventHandler? HasFirstScanChanged;
    public event EventHandler? IsScanningChanged;
    public event EventHandler? PauseConsoleDuringScanChanged;
    public event EventHandler? IsIntInputHexadecimalChanged;
    public event EventHandler? IsIntInputUnsignedChanged;
    public event EventHandler? UseFirstValueForNextScanChanged;
    public event EventHandler? UsePreviousValueForNextScanChanged;
    public event EventHandler? FloatScanModeChanged;
    public event EventHandler? StringScanModeChanged;
    public event EventHandler? DataTypeChanged;
    public event EventHandler? ScanForAnyDataTypeChanged;
    public event EventHandler? NumericScanTypeChanged;
    public event EventHandler? UseExpressionParsingChanged;
    public event EventHandler? StringIgnoreCaseChanged;
    public event EventHandler? AlignmentChanged;
    public event EventHandler? ScanMemoryPagesChanged;
    public event EventHandler? IsRefreshingAddressesChanged;
    public event EventHandler? NextScanOverReadChanged;

    /// <summary>
    /// An event fired when <see cref="SetScanRange"/> is invoked. This provides the old values for <see cref="StartAddress"/> and <see cref="ScanLength"/>
    /// </summary>
    public event EventHandler<ScanRangeChangedEventArgs>? ScanRangeChanged;

    private readonly ConcurrentQueue<ScanResultViewModel> resultBuffer;
    private readonly RateLimitedDispatchAction rldaMoveBufferIntoResultList;
    private readonly RateLimitedDispatchAction rldaRefreshSavedAddressList;

    public ScanningProcessor(MemoryEngine memoryEngine) {
        this.MemoryEngine = memoryEngine ?? throw new ArgumentNullException(nameof(memoryEngine));
        BasicApplicationConfiguration cfg = BasicApplicationConfiguration.Instance;

        this.inputA = this.inputB = "";
        this.dataType = DataType.Int32;
        this.numericScanType = NumericScanType.Equals;
        this.StartAddress = cfg.StartAddress;
        this.ScanLength = cfg.ScanLength;
        if (((ulong) this.StartAddress + this.ScanLength) > uint.MaxValue) {
            this.ScanLength = uint.MaxValue - this.StartAddress;
        }

        this.Alignment = this.dataType.GetAlignmentFromDataType();
        this.NextScanOverRead = cfg.NextScanOverRead;
        this.PauseConsoleDuringScan = cfg.PauseConsoleDuringScan;
        this.ScanMemoryPages = cfg.ScanMemoryPages;
        this.IsIntInputHexadecimal = cfg.DTInt_UseHexValue;
        this.IsIntInputUnsigned = cfg.DTInt_UseUnsignedValue;
        this.FloatScanOption = cfg.DTFloat_Mode;
        this.StringScanOption = cfg.DTString_Mode;
        this.StringIgnoreCase = cfg.DTString_IgnoreCase;

        this.resultBuffer = new ConcurrentQueue<ScanResultViewModel>();

        this.ScanResults.CollectionChanged += (sender, args) => {
            ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        };

        this.rldaMoveBufferIntoResultList = RateLimitedDispatchActionBase.ForDispatcherSync(() => {
            List<ScanResultViewModel> list = new List<ScanResultViewModel>(20);
            for (int i = 0; i < 20 && this.resultBuffer.TryDequeue(out ScanResultViewModel? result); i++)
                list.Add(result);

            this.ScanResults.AddRange(list);
        }, TimeSpan.FromMilliseconds(100), DispatchPriority.BeforeRender);

        this.rldaRefreshSavedAddressList = RateLimitedDispatchActionBase.ForDispatcherAsync(this.RefreshSavedAddressesAsync, TimeSpan.FromMilliseconds(100));

        this.MemoryEngine.ConnectionAboutToChange += this.OnEngineConnectionAboutToChange;
    }

    private async Task OnEngineConnectionAboutToChange(object? o, ConnectionChangingEventArgs args) {
        args.Progress.SetCaptionAndText("Memory Scan", "Stopping current scan...");
        if (this.ScanningActivity != null && this.ScanningActivity.IsRunning) {
            if (this.ScanningActivity.TryCancel()) { // should always return true
                await this.ScanningActivity;
            }
        }
    }

    /// <summary>
    /// Sets the <see cref="StartAddress"/> and <see cref="ScanLength"/> values. This is necessary to prevent risking
    /// <see cref="ArgumentOutOfRangeException"/> when setting them individually due to integer overflow.
    /// </summary>
    /// <param name="newStartAddress">The new start address</param>
    /// <param name="newScanLength">The new scan length</param>
    /// <param name="updateConfiguration">Updates the values in <see cref="BasicApplicationConfiguration"/></param>
    public void SetScanRange(uint newStartAddress, uint newScanLength, bool updateConfiguration = true) {
        if ((newStartAddress + newScanLength) < newStartAddress) {
            // should we just support 64 bit addresses?
            // I don't even think there's anything useful beyond 0xFFFFFFFF...
            // and the xbox can't even physically address that far
            throw new ArgumentException("New scan range exceed size of UInt32; it requires a 64 bit address range, which is unsupported");
        }

        // Just to ensure there isn't weird glitches by changing both before events,
        // we first set length to 0 so that we can change StartAddress without
        // risking observers doing newValue + processor.ScanLength and overflowing

        uint oldStart = this.StartAddress, oldLength = this.ScanLength;
        this.StartAddress = newStartAddress;
        this.ScanLength = newScanLength;

        this.ScanRangeChanged?.Invoke(this, new ScanRangeChangedEventArgs(oldStart, oldLength));

        if (updateConfiguration) {
            BasicApplicationConfiguration.Instance.StartAddress = this.StartAddress;
            BasicApplicationConfiguration.Instance.ScanLength = this.ScanLength;
        }
    }

    public void ClearResults() {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        this.resultBuffer.Clear();
        this.ScanResults.Clear();
    }

    public List<ScanResultViewModel> GetScanResultsAndQueued(bool clearQueue = false) {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        List<ScanResultViewModel> list = this.ScanResults.ToList();
        list.AddRange(this.resultBuffer);
        if (clearQueue)
            this.resultBuffer.Clear();
        return list;
    }

    public async Task ScanFirstOrNext() {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();

        if (this.IsScanning)
            throw new InvalidOperationException("Currently scanning");

        IConsoleConnection? connection = this.MemoryEngine.Connection;
        if (connection == null)
            throw new InvalidOperationException("No console connection");

        IBusyToken? theCoolToken = await this.MemoryEngine.BeginBusyOperationUsingActivityAsync("Pre-Scan Setup");
        if (theCoolToken == null)
            return; // user cancelled token fetch

        Reference<IBusyToken?> busyTokenRef = new Reference<IBusyToken?>(theCoolToken);

        try {
            if ((connection = this.MemoryEngine.Connection) == null)
                return; // rare disconnection before token acquired

            if (this.MemoryEngine.IsShuttingDown)
                return; // program shutting down before token acquired

            bool pauseDuringScan = this.PauseConsoleDuringScan;
            bool scanForAnything = this.ScanForAnyDataType;
            ScanningContext context =
                (this.hasDoneFirstScan ? this.FirstScanWasUnknownDataType : scanForAnything)
                    ? new AnyTypeScanningContext(this)
                    : new DataTypedScanningContext(this);

            if (!await context.Setup(connection)) {
                return;
            }

            // should be impossible since we obtain the busy token which is required before scanning

            Debug.Assert(!this.IsScanning, "WTF");

            DispatcherActivityProgress progress = new DispatcherActivityProgress {
                Caption = "Memory Scan", Text = "Beginning scan..."
            };

            using CancellationTokenSource cts = new CancellationTokenSource();

            this.ScanningActivity = ActivityManager.Instance.RunTask(RunScanInAction, progress, cts);
            await this.ScanningActivity;
            this.ScanningActivity = null;
            return;

            async Task RunScanInAction() {
                ActivityTask thisTask = ActivityTask.Current;
                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    // If for some reason it gets force disconnected in an already scheduled
                    // dispatcher operation, we should just safely stop scanning
                    if (connection.IsClosed || this.MemoryEngine.IsShuttingDown) {
                        return;
                    }

                    this.IsScanning = true;
                }, token: CancellationToken.None);

                bool result = false;
                if (this.IsScanning && !connection.IsClosed) {
                    thisTask.CancellationToken.ThrowIfCancellationRequested();
                    try {
                        context.ResultFound += (sender, model) => {
                            this.resultBuffer.Enqueue(model);
                            this.rldaMoveBufferIntoResultList.InvokeAsync();
                        };

                        if (this.hasDoneFirstScan) {
                            progress.Text = "Accumulating scan results...";

                            async Task<List<ScanResultViewModel>?> TrySetupScan() {
                                List<ScanResultViewModel> items = this.ScanResults.ToList();
                                items.AddRange(this.resultBuffer);
                                if (!await context.CanRunNextScan(items)) {
                                    return null;
                                }

                                this.ScanResults.Clear();
                                this.resultBuffer.Clear();
                                return items;
                            }

                            List<ScanResultViewModel>? srcList = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(TrySetupScan, captureContext: true).Unwrap();
                            if (srcList != null) {
                                bool canContinue = false;
                                progress.Text = "Scanning...";

                                bool isAlreadyFrozen = false;
                                IFeatureIceCubes? iceCubes = pauseDuringScan ? connection.GetFeatureOrDefault<IFeatureIceCubes>() : null;
                                try {
                                    if (iceCubes != null && !connection.IsClosed) {
                                        isAlreadyFrozen = await iceCubes.DebugFreeze() == FreezeResult.AlreadyFrozen;
                                    }

                                    canContinue = true;
                                }
                                catch (Exception e) when (e is IOException || e is TimeoutException) {
                                    await IMessageDialogService.Instance.ShowMessage(e is IOException ? "Connection IO Error" : "Connection Timed Out", "Error freezing console", e.Message);
                                }
                                catch (Exception e) {
                                    await IMessageDialogService.Instance.ShowExceptionMessage("Unexpected Error", "Unexpected error freezing console.", e);
                                }

                                if (canContinue) {
                                    try {
                                        result = true;
                                        await context.PerformNextScan(connection, srcList, busyTokenRef);
                                    }
                                    catch (OperationCanceledException) {
                                        // ignored
                                    }
                                    catch (Exception e) when (e is IOException || e is TimeoutException) {
                                        await IMessageDialogService.Instance.ShowMessage(e is IOException ? "Connection IO Error" : "Connection Timed Out", "Connection error while performing next scan", e.Message);
                                        result = false;
                                    }
                                    catch (Exception e) {
                                        Debugger.Break();
                                        await IMessageDialogService.Instance.ShowExceptionMessage("Unexpected Error", "Error performing next scan.", e);
                                        result = false;
                                    }

                                    try {
                                        if (iceCubes != null && !isAlreadyFrozen && !connection.IsClosed) {
                                            await iceCubes.DebugUnFreeze();
                                        }
                                    }
                                    catch (Exception e) when (e is IOException || e is TimeoutException) {
                                        await IMessageDialogService.Instance.ShowMessage(e is IOException ? "Connection IO Error" : "Connection Timed Out", "Error unfreezing console", e.Message);
                                        result = false;
                                    }
                                    catch (Exception e) {
                                        await IMessageDialogService.Instance.ShowExceptionMessage("Unexpected Error", "Unexpected error unfreezing console.", e);
                                        result = false;
                                    }
                                }
                            }
                            else {
                                result = false;
                            }
                        }
                        else {
                            progress.Text = "Scanning...";

                            // ScanningContext objects do not dispose of the token when they run to completion,
                            // which means we do not have to reacquire it in this method
                            try {
                                result = true;
                                await context.PerformFirstScan(connection, busyTokenRef);
                            }
                            catch (OperationCanceledException) {
                                // ignored
                            }
                            catch (Exception e) when (e is IOException || e is TimeoutException) {
                                await IMessageDialogService.Instance.ShowMessage(e is IOException ? "Connection IO Error" : "Connection Timed Out", "Connection error while performing first scan", e.Message);
                                result = false;
                            }
                            catch (Exception e) {
                                Debugger.Break();
                                await IMessageDialogService.Instance.ShowExceptionMessage("Unexpected Error", "Error performing first scan.", e);
                                result = false;
                            }
                        }

                        this.rldaMoveBufferIntoResultList.InvokeAsync();
                    }
                    catch {
                        Debugger.Break();
                        result = false;
                    }

                    Task updateListTask = Task.CompletedTask;
                    if (result && !this.MemoryEngine.IsShuttingDown && !thisTask.IsCancellationRequested && !this.resultBuffer.IsEmpty) {
                        progress.Text = "Updating result list...";
                        int count = this.resultBuffer.Count;
                        const int chunkSize = 500;
                        updateListTask = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(async () => {
                            using PopCompletionStateRangeToken x = progress.CompletionState.PushCompletionRange(0.0, 1.0 / ((double) count / chunkSize));
                            List<ScanResultViewModel> list = new List<ScanResultViewModel>(Math.Min(chunkSize, count));
                            while (!this.resultBuffer.IsEmpty) {
                                for (int i = 0; i < chunkSize && this.resultBuffer.TryDequeue(out ScanResultViewModel? scanResult); i++) {
                                    list.Add(scanResult);
                                }

                                this.ScanResults.AddRange(list);
                                list.Clear();

                                progress.CompletionState.OnProgress(1.0);
                                try {
                                    await Task.Delay(50, thisTask.CancellationToken);
                                }
                                catch (OperationCanceledException) {
                                    return;
                                }
                            }
                        }, token: CancellationToken.None);
                    }

                    if (context.HasConnectionError) {
                        await IMessageDialogService.Instance.ShowMessage("Network error", context.ConnectionException!.Message, "Please reconnect to the console", defaultButton: MessageBoxResult.OK);
                    }

                    await updateListTask;
                }

                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    this.FirstScanWasUnknownDataType = scanForAnything;
                    this.HasDoneFirstScan = result;
                    this.IsScanning = false;
                    if (!this.MemoryEngine.IsShuttingDown) { // another race condition i suppose
                        IBusyToken? token = busyTokenRef.Value;
                        if (this.MemoryEngine.BusyLock.IsTokenValid(token)) {
                            this.MemoryEngine.CheckConnection(token);
                        }
                        else {
                            this.MemoryEngine.CheckConnection();
                        }
                    }
                }, token: CancellationToken.None);
            }
        }
        finally {
            busyTokenRef.Value?.Dispose();
            busyTokenRef.Value = null;
        }
    }

    public void ResetScan() {
        if (this.IsScanning)
            throw new InvalidOperationException("Currently scanning");

        this.ClearResults();
        this.FirstScanWasUnknownDataType = false;
        this.HasDoneFirstScan = false;
        this.UseFirstValueForNextScan = false;
        this.UsePreviousValueForNextScan = false;
    }

    /// <summary>
    /// Signals to the updater to refresh the current value of saved addresses
    /// </summary>
    public void RefreshSavedAddressesLater() {
        this.rldaRefreshSavedAddressList.InvokeAsync();
    }

    public Task RefreshSavedAddressesAsync() => this.RefreshSavedAddressesAsync(false);

    public async Task RefreshSavedAddressesAsync(bool bypassLimits, bool invalidateCaches = false) {
        if (this.IsRefreshingAddresses || this.MemoryEngine.IsConnectionBusy || this.MemoryEngine.Connection == null) {
            return; // concurrent operations are dangerous and can corrupt the communication pipe until restarting connection
        }

        if (this.resultBuffer.IsEmpty && this.ScanResults.Count < 1 && this.MemoryEngine.AddressTableManager.RootEntry.Items.Count < 1) {
            return; // nothing to update so do nothing
        }

        using IBusyToken? token = this.MemoryEngine.TryBeginBusyOperation();
        if (token == null) {
            return; // do not read while connection busy
        }

        await this.RefreshSavedAddressesAsync(token, bypassLimits, invalidateCaches);
    }

    private sealed class AddressTableRefreshEntry(AddressTableEntry entry, IntegerRange<uint> memorySpan) {
        public readonly AddressTableEntry Entry = entry;
        public readonly IntegerRange<uint> MemorySpan = memorySpan; // maximum length
        public readonly DataType DataType = entry.DataType;
        public IDataValue NewValue = null!; // cheeky
    }

    private sealed class ScanResultRefreshEntry(ScanResultViewModel entry, IntegerRange<uint> memorySpan) {
        public readonly ScanResultViewModel Entry = entry;
        public readonly IntegerRange<uint> MemorySpan = memorySpan; // maximum length
        public readonly DataType DataType = entry.DataType;
        public IDataValue NewValue = null!; // cheeky
    }

    /// <summary>
    /// Refreshes the saved address list
    /// </summary>
    /// <param name="busyOperationToken">The busy operation token. Does not dispose once finished</param>
    /// <exception cref="InvalidOperationException">No connection is present</exception>
    public async Task RefreshSavedAddressesAsync(IBusyToken busyOperationToken, bool bypassLimits = false, bool invalidateCaches = false) {
        this.MemoryEngine.BusyLock.ValidateToken(busyOperationToken);
        if (this.IsRefreshingAddresses) {
            throw new InvalidOperationException("Already refreshing");
        }

        if (this.MemoryEngine.IsShuttingDown) {
            return;
        }

        IConsoleConnection connection = this.MemoryEngine.Connection ?? throw new InvalidOperationException("No connection present");

        uint max = BasicApplicationConfiguration.Instance.MaxRowsBeforeDisableAutoRefresh;

        // TODO: maybe batch together results whose addresses are close by, and read a single chunk?
        // May be faster if the console is not debug frozen and we have to update 100s of results...
        List<AddressTableEntry>? savedList = new List<AddressTableEntry>(100);
        foreach (AddressTableEntry saved in this.MemoryEngine.AddressTableManager.GetAllAddressEntries()) {
            if (saved.IsAutoRefreshEnabled && saved.IsVisibleInMainSavedResultList) {
                if (!bypassLimits && savedList.Count > max) {
                    savedList = null;
                    break;
                }

                savedList.Add(saved);
            }
        }

        // Lazily prevents concurrent modification due to awaiting read text
        List<ScanResultViewModel>? list = (savedList == null || (!bypassLimits && this.ScanResults.Count > max)) ? null : this.ScanResults.ToList();
        if ((savedList == null || savedList.Count < 1) && (list == null || list.Count < 1)) {
            return;
        }

        int grandTotalCount = (list?.Count ?? 0) + (savedList?.Count ?? 0);
        if (!bypassLimits && grandTotalCount > max) {
            return;
        }

        int networkErrorType = 0;
        this.IsRefreshingAddresses = true;
        try {
            using CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            // The previous implementation of this was just awaiting ReadAsText on the main thread
            // for each saved address and scan result.
            // 
            // However, although it didn't lag the UI, it took forever to update lots of results,
            // because each time it awaited ReadAsText it almost always has to jump back to the
            // main thread (from a background thread where maybe the TCP connection fired the continuation).
            // 
            // Rather than mess around with ConfigureAwait(false) I decided to just make it
            // all run on a BG task, and if it's been running for over 500ms, then show an activity
            // to let the user cancel it (maybe the connection was disconnected or is slow)

            Task readOperationTask = Task.Run(async () => {
                token.ThrowIfCancellationRequested();
                List<AddressTableRefreshEntry> ateList = new List<AddressTableRefreshEntry>();
                List<ScanResultRefreshEntry> resultList = new List<ScanResultRefreshEntry>();
                bool isLittleEndian = connection.IsLittleEndian;

                if (savedList != null) {
                    foreach (AddressTableEntry entry in savedList) {
                        token.ThrowIfCancellationRequested();
                        if (entry.IsAutoRefreshEnabled) { // may change between dispatcher callbacks
                            uint? addr = await entry.MemoryAddress.TryResolveAddress(connection, invalidateCaches);
                            if (addr.HasValue)
                                ateList.Add(new AddressTableRefreshEntry(entry, IntegerRange.FromStartAndLength(addr.Value, (uint) MemoryEngine.GetMaximumDataValueSize(entry, isLittleEndian))));
                        }
                    }
                }

                if (list != null) {
                    foreach (ScanResultViewModel result in list) {
                        token.ThrowIfCancellationRequested();
                        resultList.Add(new ScanResultRefreshEntry(result, IntegerRange.FromStartAndLength(result.Address, (uint) MemoryEngine.GetMaximumDataValueSize(result, isLittleEndian))));
                    }
                }

                IntegerSet<uint> fragmentSet = new IntegerSet<uint>();
                foreach (AddressTableRefreshEntry entry in ateList)
                    fragmentSet.Add(entry.MemorySpan);
                foreach (ScanResultRefreshEntry entry in resultList)
                    fragmentSet.Add(entry.MemorySpan);

                FragmentedMemoryBuffer memoryBuffer = await MemoryEngine.CreateMemoryView(connection, fragmentSet, default, cancellationToken: token);
                if (savedList != null) {
                    foreach (AddressTableRefreshEntry re in ateList) {
                        token.ThrowIfCancellationRequested();
                        if (re.Entry.IsAutoRefreshEnabled && re.Entry.DataType == re.DataType) { // may change between dispatcher callbacks
                            re.NewValue = MemoryEngine.ReadDataValueFromFragmentBuffer(memoryBuffer, re.MemorySpan, re.DataType, re.Entry.StringType, re.Entry.StringLength, re.Entry.ArrayLength, connection.IsLittleEndian);
                        }
                    }

                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                        // Only <=100 values to update, so not too UI intensive
                        foreach (AddressTableRefreshEntry re in ateList) {
                            if (re.Entry.IsAutoRefreshEnabled && re.Entry.DataType == re.DataType) // may change between dispatcher callbacks
                                re.Entry.Value = re.NewValue;
                        }
                    }, token: CancellationToken.None);
                }

                // safety net -- we still need to implement logic to notify view models when they're visible in the
                // UI, although this does kind of break the MVVM pattern but oh well
                if (list != null) {
                    foreach (ScanResultRefreshEntry re in resultList) {
                        token.ThrowIfCancellationRequested();
                        if (re.Entry.DataType == re.DataType)
                            re.NewValue = MemoryEngine.ReadDataValueFromFragmentBuffer(memoryBuffer, re.MemorySpan, re.DataType, re.Entry.StringType, re.Entry.CurrentStringLength, re.Entry.CurrentArrayLength, connection.IsLittleEndian);
                    }

                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                        // Only <=100 values to update, so not too UI intensive
                        foreach (ScanResultRefreshEntry re in resultList) {
                            if (re.Entry.DataType == re.DataType) // may change between dispatcher callbacks
                                re.Entry.CurrentValue = re.NewValue;
                        }
                    }, token: CancellationToken.None);
                }
            }, CancellationToken.None);

            await Task.WhenAny(readOperationTask, Task.Delay(500, token));

            if (!readOperationTask.IsCompleted) {
                await ActivityManager.Instance.RunTask(async () => {
                    IActivityProgress p = ActivityTask.Current.Progress;
                    p.Caption = "Long refresh";
                    p.Text = $"Refreshing {grandTotalCount} value{Lang.S(grandTotalCount)}...";
                    p.IsIndeterminate = true;
                    networkErrorType = await AwaitOperation(readOperationTask);
                }, cts);
            }
            else {
                // We must always await the task, even if it has completed, so that
                // the exception can get handled and won't be unobserved which would,
                // at some point during task finalization, crash the app
                networkErrorType = await AwaitOperation(readOperationTask);
            }

            static async Task<int> AwaitOperation(Task task) {
                try {
                    await task;
                    return 0;
                }
                catch (IOException) {
                    return 1;
                }
                catch (TimeoutException) {
                    return 2;
                }
                catch (Exception e) {
                    AppLogger.Instance.WriteLine("Unexpected exception during refresh operation");
                    AppLogger.Instance.WriteLine(e.GetToString());
                    return 3;
                }
            }
        }
        finally {
            this.IsRefreshingAddresses = false;
        }

        if (networkErrorType != 0) {
            this.MemoryEngine.CheckConnection(busyOperationToken,
                networkErrorType == 2 // Timeout
                    ? ConnectionChangeCause.LostConnection
                    : ConnectionChangeCause.ConnectionError);
        }
    }
}

// Separate class because the ScanningProcessor is already stuffed full enough of properties
public sealed class UnknownDataTypeOptions {
    /// <summary>
    /// Do not add/remove items!!! Only move them
    /// </summary>
    public ObservableList<ScanningOrderModel> Orders { get; }

    private readonly ScanningOrderModel orderByte;
    private readonly ScanningOrderModel orderInt16;
    private readonly ScanningOrderModel orderInt32;
    private readonly ScanningOrderModel orderInt64;

    public bool CanSearchForByte => this.orderByte.IsEnabled;

    public bool CanSearchForShort => this.orderInt16.IsEnabled;

    public bool CanSearchForInt => this.orderInt32.IsEnabled;

    public bool CanSearchForLong => this.orderInt64.IsEnabled;

    public bool CanSearchForFloat {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.CanSearchForFloatChanged);
    } = true;

    public bool CanSearchForDouble {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.CanSearchForDoubleChanged);
    } = true;

    public bool CanSearchForString {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.CanSearchForStringChanged);
    } = true;

    public bool CanRunNextScanForByteArray {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.CanRunNextScanForByteArrayChanged);
    } = true;

    public event EventHandler? CanSearchForFloatChanged;
    public event EventHandler? CanSearchForDoubleChanged;
    public event EventHandler? CanSearchForStringChanged;
    public event EventHandler? CanRunNextScanForByteArrayChanged;

    public UnknownDataTypeOptions() {
        this.Orders = new ObservableList<ScanningOrderModel>() {
            (this.orderInt32 = new ScanningOrderModel(DataType.Int32)),
            (this.orderInt16 = new ScanningOrderModel(DataType.Int16)),
            (this.orderByte = new ScanningOrderModel(DataType.Byte)),
            (this.orderInt64 = new ScanningOrderModel(DataType.Int64)),
        };

        this.Orders.ValidateAdd += (list, index, items) => throw new InvalidOperationException("Items cannot be added to this list");
        this.Orders.ValidateRemove += (list, index, count) => throw new InvalidOperationException("Items cannot be removed from this list");
        this.Orders.ValidateReplace += (list, index, oldItem, newItem) => throw new InvalidOperationException("Items cannot be replaced in this list");
    }

    /// <summary>
    /// The order in which we scan integer types. Default order is int32, int16 , byte and finally int64.
    /// This order is used because int32 is the most common data type, next to int16 and then byte. int64 is uncommon hence it's last. 
    /// </summary>
    public DataType[] GetIntDataTypeOrdering() {
        DataType[] array = this.Orders.Select(x => x.DataType).ToArray();
        Debug.Assert(array.Length == 4);
        foreach (DataType dt in array) {
            Debug.Assert(dt.IsInteger());
        }

        return array;
    }
}

public readonly struct ScanRangeChangedEventArgs(uint oldAddress, uint oldLength) {
    public uint OldAddress { get; } = oldAddress;
    public uint OldLength { get; } = oldLength;
}