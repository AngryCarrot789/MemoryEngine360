using System.Diagnostics.CodeAnalysis;
using MemEngine360.Configs;
using PFXToolKitUI;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Connections;

public delegate void OpenConnectionInfoSelectedConnectionTypeChangedEventHandler(OpenConnectionInfo sender, ConnectionTypeEntry? oldSelectedConnectionType, ConnectionTypeEntry? newSelectedConnectionType);

/// <summary>
/// Contains information used in a <see cref="IOpenConnectionView"/>, such as applicable connections
/// </summary>
public sealed class OpenConnectionInfo {
    private ConnectionTypeEntry? selectedConnectionType;

    /// <summary>
    /// Gets the list of available connections the user can connect to
    /// </summary>
    public ObservableList<ConnectionTypeEntry> ConnectionTypes { get; }

    /// <summary>
    /// Gets or sets the selected connection type in the list
    /// </summary>
    public ConnectionTypeEntry? SelectedConnectionType {
        get => this.selectedConnectionType;
        set => PropertyHelper.SetAndRaiseINE(ref this.selectedConnectionType, value, this, static (t, o, n) => t.SelectedConnectionTypeChanged?.Invoke(t, o, n));
    }

    public event OpenConnectionInfoSelectedConnectionTypeChangedEventHandler? SelectedConnectionTypeChanged;

    public OpenConnectionInfo() {
        this.ConnectionTypes = new ObservableList<ConnectionTypeEntry>();
    }

    public bool TryGetEntryForType(RegisteredConnectionType type, [NotNullWhen(true)] out ConnectionTypeEntry? entry) {
        return (entry = this.ConnectionTypes.FirstOrDefault(x => ReferenceEquals(x.Type, type))) != null;
    }

    public static OpenConnectionInfo CreateDefault(string? selectedConsoleType = null, Predicate<RegisteredConnectionType>? isEnabledFilter = null) {
        ConsoleConnectionManager manager = ApplicationPFX.GetComponent<ConsoleConnectionManager>();

        if (string.IsNullOrWhiteSpace(selectedConsoleType))
            selectedConsoleType = null;

        selectedConsoleType ??= BasicApplicationConfiguration.Instance.LastConnectionTypeUsed;
        if (string.IsNullOrWhiteSpace(selectedConsoleType))
            selectedConsoleType = null;

        OpenConnectionInfo info = new OpenConnectionInfo();
        ConnectionTypeEntry? selectedEntry = null;

        foreach (RegisteredConnectionType type in manager.RegisteredConsoleTypes) {
            ConnectionTypeEntry entry = new ConnectionTypeEntry(type) {
                IsEnabled = isEnabledFilter == null || isEnabledFilter(type)
            };

            if (selectedConsoleType != null && selectedConsoleType.EqualsIgnoreCase(type.RegisteredId)) {
                selectedEntry = entry;
            }

            info.ConnectionTypes.Add(entry);
        }
        
        info.SelectedConnectionType = selectedEntry;
        if (info.SelectedConnectionType == null || !info.SelectedConnectionType.IsEnabled) {
            info.SelectedConnectionType = info.ConnectionTypes.FirstOrDefault(x => x.IsEnabled);
        }
        
        return info;
    }
}

public delegate void ControlTypeEntryEventHandler(ConnectionTypeEntry sender);

public delegate void ConnectionTypeEntryInfoChangedEventHandler(ConnectionTypeEntry sender, UserConnectionInfo? oldInfo, UserConnectionInfo? newInfo);

public sealed class ConnectionTypeEntry {
    private bool isEnabled = true;
    private UserConnectionInfo? info;

    /// <summary>
    /// Gets the connection type
    /// </summary>
    public RegisteredConnectionType Type { get; }

    /// <summary>
    /// Gets or sets if this connection type can be selected by the user or not.
    /// For example, connections on unsupported platforms cannot be clicked
    /// </summary>
    public bool IsEnabled {
        get => this.isEnabled;
        set => PropertyHelper.SetAndRaiseINE(ref this.isEnabled, value, this, static t => t.IsEnabledChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets or sets the open connection info presented for this connection type.
    /// This is lazily created by the UI when the console type is clicked for the first time.
    /// </summary>
    public UserConnectionInfo? Info {
        get => this.info;
        set {
            UserConnectionInfo? oldValue = this.info;
            if (!Equals(oldValue, value)) {
                bool wasVisible = false;
                if (oldValue != null && UserConnectionInfo.IsShownInView(oldValue)) {
                    wasVisible = true;
                    UserConnectionInfo.InternalHide(oldValue);
                }
                
                this.info = value;
                this.InfoChanged?.Invoke(this, oldValue, value);

                if (value != null && wasVisible) {
                    UserConnectionInfo.InternalShow(value);
                }
            }
        }
    }

    public event ControlTypeEntryEventHandler? IsEnabledChanged;
    public event ConnectionTypeEntryInfoChangedEventHandler? InfoChanged;

    public ConnectionTypeEntry(RegisteredConnectionType type) {
        this.Type = type;
    }

    /// <summary>
    /// Gets the current <see cref="Info"/> or creates new info via <see cref="RegisteredConnectionType.CreateConnectionInfo"/>
    /// </summary>
    public UserConnectionInfo? GetOrAssignConnectionInfo() {
        return this.Info ??= this.Type.CreateConnectionInfo();
    }
}