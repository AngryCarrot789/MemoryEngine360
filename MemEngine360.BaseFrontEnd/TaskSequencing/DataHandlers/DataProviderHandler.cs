using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.ValueAbstraction;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.DataHandlers;

public delegate void DataProviderHandlerEventHandler(DataProviderHandler sender);

public abstract class DataProviderHandler {
    protected DataValueProvider? internalProvider;
    private DataType myDataType;
    private StringType stringType;
    private bool parseIntAsHex;

    /// <summary>
    /// Gets or sets the data type that this handler should use to parse text box values
    /// </summary>
    public DataType DataType {
        get => this.myDataType;
        set {
            if (this.myDataType != value) {
                this.myDataType = value;
                this.OnDataTypeChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the encoding used to encode/decode strings/bytes
    /// </summary>
    public StringType StringType {
        get => this.stringType;
        set {
            if (this.stringType != value) {
                this.stringType = value;
                this.OnStringTypeChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets if this handler should parse text as hexadecimal when <see cref="DataType"/> is integer based
    /// </summary>
    public bool ParseIntAsHex {
        get => this.parseIntAsHex;
        set {
            if (this.parseIntAsHex != value) {
                this.parseIntAsHex = value;
                this.OnParseIntAsHexChanged();
            }
        }
    }

    public bool IsConnected => this.internalProvider != null;

    public event DataProviderHandlerEventHandler? DataTypeChanged;
    public event DataProviderHandlerEventHandler? StringTypeChanged;
    public event DataProviderHandlerEventHandler? ParseIntAsHexChanged;

    protected DataProviderHandler() {
    }

    public void Connect(DataValueProvider newProvider) {
        if (this.internalProvider != null)
            throw new InvalidOperationException("Already connected");
        if (!this.CheckProviderType(newProvider))
            throw new InvalidOperationException("Invalid provider type: " + newProvider.GetType());

        this.internalProvider = newProvider;
        this.OnConnected();
    }

    public void Disconnect() {
        if (this.internalProvider == null)
            throw new InvalidOperationException("Not connected");

        this.OnDisconnect();
        this.internalProvider = null;
    }

    protected abstract void OnConnected();

    protected abstract void OnDisconnect();

    protected virtual bool CheckProviderType(DataValueProvider provider) {
        return true;
    }

    protected virtual void OnDataTypeChanged() {
        this.DataTypeChanged?.Invoke(this);
    }

    protected virtual void OnStringTypeChanged() {
        this.StringTypeChanged?.Invoke(this);
    }

    protected virtual void OnParseIntAsHexChanged() {
        this.ParseIntAsHexChanged?.Invoke(this);
    }

    protected static string GetTextFromDataValue(IDataValue? value, bool parseIntAsHex) {
        return value == null ? "" : MemoryEngine360.GetStringFromDataValue(value, parseIntAsHex && value.DataType.IsInteger() ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal);
    }
}

public abstract class DataProviderHandler<T> : DataProviderHandler where T : DataValueProvider {
    public T Provider => (T?) this.internalProvider ?? throw new InvalidOperationException("Not connected");

    protected DataProviderHandler() {
    }

    protected sealed override bool CheckProviderType(DataValueProvider provider) {
        return provider is T;
    }
}