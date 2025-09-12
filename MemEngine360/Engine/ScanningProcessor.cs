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
using PFXToolKitUI.Logging;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.Engine;

public delegate void ScanningProcessorEventHandler(ScanningProcessor sender);

public delegate void ScanningProcessorScanRangeChangedEventHandler(ScanningProcessor sender, uint oldAddress, uint oldLength);

public delegate void UnknownDataTypeOptionsEventHandler(UnknownDataTypeOptions sender);

public class ScanningProcessor {
    private string inputA, inputB;
    private bool hasDoneFirstScan;
    private bool isScanning;
    private uint alignment;
    private bool pauseConsoleDuringScan, scanMemoryPages, isIntInputHexadecimal;
    private bool nextScanUsesFirstValue, nextScanUsesPreviousValue;
    private FloatScanOption floatScanOption;
    private StringType stringScanOption;
    private DataType dataType;
    private bool scanForAnyDataType;
    private NumericScanType numericScanType;
    private bool stringIgnoreCase;
    private bool isRefreshingAddresses;

    public ActivityTask? ScanningActivity { get; private set; }

    /// <summary>
    /// Gets the primary input
    /// </summary>
    public string InputA {
        get => this.inputA;
        set => PropertyHelper.SetAndRaiseINE(ref this.inputA, value ?? "", this, static t => t.InputAChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets the secondary input, used when <see cref="NumericScanType"/> is <see cref="Modes.NumericScanType.Between"/> or <see cref="Modes.NumericScanType.NotBetween"/>
    /// </summary>
    public string InputB {
        get => this.inputB;
        set => PropertyHelper.SetAndRaiseINE(ref this.inputB, value ?? "", this, static t => t.InputBChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets whether the first scan has been performed. <see cref=""/>
    /// </summary>
    public bool HasDoneFirstScan {
        get => this.hasDoneFirstScan;
        set => PropertyHelper.SetAndRaiseINE(ref this.hasDoneFirstScan, value, this, static t => t.HasFirstScanChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets whether the first scan was run when <see cref="ScanForAnyDataType"/> was true. This changes when <see cref="HasDoneFirstScan"/> changes
    /// </summary>
    public bool FirstScanWasUnknownDataType { get; private set; }

    /// <summary>
    /// Gets whether we're currently scanning.
    /// </summary>
    public bool IsScanning {
        get => this.isScanning;
        private set => PropertyHelper.SetAndRaiseINE(ref this.isScanning, value, this, static t => t.IsScanningChanged?.Invoke(t));
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
        get => this.alignment;
        set {
            if (value == 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Alignment cannot be zero");
            PropertyHelper.SetAndRaiseINE(ref this.alignment, value, this, static t => t.AlignmentChanged?.Invoke(t));
        }
    }

    /// <summary>
    /// Gets or sets if we should use the debug freeze/unfreeze commands while scanning.
    /// Freezing the console during a scan massively increases data transfer rates
    /// </summary>
    public bool PauseConsoleDuringScan {
        get => this.pauseConsoleDuringScan;
        set {
            if (this.pauseConsoleDuringScan != value) {
                this.pauseConsoleDuringScan = value;
                this.PauseConsoleDuringScanChanged?.Invoke(this);
                BasicApplicationConfiguration.Instance.PauseConsoleDuringScan = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets if we should read the console's registered memory pages,
    /// rather than blindly scanning the entirety of the configured memory region
    /// </summary>
    public bool ScanMemoryPages {
        get => this.scanMemoryPages;
        set {
            if (this.scanMemoryPages != value) {
                this.scanMemoryPages = value;
                this.ScanMemoryPagesChanged?.Invoke(this);
                BasicApplicationConfiguration.Instance.ScanMemoryPages = value;
            }
        }
    }

    public bool IsIntInputHexadecimal {
        get => this.isIntInputHexadecimal;
        set {
            if (this.isIntInputHexadecimal != value) {
                this.isIntInputHexadecimal = value;
                this.IsIntInputHexadecimalChanged?.Invoke(this);
                BasicApplicationConfiguration.Instance.DTInt_UseHexValue = value;
            }
        }
    }

    public bool UseFirstValueForNextScan {
        get => this.nextScanUsesFirstValue;
        set {
            if (this.nextScanUsesFirstValue != value) {
                this.nextScanUsesFirstValue = value;
                this.UseFirstValueForNextScanChanged?.Invoke(this);
                if (value)
                    this.UsePreviousValueForNextScan = false;
            }
        }
    }

    public bool UsePreviousValueForNextScan {
        get => this.nextScanUsesPreviousValue;
        set {
            if (this.nextScanUsesPreviousValue != value) {
                this.nextScanUsesPreviousValue = value;
                this.UsePreviousValueForNextScanChanged?.Invoke(this);
                if (value)
                    this.UseFirstValueForNextScan = false;
            }
        }
    }

    public FloatScanOption FloatScanOption {
        get => this.floatScanOption;
        set {
            if (this.floatScanOption != value) {
                this.floatScanOption = value;
                this.FloatScanModeChanged?.Invoke(this);
                BasicApplicationConfiguration.Instance.DTFloat_Mode = value;
            }
        }
    }

    public StringType StringScanOption {
        get => this.stringScanOption;
        set {
            if (this.stringScanOption != value) {
                this.stringScanOption = value;
                this.StringScanModeChanged?.Invoke(this);
                BasicApplicationConfiguration.Instance.DTString_Mode = value;
            }
        }
    }

    public DataType DataType {
        get => this.dataType;
        set {
            if (this.dataType != value) {
                this.dataType = value;
                this.DataTypeChanged?.Invoke(this);
                this.Alignment = this.dataType.GetAlignmentFromDataType();
            }
        }
    }

    public bool ScanForAnyDataType {
        get => this.scanForAnyDataType;
        set {
            if (this.scanForAnyDataType != value) {
                this.scanForAnyDataType = value;
                this.ScanForAnyDataTypeChanged?.Invoke(this);
                this.Alignment = value ? 1 : this.dataType.GetAlignmentFromDataType();
            }
        }
    }

    public NumericScanType NumericScanType {
        get => this.numericScanType;
        set => PropertyHelper.SetAndRaiseINE(ref this.numericScanType, value, this, static t => t.NumericScanTypeChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets if strings should be searched as case-insensitive
    /// </summary>
    public bool StringIgnoreCase {
        get => this.stringIgnoreCase;
        set {
            if (this.stringIgnoreCase != value) {
                this.stringIgnoreCase = value;
                this.StringIgnoreCaseChanged?.Invoke(this);
                BasicApplicationConfiguration.Instance.DTString_IgnoreCase = value;
            }
        }
    }

    /// <summary>
    /// Returns true when we are currently in the process of refreshing the scan results and saved addresses
    /// </summary>
    public bool IsRefreshingAddresses {
        get => this.isRefreshingAddresses;
        private set => PropertyHelper.SetAndRaiseINE(ref this.isRefreshingAddresses, value, this, static t => t.IsRefreshingAddressesChanged?.Invoke(t));
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

    public event ScanningProcessorEventHandler? InputAChanged, InputBChanged;
    public event ScanningProcessorEventHandler? HasFirstScanChanged;
    public event ScanningProcessorEventHandler? IsScanningChanged;
    public event ScanningProcessorEventHandler? PauseConsoleDuringScanChanged;
    public event ScanningProcessorEventHandler? IsIntInputHexadecimalChanged;
    public event ScanningProcessorEventHandler? UseFirstValueForNextScanChanged;
    public event ScanningProcessorEventHandler? UsePreviousValueForNextScanChanged;
    public event ScanningProcessorEventHandler? FloatScanModeChanged;
    public event ScanningProcessorEventHandler? StringScanModeChanged;
    public event ScanningProcessorEventHandler? DataTypeChanged;
    public event ScanningProcessorEventHandler? ScanForAnyDataTypeChanged;
    public event ScanningProcessorEventHandler? NumericScanTypeChanged;
    public event ScanningProcessorEventHandler? StringIgnoreCaseChanged;
    public event ScanningProcessorEventHandler? AlignmentChanged;
    public event ScanningProcessorEventHandler? ScanMemoryPagesChanged;
    public event ScanningProcessorEventHandler? IsRefreshingAddressesChanged;

    /// <summary>
    /// An event fired when <see cref="SetScanRange"/> is invoked. This provides the old values for <see cref="StartAddress"/> and <see cref="ScanLength"/>
    /// </summary>
    public event ScanningProcessorScanRangeChangedEventHandler? ScanRangeChanged;

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

        this.alignment = this.dataType.GetAlignmentFromDataType();
        this.pauseConsoleDuringScan = cfg.PauseConsoleDuringScan;
        this.scanMemoryPages = cfg.ScanMemoryPages;
        this.isIntInputHexadecimal = cfg.DTInt_UseHexValue;
        this.floatScanOption = cfg.DTFloat_Mode;
        this.stringScanOption = cfg.DTString_Mode;
        this.stringIgnoreCase = cfg.DTString_IgnoreCase;

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

    private async Task OnEngineConnectionAboutToChange(MemoryEngine sender, ulong frame) {
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

        this.ScanRangeChanged?.Invoke(this, oldStart, oldLength);

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

        if (this.isScanning)
            throw new InvalidOperationException("Currently scanning");

        IConsoleConnection? connection = this.MemoryEngine.Connection;
        if (connection == null)
            throw new InvalidOperationException("No console connection");

        IDisposable? token = await this.MemoryEngine.BeginBusyOperationActivityAsync("Pre-Scan Setup");
        if (token == null)
            return; // user cancelled token fetch

        try {
            if ((connection = this.MemoryEngine.Connection) == null)
                return; // rare disconnection before token acquired

            if (this.MemoryEngine.IsShuttingDown)
                return; // program shutting down before token acquired

            bool pauseDuringScan = this.pauseConsoleDuringScan;
            bool scanForAnything = this.ScanForAnyDataType;
            ScanningContext context =
                (this.hasDoneFirstScan ? this.FirstScanWasUnknownDataType : scanForAnything)
                    ? new AnyTypeScanningContext(this)
                    : new DataTypedScanningContext(this);

            if (!await context.Setup(connection)) {
                return;
            }

            // should be impossible since we obtain the busy token which is required before scanning

            Debug.Assert(this.isScanning == false, "WTF");

            ConcurrentActivityProgress progress = new ConcurrentActivityProgress {
                Caption = "Memory Scan", Text = "Beginning scan..."
            };

            using CancellationTokenSource cts = new CancellationTokenSource();
            this.ScanningActivity = ActivityManager.Instance.RunTask(async () => {
                ActivityTask thisTask = ActivityManager.Instance.CurrentTask;
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
                            List<ScanResultViewModel>? srcList = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(async () => {
                                List<ScanResultViewModel> items = this.ScanResults.ToList();
                                items.AddRange(this.resultBuffer);
                                if (!await context.CanRunNextScan(items)) {
                                    return null;
                                }

                                this.ScanResults.Clear();
                                this.resultBuffer.Clear();
                                return items;
                            }).Unwrap();

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
                                    await LogExceptionHelper.ShowMessageAndPrintToLogs("Unexpected Error", "Unexpected error freezing console.", e);
                                }

                                if (canContinue) {
                                    try {
                                        result = true;
                                        token = await context.PerformNextScan(connection, srcList, token);
                                    }
                                    catch (OperationCanceledException) {
                                        Debugger.Break(); // OCE should not be thrown
                                    }
                                    catch (Exception e) when (e is IOException || e is TimeoutException) {
                                        await IMessageDialogService.Instance.ShowMessage(e is IOException ? "Connection IO Error" : "Connection Timed Out", "Connection error while performing next scan", e.Message);
                                        result = false;
                                    }
                                    catch (Exception e) {
                                        await LogExceptionHelper.ShowMessageAndPrintToLogs("Unexpected Error", "Error performing next scan.", e);
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
                                        await LogExceptionHelper.ShowMessageAndPrintToLogs("Unexpected Error", "Unexpected error unfreezing console.", e);
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

                            try {
                                result = true;
                                token = await context.PerformFirstScan(connection, token);
                            }
                            catch (OperationCanceledException) {
                                Debugger.Break(); // OCE should not be thrown
                            }
                            catch (Exception e) when (e is IOException || e is TimeoutException) {
                                await IMessageDialogService.Instance.ShowMessage(e is IOException ? "Connection IO Error" : "Connection Timed Out", "Connection error while performing first scan", e.Message);
                                result = false;
                            }
                            catch (Exception e) {
                                await LogExceptionHelper.ShowMessageAndPrintToLogs("Unexpected Error", "Error performing first scan.", e);
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
                    if (result && !this.MemoryEngine.IsShuttingDown && !thisTask.IsCancellationRequested) {
                        progress.Text = "Updating result list...";
                        int count = this.resultBuffer.Count;
                        const int chunkSize = 500;
                        int range = count / chunkSize;
                        using PopCompletionStateRangeToken x = progress.CompletionState.PushCompletionRange(0.0, 1.0 / range);
                        updateListTask = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(async () => {
                            while (!this.resultBuffer.IsEmpty) {
                                List<ScanResultViewModel> list = new List<ScanResultViewModel>();
                                for (int i = 0; i < chunkSize && this.resultBuffer.TryDequeue(out ScanResultViewModel? scanResult); i++)
                                    list.Add(scanResult);

                                this.ScanResults.AddRange(list);

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
                        if (this.MemoryEngine.BusyLocker.IsTokenValid(token)) {
                            this.MemoryEngine.CheckConnection(token);
                        }
                        else {
                            this.MemoryEngine.CheckConnection();
                        }
                    }
                }, token: CancellationToken.None);
            }, progress, cts);

            try {
                await this.ScanningActivity;
            }
            finally {
                this.ScanningActivity = null;
            }
        }
        finally {
            token?.Dispose();
        }
    }

    public void ResetScan() {
        if (this.isScanning)
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

        using IDisposable? token = this.MemoryEngine.TryBeginBusyOperation();
        if (token == null) {
            return; // do not read while connection busy
        }

        await this.RefreshSavedAddressesAsync(token, bypassLimits, invalidateCaches);
    }

    /// <summary>
    /// Refreshes the saved address list
    /// </summary>
    /// <param name="busyOperationToken">The busy operation token. Does not dispose once finished</param>
    /// <exception cref="InvalidOperationException">No connection is present</exception>
    public async Task RefreshSavedAddressesAsync(IDisposable busyOperationToken, bool bypassLimits = false, bool invalidateCaches = false) {
        this.MemoryEngine.BusyLocker.ValidateToken(busyOperationToken);
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

                if (savedList != null) {
                    IDataValue?[] values = new IDataValue?[savedList.Count];
                    await Task.Run(async () => {
                        for (int i = 0; i < values.Length; i++) {
                            token.ThrowIfCancellationRequested();
                            AddressTableEntry item = savedList[i];
                            if (item.IsAutoRefreshEnabled) { // may change between dispatcher callbacks
                                uint? addr = await item.MemoryAddress.TryResolveAddress(connection, invalidateCaches);
                                values[i] = addr.HasValue ? await MemoryEngine.ReadDataValue(connection, addr.Value, item.DataType, item.StringType, item.StringLength, item.ArrayLength) : null;
                            }
                        }
                    }, token);

                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                        // Only <=100 values to update, so not too UI intensive
                        for (int i = 0; i < values.Length; i++) {
                            AddressTableEntry address = savedList[i];
                            if (address.IsAutoRefreshEnabled) // may change between dispatcher callbacks
                                address.Value = values[i];
                        }
                    }, token: CancellationToken.None);
                }

                // safety net -- we still need to implement logic to notify view models when they're visible in the
                // UI, although this does kind of break the MVVM pattern but oh well
                if (list != null) {
                    IDataValue[] values = new IDataValue[list.Count];
                    await Task.Run(async () => {
                        for (int i = 0; i < values.Length; i++) {
                            token.ThrowIfCancellationRequested();
                            ScanResultViewModel item = list[i];
                            values[i] = await MemoryEngine.ReadDataValue(connection, item.Address, item.DataType, item.StringType, item.CurrentStringLength, item.CurrentArrayLength);
                        }
                    }, token);

                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                        // Only <=100 values to update, so not too UI intensive
                        for (int i = 0; i < list.Count; i++) {
                            list[i].CurrentValue = values[i];
                        }
                    }, token: CancellationToken.None);
                }
            }, CancellationToken.None);

            await Task.WhenAny(readOperationTask, Task.Delay(500, token));

            if (!readOperationTask.IsCompleted) {
                await ActivityManager.Instance.RunTask(async () => {
                    IActivityProgress p = ActivityManager.Instance.GetCurrentProgressOrEmpty();
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

    private bool canSearchForFloat = true;
    private bool canSearchForDouble = true;
    private bool canSearchForString = true;
    private bool canRunNextScanForByteArray = true;

    public bool CanSearchForByte => this.Orders[0].IsEnabled;

    public bool CanSearchForShort => this.Orders[1].IsEnabled;

    public bool CanSearchForInt => this.Orders[2].IsEnabled;

    public bool CanSearchForLong => this.Orders[3].IsEnabled;

    public bool CanSearchForFloat {
        get => this.canSearchForFloat;
        set => PropertyHelper.SetAndRaiseINE(ref this.canSearchForFloat, value, this, static t => t.CanSearchForFloatChanged?.Invoke(t));
    }

    public bool CanSearchForDouble {
        get => this.canSearchForDouble;
        set => PropertyHelper.SetAndRaiseINE(ref this.canSearchForDouble, value, this, static t => t.CanSearchForDoubleChanged?.Invoke(t));
    }

    public bool CanSearchForString {
        get => this.canSearchForString;
        set => PropertyHelper.SetAndRaiseINE(ref this.canSearchForString, value, this, static t => t.CanSearchForStringChanged?.Invoke(t));
    }

    public bool CanRunNextScanForByteArray {
        get => this.canRunNextScanForByteArray;
        set => PropertyHelper.SetAndRaiseINE(ref this.canRunNextScanForByteArray, value, this, static t => t.CanRunNextScanForByteArrayChanged?.Invoke(t));
    }

    public event UnknownDataTypeOptionsEventHandler? CanSearchForFloatChanged;
    public event UnknownDataTypeOptionsEventHandler? CanSearchForDoubleChanged;
    public event UnknownDataTypeOptionsEventHandler? CanSearchForStringChanged;
    public event UnknownDataTypeOptionsEventHandler? CanRunNextScanForByteArrayChanged;

    public UnknownDataTypeOptions() {
        this.Orders = new ObservableList<ScanningOrderModel>() {
            new ScanningOrderModel(DataType.Int32),
            new ScanningOrderModel(DataType.Int16),
            new ScanningOrderModel(DataType.Byte),
            new ScanningOrderModel(DataType.Int64),
        };

        this.Orders.BeforeItemAdded += (list, index, item) => throw new InvalidOperationException("Items cannot be added to this list");
        this.Orders.BeforeItemsRemoved += (list, index, count) => throw new InvalidOperationException("Items cannot be removed from this list");
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