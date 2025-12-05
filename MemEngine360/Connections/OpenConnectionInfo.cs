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

using System.Diagnostics.CodeAnalysis;
using MemEngine360.Configs;
using PFXToolKitUI;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Connections;

/// <summary>
/// Contains information used in a <see cref="IOpenConnectionView"/>, such as applicable connections
/// </summary>
public sealed class OpenConnectionInfo {
    /// <summary>
    /// Gets the list of available connections the user can connect to
    /// </summary>
    public ObservableList<ConnectionTypeEntry> ConnectionTypes { get; }

    /// <summary>
    /// Gets or sets the selected connection type in the list
    /// </summary>
    public ConnectionTypeEntry? SelectedConnectionType {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.SelectedConnectionTypeChanged);
    }

    public event EventHandler<ValueChangedEventArgs<ConnectionTypeEntry?>>? SelectedConnectionTypeChanged;

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

public sealed class ConnectionTypeEntry {
    /// <summary>
    /// Gets the connection type
    /// </summary>
    public RegisteredConnectionType Type { get; }

    /// <summary>
    /// Gets or sets if this connection type can be selected by the user or not.
    /// For example, connections on unsupported platforms cannot be clicked
    /// </summary>
    public bool IsEnabled {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsEnabledChanged);
    } = true;

    /// <summary>
    /// Gets or sets the open connection info presented for this connection type.
    /// This is lazily created by the UI when the console type is clicked for the first time.
    /// </summary>
    public UserConnectionInfo? Info {
        get => field;
        set {
            UserConnectionInfo? oldValue = field;
            if (!Equals(oldValue, value)) {
                bool wasVisible = false;
                if (oldValue != null && UserConnectionInfo.IsShownInView(oldValue)) {
                    wasVisible = true;
                    UserConnectionInfo.InternalHide(oldValue);
                }

                field = value;
                this.InfoChanged?.Invoke(this, new ValueChangedEventArgs<UserConnectionInfo?>(oldValue, value));

                if (value != null && wasVisible) {
                    UserConnectionInfo.InternalShow(value);
                }
            }
        }
    }

    public event EventHandler? IsEnabledChanged;
    public event EventHandler<ValueChangedEventArgs<UserConnectionInfo?>>? InfoChanged;

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