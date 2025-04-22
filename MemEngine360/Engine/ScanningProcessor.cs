using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.Scanners;
using PFXToolKitUI;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.RDA;

namespace MemEngine360.Engine;

public delegate void ScanningProcessorEventHandler(ScanningProcessor sender);

public delegate void ScanningProcessorAddressChangedEventHandler(ScanningProcessor sender, uint oldValue, uint newValue);

public class ScanningProcessor {
    private string inputA, inputB;
    private bool hasDoneFirstScan;
    private bool isScanning;
    private uint startAddress, scanLength;
    private bool pauseConsoleDuringScan;
    private bool isIntInputHexadecimal;
    private FloatScanOption floatScanOption;
    private StringScanOption stringScanOption;
    private DataType dataType;
    private NumericScanType numericScanType;

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

    public StringScanOption StringScanOption {
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
    
    public bool CanPerformFirstScan => !this.IsScanning && !this.HasDoneFirstScan && this.MemoryEngine360.Connection != null && !this.MemoryEngine360.IsConnectionBusy;
    public bool CanPerformNextScan => !this.IsScanning && this.HasDoneFirstScan && this.MemoryEngine360.Connection != null && !this.MemoryEngine360.IsConnectionBusy;
    public bool CanPerformReset => !this.IsScanning && this.HasDoneFirstScan;
    public bool IsSecondInputRequired => this.numericScanType == NumericScanType.Between && this.DataType.IsNumeric();

    public ObservableCollection<ScanResultViewModel> ScanResults { get; } = new ObservableCollection<ScanResultViewModel>();
    public ObservableCollection<SavedAddressViewModel> SavedAddresses { get; } = new ObservableCollection<SavedAddressViewModel>();
    
    public ObservableList<ScanResultViewModel> SelectedResults { get; } = new ObservableList<ScanResultViewModel>();
    
    public MemoryEngine360 MemoryEngine360 { get; }

    public event ScanningProcessorEventHandler? InputAChanged;
    public event ScanningProcessorEventHandler? InputBChanged;
    public event ScanningProcessorEventHandler? HasFirstScanChanged;
    public event ScanningProcessorEventHandler? IsScanningChanged;
    public event ScanningProcessorAddressChangedEventHandler? StartAddressChanged;
    public event ScanningProcessorAddressChangedEventHandler? ScanLengthChanged;
    public event ScanningProcessorEventHandler? PauseConsoleDuringScanChanged;
    public event ScanningProcessorEventHandler? IsIntInputHexadecimalChanged;
    public event ScanningProcessorEventHandler? FloatScanModeChanged;
    public event ScanningProcessorEventHandler? StringScanModeChanged;
    public event ScanningProcessorEventHandler? DataTypeChanged;
    public event ScanningProcessorEventHandler? NumericScanTypeChanged;

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
        this.pauseConsoleDuringScan = cfg.PauseConsoleDuringScan;
        this.isIntInputHexadecimal = cfg.DTInt_UseHexValue;
        this.floatScanOption = cfg.DTFloat_Mode;
        this.stringScanOption = cfg.DTString_Mode;

        this.resultBuffer = new ConcurrentQueue<ScanResultViewModel>();

        // Adds up to 100 items per second
        this.rldaMoveBufferIntoResultList = RateLimitedDispatchActionBase.ForDispatcherSync(() => {
            for (int i = 0; i < 20 && this.resultBuffer.TryDequeue(out ScanResultViewModel? result); i++) {
                this.ScanResults.Add(result);
            }
        }, TimeSpan.FromMilliseconds(200));
        
        this.rldaRefreshSavedAddressList = RateLimitedDispatchActionBase.ForDispatcherAsync(this.RefreshSavedAddressesAsync, TimeSpan.FromMilliseconds(200));
    }

    public async Task ScanNext() {
        if (this.isScanning)
            throw new InvalidOperationException("Currently scanning");
        
        using IDisposable? token = this.MemoryEngine360.BeginBusyOperation();
        if (token == null)
            throw new InvalidOperationException("Engine is currently busy. Cannot scan");

        this.IsScanning = true;
        DefaultProgressTracker progress = new DefaultProgressTracker {
            Caption = "Scan", Text = "Beginning scan"
        };

        CancellationTokenSource cts = new CancellationTokenSource();
        bool success = await ActivityManager.Instance.RunTask(async () => {
            bool success = await this.ScanNextInternal(progress);
            if (!success && !this.MemoryEngine360.Connection!.IsReallyConnected) {
                this.resultBuffer.Clear();
                this.ScanResults.Clear();
                
                this.MemoryEngine360.Connection.Dispose();
                this.MemoryEngine360.Connection = null;
                return false;
            }

            progress.Text = "Updating result list...";
            int count = this.resultBuffer.Count;
            const int chunkSize = 200;
            int range = count / chunkSize;
            using PopCompletionStateRangeToken x = progress.CompletionState.PushCompletionRange(0.0, 1.0 / range);
            await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(async () => {
                while (!this.resultBuffer.IsEmpty) {
                    for (int i = 0; i < chunkSize && this.resultBuffer.TryDequeue(out ScanResultViewModel? result); i++) {
                        this.ScanResults.Add(result);
                    }

                    progress.CompletionState.OnProgress(1.0);
                    try {
                        await Task.Delay(50, cts.Token);
                    }
                    catch (OperationCanceledException) {
                        this.resultBuffer.Clear();
                        return success;
                    }
                }

                return success;
            });

            return success;
        }, progress, cts);

        cts.Dispose();
        this.HasDoneFirstScan = success;
        this.IsScanning = false;
    }

    public void ResetScan() {
        if (this.isScanning)
            throw new InvalidOperationException("Currently scanning");

        this.ScanResults.Clear();
        this.HasDoneFirstScan = false;
    }

    private async Task<bool> ScanNextInternal(IActivityProgress activity) {
        if (string.IsNullOrEmpty(this.InputA)) {
            await IMessageDialogService.Instance.ShowMessage("Input format", this.IsSecondInputRequired ? "'From' input is empty" :"Input is empty");
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

        ObservableList<ScanResultViewModel> list = new ObservableList<ScanResultViewModel>();
        ObservableItemProcessor.MakeIndexable(list,
            (sender, index, item) => {
                if (index != (list.Count - 1))
                    throw new InvalidOperationException("Must use Add, not Insert");
                this.resultBuffer.Enqueue(item);
                this.rldaMoveBufferIntoResultList.InvokeAsync();
            },
            (sender, index, item) => throw new InvalidOperationException("Cannot remove from the results list"),
            (sender, oldIdx, newIdx, item) => throw new InvalidOperationException("Cannot move items in the results list"));

        bool result;
        try {
            result = await Task.Run(() => scanner.Scan(this, list, activity));
        }
        catch (OperationCanceledException) {
            result = true;
        }
        catch (Exception e) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Error while scanning", e.ToString());
            result = false;
        }

        this.rldaMoveBufferIntoResultList.InvokeAsync();
        return result;
    }

    /// <summary>
    /// Signals to the updater to refresh the current value of saved addresses
    /// </summary>
    public void RefreshSavedAddresses() {
        this.rldaRefreshSavedAddressList.InvokeAsync();
    }
    
    private async Task RefreshSavedAddressesAsync() {
        IConsoleConnection? connection;
        if (this.IsScanning || (connection = this.MemoryEngine360.Connection) == null || connection.IsBusy) {
            return; // concurrent operations are dangerous and can corrupt the communication pipe until restarting connection
        }

        using IDisposable? token = this.MemoryEngine360.BeginBusyOperation();
        if (token == null) {
            return; // do not modify connection while busy
        }
        
        foreach (SavedAddressViewModel address in this.SavedAddresses) {
            address.Value = await MemoryEngine360.ReadAsText(connection, address.Address, address.DataType,
                address.DisplayAsHex
                    ? MemoryEngine360.NumericDisplayType.Hexadecimal
                    : (address.DisplayAsUnsigned
                        ? MemoryEngine360.NumericDisplayType.Unsigned
                        : MemoryEngine360.NumericDisplayType.Normal),
                (uint) address.StringLength);
        }
    }
}