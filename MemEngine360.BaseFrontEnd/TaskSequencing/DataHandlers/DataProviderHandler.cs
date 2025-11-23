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

using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.ValueAbstraction;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.DataHandlers;

// The data provider handlers massively simplify creating value binding between the list content and editor content controls

public abstract class DataProviderHandler {
    protected DataValueProvider? internalProvider;

    /// <summary>
    /// Gets or sets the data type that this handler should use to parse text box values
    /// </summary>
    public DataType DataType {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.OnDataTypeChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets if this handler should parse text as hexadecimal when <see cref="DataType"/> is integer based
    /// </summary>
    public bool ParseIntAsHex {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.OnParseIntAsHexChanged();
            }
        }
    }

    public bool IsConnected => this.internalProvider != null;

    public event EventHandler? DataTypeChanged;
    public event EventHandler? ParseIntAsHexChanged;

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
        this.DataTypeChanged?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnParseIntAsHexChanged() {
        this.ParseIntAsHexChanged?.Invoke(this, EventArgs.Empty);
    }

    protected static string GetTextFromDataValue(IDataValue? value, bool parseIntAsHex) {
        return value == null ? "" : DataValueUtils.GetStringFromDataValue(value, parseIntAsHex && value.DataType.IsInteger() ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal);
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