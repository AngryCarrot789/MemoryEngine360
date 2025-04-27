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

using MemEngine360.Engine.Modes;
using PFXToolKitUI;
using PFXToolKitUI.Persistence;

namespace MemEngine360.Configs;

public class BasicApplicationConfiguration : PersistentConfiguration {
    public static readonly PersistentProperty<string> LastHostNameProperty = PersistentProperty.RegisterString<BasicApplicationConfiguration>("LastHostName", defaultValue: "", owner => owner.lastHostName, (x, y) => x.lastHostName = y, false);
    public static readonly PersistentProperty<uint> StartAddressProperty = PersistentProperty.RegisterParsable<uint, BasicApplicationConfiguration>("StartAddress", defaultValue: 0x80000000, owner => owner.startAddr, (x, y) => x.startAddr = y, false);
    public static readonly PersistentProperty<uint> ScanLengthProperty = PersistentProperty.RegisterParsable<uint, BasicApplicationConfiguration>("ScanLength", defaultValue: 0x10000, owner => owner.scanLength, (x, y) => x.scanLength = y, false);
    public static readonly PersistentProperty<bool> PauseConsoleDuringScanProperty = PersistentProperty.RegisterBool<BasicApplicationConfiguration>("PauseConsoleDuringScan", defaultValue: false, owner => owner.pauseConsoleDuringScan, (x, y) => x.pauseConsoleDuringScan = y, false);
    public static readonly PersistentProperty<bool> ScanMemoryPagesProperty = PersistentProperty.RegisterBool<BasicApplicationConfiguration>("ScanMemoryPages", defaultValue: true, owner => owner.scanMemoryPages, (x, y) => x.scanMemoryPages = y, false);
    public static readonly PersistentProperty<bool> DTInt_UseHexValueProperty = PersistentProperty.RegisterBool<BasicApplicationConfiguration>("DTInt_UseHexValue", defaultValue: false, owner => owner.dtInt_UseHexValueProperty, (x, y) => x.dtInt_UseHexValueProperty = y, false);
    public static readonly PersistentProperty<byte> DTFloat_ModeProperty = PersistentProperty.RegisterParsable<byte, BasicApplicationConfiguration>("DTFloat_Mode", defaultValue: (byte) FloatScanOption.TruncateToQuery, owner => owner.dtFloat_Mode, (x, y) => x.dtFloat_Mode = y, false);
    public static readonly PersistentProperty<byte> DTString_ModeProperty = PersistentProperty.RegisterParsable<byte, BasicApplicationConfiguration>("DTString_Mode", defaultValue: (byte) StringType.UTF8, owner => owner.dtString_Mode, (x, y) => x.dtString_Mode = y, false);

    public static BasicApplicationConfiguration Instance => ApplicationPFX.Instance.PersistentStorageManager.GetConfiguration<BasicApplicationConfiguration>();

    private string lastHostName = LastHostNameProperty.DefaultValue;
    private uint startAddr = StartAddressProperty.DefaultValue;
    private uint scanLength = ScanLengthProperty.DefaultValue;
    private bool pauseConsoleDuringScan = PauseConsoleDuringScanProperty.DefaultValue;
    private bool scanMemoryPages = ScanMemoryPagesProperty.DefaultValue;
    private bool dtInt_UseHexValueProperty = DTInt_UseHexValueProperty.DefaultValue;
    private byte dtFloat_Mode = DTFloat_ModeProperty.DefaultValue;
    private byte dtString_Mode = DTString_ModeProperty.DefaultValue;


    /// <summary>
    /// Gets or sets the last host name that was entered when connecting to an xbox 360. This is just a convenience feature
    /// </summary>
    public string LastHostName {
        get => LastHostNameProperty.GetValue(this);
        set => LastHostNameProperty.SetValue(this, value);
    }

    public uint StartAddress {
        get => StartAddressProperty.GetValue(this);
        set => StartAddressProperty.SetValue(this, value);
    }

    public uint ScanLength {
        get => ScanLengthProperty.GetValue(this);
        set => ScanLengthProperty.SetValue(this, value);
    }

    public bool PauseConsoleDuringScan {
        get => PauseConsoleDuringScanProperty.GetValue(this);
        set => PauseConsoleDuringScanProperty.SetValue(this, value);
    }

    public bool ScanMemoryPages {
        get => ScanMemoryPagesProperty.GetValue(this);
        set => ScanMemoryPagesProperty.SetValue(this, value);
    }

    /// <summary>
    /// Gets or sets if the scan "Value" field is parsed in hexadecimal format rather
    /// than plain integer. When true, entering "400" results in an integer value of 1024
    /// </summary>
    public bool DTInt_UseHexValue {
        get => DTInt_UseHexValueProperty.GetValue(this);
        set => DTInt_UseHexValueProperty.SetValue(this, value);
    }

    public FloatScanOption DTFloat_Mode {
        get => (FloatScanOption) DTFloat_ModeProperty.GetValue(this);
        set => DTFloat_ModeProperty.SetValue(this, (byte) value);
    }

    public StringType DTString_Mode {
        get => (StringType) DTString_ModeProperty.GetValue(this);
        set => DTString_ModeProperty.SetValue(this, (byte) value);
    }

    public BasicApplicationConfiguration() {
    }
}