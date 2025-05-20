namespace MemEngine360.PointerScanning;

public delegate void PointerScannerEventHandler(PointerScanner sender);

public class PointerScanner {
    private uint addressableBase;
    private uint addressableLength;
    private uint targetAddress;
    private uint maxDepth;

    /// <summary>
    /// Gets the base address of the addressable memory space, as in, the smallest address a pointer can be
    /// </summary>
    public uint AddressableBase {
        get => this.addressableBase;
        set {
            if (this.addressableBase != value) {
                this.addressableBase = value;
                this.AddressableBaseChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets the number of bytes (relative to <see cref="AddressableBase"/>) that can be scanned as a potential pointer
    /// </summary>
    public uint AddressableLength {
        get => this.addressableLength;
        set {
            if (this.addressableLength != value) {
                this.addressableLength = value;
                this.AddressableLengthChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets the maximum depth a pointer can be, as in, the max amount of offsets there can be to reach <see cref="TargetAddress"/>
    /// </summary>
    public uint MaxDepth {
        get => this.maxDepth;
        set {
            if (this.maxDepth != value) {
                this.maxDepth = value;
                this.MaxDepthChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets the actual address we want to scan for, e.g. the memory address of an ammo count
    /// </summary>
    public uint TargetAddress {
        get => this.targetAddress;
        set {
            if (this.targetAddress != value) {
                this.targetAddress = value;
                this.TargetAddressChanged?.Invoke(this);
            }
        }
    }

    public event PointerScannerEventHandler? AddressableBaseChanged;
    public event PointerScannerEventHandler? AddressableLengthChanged;
    public event PointerScannerEventHandler? MaxDepthChanged;
    public event PointerScannerEventHandler? TargetAddressChanged;

    public PointerScanner() {
    }
}