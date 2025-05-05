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

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.Scanners;
using PFXToolKitUI;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.Engine;

public delegate void ScanningProcessorEventHandler(ScanningProcessor sender);

public delegate void ScanningProcessorAddressChangedEventHandler(ScanningProcessor sender, uint oldValue, uint newValue);

public class ScanningProcessor {
    private string inputA, inputB;
    private bool hasDoneFirstScan, isScanning;
    private uint startAddress, scanLength;
    private uint alignment;
    private bool pauseConsoleDuringScan, scanMemoryPages, isIntInputHexadecimal;
    private bool nextScanUsesFirstValue, nextScanUsesPreviousValue;
    private FloatScanOption floatScanOption;
    private StringType stringScanOption;
    private DataType dataType;
    private NumericScanType numericScanType;
    private bool stringIgnoreCase;
    private bool isRefreshingAddresses;


    public ActivityTask? ScanningActivity { get; private set; }

    /// <summary>
    /// Gets the primary input
    /// </summary>
    public string InputA {
        get => this.inputA;
        set {
            value ??= "";
            if (this.inputA != value) {
                this.inputA = value;
                this.InputAChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets the secondary input, used when <see cref="NumericScanType"/> is <see cref="Modes.NumericScanType.Between"/>
    /// </summary>
    public string InputB {
        get => this.inputB;
        set {
            value ??= "";
            if (this.inputB != value) {
                this.inputB = value;
                this.InputBChanged?.Invoke(this);
            }
        }
    }

    public bool HasDoneFirstScan {
        get => this.hasDoneFirstScan;
        private set {
            if (this.hasDoneFirstScan != value) {
                this.hasDoneFirstScan = value;
                this.HasFirstScanChanged?.Invoke(this);
            }
        }
    }

    public bool IsScanning {
        get => this.isScanning;
        private set {
            if (this.isScanning != value) {
                this.isScanning = value;
                this.IsScanningChanged?.Invoke(this);
            }
        }
    }

    public uint StartAddress {
        get => this.startAddress;
        set {
            uint oldValue = this.startAddress;
            if (oldValue != value) {
                this.startAddress = value;
                this.StartAddressChanged?.Invoke(this, oldValue, value);
                BasicApplicationConfiguration.Instance.StartAddress = value;
            }
        }
    }

    public uint ScanLength {
        get => this.scanLength;
        set {
            uint oldValue = this.scanLength;
            if (oldValue != value) {
                this.scanLength = value;
                this.ScanLengthChanged?.Invoke(this, oldValue, value);
                BasicApplicationConfiguration.Instance.ScanLength = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the alignment value. This is continually added to the address when scanning.
    /// This is necessary when scanning data types bigger than 1 byte, because you don't want to
    /// scan for an int32 halfway through another int32 value or whatever
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Alignment cannot be 0</exception>
    public uint Alignment {
        get => this.alignment;
        set {
            if (this.alignment != value) {
                if (value == 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Alignment cannot be zero");

                this.alignment = value;
                this.AlignmentChanged?.Invoke(this);
            }
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
            if (this.scanMemoryPages == value)
                return;

            this.scanMemoryPages = value;
            this.ScanMemoryPagesChanged?.Invoke(this);
            BasicApplicationConfiguration.Instance.ScanMemoryPages = value;
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
                this.Alignment = GetAlignmentFromDataType(this.dataType);
            }
        }
    }

    public NumericScanType NumericScanType {
        get => this.numericScanType;
        set {
            if (this.numericScanType == value)
                return;

            this.numericScanType = value;
            this.NumericScanTypeChanged?.Invoke(this);
        }
    }
    
    /// <summary>
    /// Gets or sets if strings should be searched as case-insensitive
    /// </summary>
    public bool StringIgnoreCase {
        get => this.stringIgnoreCase;
        set {
            if (this.stringIgnoreCase == value)
                return;

            this.stringIgnoreCase = value;
            this.StringIgnoreCaseChanged?.Invoke(this);
            BasicApplicationConfiguration.Instance.DTString_IgnoreCase = value;
        }
    }

    /// <summary>
    /// Returns true when we are currently in the process of refreshing the scan results and saved addresses
    /// </summary>
    public bool IsRefreshingAddresses {
        get => this.isRefreshingAddresses;
        private set {
            if (this.isRefreshingAddresses == value)
                return;

            this.isRefreshingAddresses = value;
            this.IsRefreshingAddressesChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Returns <see cref="System.StringComparison.CurrentCulture"/> when <see cref="StringIgnoreCase"/> is false,
    /// and <see cref="System.StringComparison.CurrentCultureIgnoreCase"/> when true
    /// </summary>
    public StringComparison StringComparison => this.stringIgnoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
    
    public bool CanPerformFirstScan => !this.IsScanning && !this.HasDoneFirstScan && this.MemoryEngine360.Connection != null;
    public bool CanPerformNextScan => !this.IsScanning && this.HasDoneFirstScan && this.MemoryEngine360.Connection != null;
    public bool CanPerformReset => !this.IsScanning && this.HasDoneFirstScan;

    /// <summary>
    /// Gets the visible scan results in the UI
    /// </summary>
    public ObservableCollection<ScanResultViewModel> ScanResults { get; } = new ObservableCollection<ScanResultViewModel>();

    /// <summary>
    /// Gets the saved addresses
    /// </summary>
    public ObservableCollection<SavedAddressViewModel> SavedAddresses { get; } = new ObservableCollection<SavedAddressViewModel>();

    /// <summary>
    /// Returns the actual amount of results (visible and hidden). The user may choose to cancel the
    /// "Updating result list..." operation and instead leave the remaining results as hidden.
    /// They will still be processed, just not visible
    /// </summary>
    public int ActualScanResultCount => this.ScanResults.Count + this.resultBuffer.Count;


    public MemoryEngine360 MemoryEngine360 { get; }


    public event ScanningProcessorEventHandler? InputAChanged;
    public event ScanningProcessorEventHandler? InputBChanged;
    public event ScanningProcessorEventHandler? HasFirstScanChanged;
    public event ScanningProcessorEventHandler? IsScanningChanged;
    public event ScanningProcessorAddressChangedEventHandler? StartAddressChanged;
    public event ScanningProcessorAddressChangedEventHandler? ScanLengthChanged;
    public event ScanningProcessorEventHandler? PauseConsoleDuringScanChanged;
    public event ScanningProcessorEventHandler? IsIntInputHexadecimalChanged;
    public event ScanningProcessorEventHandler? UseFirstValueForNextScanChanged;
    public event ScanningProcessorEventHandler? UsePreviousValueForNextScanChanged;
    public event ScanningProcessorEventHandler? FloatScanModeChanged;
    public event ScanningProcessorEventHandler? StringScanModeChanged;
    public event ScanningProcessorEventHandler? DataTypeChanged;
    public event ScanningProcessorEventHandler? NumericScanTypeChanged;
    public event ScanningProcessorEventHandler? StringIgnoreCaseChanged;
    public event ScanningProcessorEventHandler? AlignmentChanged;
    public event ScanningProcessorEventHandler? ScanMemoryPagesChanged;
    public event ScanningProcessorEventHandler? IsRefreshingAddressesChanged;

    private readonly ConcurrentQueue<ScanResultViewModel> resultBuffer;
    private readonly RateLimitedDispatchAction rldaMoveBufferIntoResultList;
    private readonly RateLimitedDispatchAction rldaRefreshSavedAddressList;

    public ScanningProcessor(MemoryEngine360 memoryEngine360) {
        this.MemoryEngine360 = memoryEngine360;
        BasicApplicationConfiguration cfg = BasicApplicationConfiguration.Instance;

        this.inputA = "";
        this.dataType = DataType.Int32;
        this.numericScanType = NumericScanType.Equals;
        this.startAddress = cfg.StartAddress;
        this.scanLength = cfg.ScanLength;
        this.alignment = GetAlignmentFromDataType(this.dataType);
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

        // Adds up to 100 items per second
        this.rldaMoveBufferIntoResultList = RateLimitedDispatchActionBase.ForDispatcherSync(() => {
            for (int i = 0; i < 10 && this.resultBuffer.TryDequeue(out ScanResultViewModel? result); i++) {
                this.ScanResults.Add(result);
            }
        }, TimeSpan.FromMilliseconds(100));

        this.rldaRefreshSavedAddressList = RateLimitedDispatchActionBase.ForDispatcherAsync(this.RefreshSavedAddressesAsync, TimeSpan.FromMilliseconds(100));
    }

    public static uint GetAlignmentFromDataType(DataType type) {
        switch (type) {
            case DataType.Byte:   return 1u;
            case DataType.Int16:  return 2u;
            case DataType.Int32:  return 4u;
            case DataType.Int64:  return 8u;
            case DataType.Float:  return 4u;
            case DataType.Double: return 8u;
            case DataType.String: return 1u; // scan for the entire string for each next char
            default:              throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public async Task ScanFirstOrNext() {
        ApplicationPFX.Instance.Dispatcher.VerifyAccess();

        if (this.isScanning)
            throw new InvalidOperationException("Currently scanning");

        IConsoleConnection? connection = this.MemoryEngine360.Connection;
        if (connection == null)
            throw new InvalidOperationException("No console connection");

        using IDisposable? token = await this.MemoryEngine360.BeginBusyOperationActivityAsync("Pre-Scan Setup");
        if (token == null)
            return; // user cancelled token fetch

        if ((connection = this.MemoryEngine360.Connection) == null)
            return; // rare disconnection before token acquired

        if (this.MemoryEngine360.IsShuttingDown)
            return; // program shutting down before token acquired

        DataType scanningDataType = this.DataType;
        ScanningContext context = new ScanningContext(this);
        if (!await context.Setup()) {
            return;
        }

        byte[] bytes = await connection.ReadBytes(0x80000000, 1000);

        // should be impossible since we obtain the busy token which is required before scanning
        
        Debug.Assert(this.isScanning == false, "WTF");

        bool debugFreeze = this.PauseConsoleDuringScan;
        DefaultProgressTracker progress = new DefaultProgressTracker {
            Caption = "Scan", Text = "Beginning scan..."
        };

        using CancellationTokenSource cts = new CancellationTokenSource();
        this.ScanningActivity = ActivityManager.Instance.RunTask(async () => {
            ActivityTask thisTask = ActivityManager.Instance.CurrentTask;
            await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(async () => {
                // If for some reason it gets force disconnected in an already scheduled
                // dispatcher operation, we should just safely stop scanning
                if (!connection.IsConnected || this.MemoryEngine360.IsShuttingDown) {
                    return;
                }

                this.IsScanning = true;
                if (debugFreeze && connection is IHaveIceCubes)
                    await ((IHaveIceCubes) connection).DebugFreeze();
            });

            bool result = false;
            if (connection.IsConnected) {
                thisTask.CancellationToken.ThrowIfCancellationRequested();
                try {
                    context.ResultFound += (sender, model) => {
                        this.resultBuffer.Enqueue(model);
                        this.rldaMoveBufferIntoResultList.InvokeAsync();
                    };

                    if (this.hasDoneFirstScan) {
                        progress.Text = "Accumulating scan results...";
                        List<ScanResultViewModel> srcList = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                            List<ScanResultViewModel> items = this.ScanResults.ToList();
                            this.ScanResults.Clear();
                            return items;
                        });

                        srcList.AddRange(this.resultBuffer);
                        this.resultBuffer.Clear();

                        bool hasDifferentDataTypes = false;
                        DataType firstDataType = this.dataType;
                        if (srcList.Count > 0) {
                            firstDataType = srcList[0].DataType;
                            for (int i = 1; i < srcList.Count; i++) {
                                if (srcList[i].DataType != firstDataType) {
                                    hasDifferentDataTypes = true;
                                    break;
                                }
                            }
                        }

                        if (hasDifferentDataTypes) {
                            await IMessageDialogService.Instance.ShowMessage("Error", "Result list contains results with different data types. This is a weird bug. Cannot continue search");
                            result = false;
                        }
                        else if (firstDataType != scanningDataType) {
                            await IMessageDialogService.Instance.ShowMessage("Error", $"Search data type is different to the search results. You're searching for {scanningDataType}, but the results contain {firstDataType}");
                            result = false;
                        }
                        else {
                            progress.Text = "Scanning...";

                            try {
                                result = true;
                                await context.PerformNextScan(connection, srcList);
                            }
                            catch (OperationCanceledException) {
                                // ignored
                            }
                            catch (Exception e) {
                                await IMessageDialogService.Instance.ShowMessage("Error", "Error while performing next scan", e.ToString());
                                result = false;
                            }
                        }
                    }
                    else {
                        progress.Text = "Scanning...";

                        try {
                            result = true;
                            await context.PerformFirstScan(connection);
                        }
                        catch (OperationCanceledException) {
                            // ignored
                        }
                        catch (Exception e) {
                            await IMessageDialogService.Instance.ShowMessage("Error", "Error while performing first scan", e.ToString());
                            result = false;
                        }
                    }

                    this.rldaMoveBufferIntoResultList.InvokeAsync();
                }
                catch {
                    Debugger.Break();
                    result = false;
                }

                if (result && !this.MemoryEngine360.IsShuttingDown && !thisTask.IsCancellationRequested) {
                    progress.Text = "Updating result list...";
                    int count = this.resultBuffer.Count;
                    const int chunkSize = 500;
                    int range = count / chunkSize;
                    using PopCompletionStateRangeToken x = progress.CompletionState.PushCompletionRange(0.0, 1.0 / range);
                    await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(async () => {
                        while (!this.resultBuffer.IsEmpty) {
                            for (int i = 0; i < chunkSize && this.resultBuffer.TryDequeue(out ScanResultViewModel? scanResult); i++) {
                                this.ScanResults.Add(scanResult);
                            }

                            progress.CompletionState.OnProgress(1.0);
                            try {
                                await Task.Delay(50, thisTask.CancellationToken);
                            }
                            catch (OperationCanceledException) {
                                return;
                            }
                        }
                    });
                }
            }

            await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(async () => {
                this.HasDoneFirstScan = result;
                this.IsScanning = false;
                if (debugFreeze && connection is IHaveIceCubes && connection.IsConnected) // race condition ^-^ so rare don't care
                    await ((IHaveIceCubes) connection).DebugUnFreeze();

                if (!this.MemoryEngine360.IsShuttingDown) // another race condition i suppose
                    this.MemoryEngine360.CheckConnection();
            });
        }, progress, cts);

        try {
            await this.ScanningActivity;
        }
        finally {
            this.ScanningActivity = null;
        }
    }

    public void ResetScan() {
        if (this.isScanning)
            throw new InvalidOperationException("Currently scanning");

        this.resultBuffer.Clear();
        this.ScanResults.Clear();
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

    public async Task RefreshSavedAddressesAsync() {
        if (this.IsScanning || this.IsRefreshingAddresses || this.MemoryEngine360.IsConnectionBusy || this.MemoryEngine360.Connection == null) {
            return; // concurrent operations are dangerous and can corrupt the communication pipe until restarting connection
        }

        using IDisposable? token = this.MemoryEngine360.BeginBusyOperation();
        if (token == null) {
            return; // do not read while connection busy
        }

        await this.RefreshSavedAddressesAsync(token);
    }

    /// <summary>
    /// Refreshes the saved address list
    /// </summary>
    /// <param name="busyOperationToken">The busy operation token. Does not dispose once finished</param>
    /// <exception cref="InvalidOperationException">No connection is present</exception>
    public async Task RefreshSavedAddressesAsync(IDisposable busyOperationToken) {
        Validate.NotNull(busyOperationToken);
        if (this.IsRefreshingAddresses) {
            throw new InvalidOperationException("Already refreshing");
        }

        IConsoleConnection connection = this.MemoryEngine360.Connection ?? throw new InvalidOperationException("No connection present");

        // ideally this shouldn't throw at all
        try {
            // TODO: maybe batch together results whose addresses are close by, and read a single chunk?
            // May be faster if the console is not debug frozen and we have to update 100s of results...
            if (this.SavedAddresses.Count < 100) {
                this.IsRefreshingAddresses = true;

                foreach (SavedAddressViewModel address in this.SavedAddresses) {
                    address.Value = await MemoryEngine360.ReadAsText(connection, address.Address, address.DataType, address.NumericDisplayType, address.StringLength);
                }
            }

            // safety net -- we still need to implement logic to notify view models when they're visible in the
            // UI, although this does kind of break the MVVM pattern but oh well
            if (this.ScanResults.Count < 100) {
                this.IsRefreshingAddresses = true;

                // Lazily prevents concurrent modification due to awaiting read text
                List<ScanResultViewModel> list = this.ScanResults.ToList();
                foreach (ScanResultViewModel result in list) {
                    result.CurrentValue = await MemoryEngine360.ReadAsText(connection, result.Address, result.DataType, result.NumericDisplayType, (uint) result.FirstValue.Length);
                }
            }
        }
        finally {
            this.IsRefreshingAddresses = false;
        }
    }
}