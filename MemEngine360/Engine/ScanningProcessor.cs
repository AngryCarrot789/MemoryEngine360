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
using PFXToolKitUI.Utils.Collections.Observable;
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
    private bool useFirstValue, usePreviousValue;
    private FloatScanOption floatScanOption;
    private StringType stringScanOption;
    private DataType dataType;
    private NumericScanType numericScanType;
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
        get => this.useFirstValue;
        set {
            if (this.useFirstValue != value) {
                this.useFirstValue = value;
                this.UseFirstValueForNextScanChanged?.Invoke(this);
                if (value)
                    this.UsePreviousValueForNextScan = false;
            }
        }
    }

    public bool UsePreviousValueForNextScan {
        get => this.usePreviousValue;
        set {
            if (this.usePreviousValue != value) {
                this.usePreviousValue = value;
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

    public bool IsRefreshingAddresses {
        get => this.isRefreshingAddresses;
        private set {
            if (this.isRefreshingAddresses == value)
                return;

            this.isRefreshingAddresses = value;
            this.IsRefreshingAddressesChanged?.Invoke(this);
        }
    }

    public bool CanPerformFirstScan => !this.IsScanning && !this.HasDoneFirstScan && this.MemoryEngine360.Connection != null;
    public bool CanPerformNextScan => !this.IsScanning && this.HasDoneFirstScan && this.MemoryEngine360.Connection != null;
    public bool CanPerformReset => !this.IsScanning && this.HasDoneFirstScan;
    public bool IsSecondInputRequired => this.numericScanType == NumericScanType.Between && this.DataType.IsNumeric();

    public ObservableCollection<ScanResultViewModel> ScanResults { get; } = new ObservableCollection<ScanResultViewModel>();
    public ObservableCollection<SavedAddressViewModel> SavedAddresses { get; } = new ObservableCollection<SavedAddressViewModel>();

    public ObservableList<ScanResultViewModel> SelectedResults { get; } = new ObservableList<ScanResultViewModel>();

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
    public event ScanningProcessorEventHandler? AlignmentChanged;
    public event ScanningProcessorEventHandler? ScanMemoryPagesChanged;
    public event ScanningProcessorEventHandler? IsRefreshingAddressesChanged;
    public event ScanningProcessorEventHandler? ScanCompleted;

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

        this.resultBuffer = new ConcurrentQueue<ScanResultViewModel>();

        this.ScanResults.CollectionChanged += (sender, args) => {
            ApplicationPFX.Instance.Dispatcher.VerifyAccess();
        };

        // Adds up to 100 items per second
        this.rldaMoveBufferIntoResultList = RateLimitedDispatchActionBase.ForDispatcherSync(() => {
            for (int i = 0; i < 20 && this.resultBuffer.TryDequeue(out ScanResultViewModel? result); i++) {
                this.ScanResults.Add(result);
            }
        }, TimeSpan.FromMilliseconds(200));

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

        using IDisposable? token = await this.MemoryEngine360.BeginBusyOperationActivityAsync("Scan Operation");
        if (token == null)
            return; // user cancelled token fetch

        if ((connection = this.MemoryEngine360.Connection) == null)
            return; // rare disconnection before token acquired

        if (this.MemoryEngine360.IsShuttingDown)
            return; // program shutting down before token acquired

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
                    result = await this.ScanNextInternal(progress);
                }
                catch {
                    Debugger.Break();
                    result = false;
                }

                if (result && !this.MemoryEngine360.IsShuttingDown) {
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
                else {
                    result = false;
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

    private async Task<bool> ScanNextInternal(IActivityProgress activity) {
        if (string.IsNullOrEmpty(this.InputA)) {
            await IMessageDialogService.Instance.ShowMessage("Input format", this.IsSecondInputRequired ? "'From' input is empty" : "Input is empty");
            return false;
        }

        if (this.IsSecondInputRequired && string.IsNullOrEmpty(this.InputB)) {
            await IMessageDialogService.Instance.ShowMessage("Input format", "'To' input is empty");
            return false;
        }

        if (!IValueScanner.Scanners.TryGetValue(this.DataType, out IValueScanner? scanner)) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Application error: invalid data type");
            return false;
        }

        ObservableList<ScanResultViewModel> dstList = new ObservableList<ScanResultViewModel>();
        ObservableItemProcessor.MakeIndexable(dstList,
            (sender, index, item) => {
                if (index != (dstList.Count - 1))
                    throw new InvalidOperationException("Must use Add, not Insert");
                this.resultBuffer.Enqueue(item);
                this.rldaMoveBufferIntoResultList.InvokeAsync();
            },
            (sender, index, item) => throw new InvalidOperationException("Cannot remove from the results list"),
            (sender, oldIdx, newIdx, item) => throw new InvalidOperationException("Cannot move items in the results list"));

        bool result;
        if (this.hasDoneFirstScan) {
            activity.Text = "Accumulating scan results...";
            List<ScanResultViewModel> srcList = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                List<ScanResultViewModel> items = this.ScanResults.ToList();
                this.ScanResults.Clear();
                items.AddRange(this.resultBuffer);
                this.resultBuffer.Clear();
                return items;
            });

            activity.Text = "Scanning...";
            try {
                result = await Task.Run(() => scanner.PerformNextScan(this, srcList, dstList, activity));
            }
            catch (OperationCanceledException) {
                result = true;
            }
            catch (Exception e) {
                await IMessageDialogService.Instance.ShowMessage("Error", "Error while performing next scan", e.ToString());
                result = false;
            }
        }
        else {
            activity.Text = "Scanning...";
            try {
                result = await Task.Run(() => scanner.PerformFirstScan(this, dstList, activity));
            }
            catch (OperationCanceledException) {
                result = true;
            }
            catch (Exception e) {
                await IMessageDialogService.Instance.ShowMessage("Error", "Error while performing first scan", e.ToString());
                result = false;
            }
        }

        this.rldaMoveBufferIntoResultList.InvokeAsync();
        return result;
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
                    address.Value = await MemoryEngine360.ReadAsText(connection, address.Address, address.DataType,
                        address.DisplayAsHex
                            ? NumericDisplayType.Hexadecimal
                            : (address.DisplayAsUnsigned
                                ? NumericDisplayType.Unsigned
                                : NumericDisplayType.Normal),
                        address.StringLength);
                }
            }

            // safety net -- we still need to implement logic to notify view models when they're visible in the
            // UI, although this does kind of break the MVVM pattern but oh well
            if (this.ScanResults.Count < 100) {
                this.IsRefreshingAddresses = true;
                
                // Lazily prevents concurrent modification
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