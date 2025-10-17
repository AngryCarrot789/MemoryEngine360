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

using MemEngine360.Engine.Modes;
using PFXToolKitUI;
using PFXToolKitUI.Persistence;

namespace MemEngine360.Configs;

public class BasicApplicationConfiguration : PersistentConfiguration {
    public static readonly PersistentProperty<string> DefaultThemeProperty =
        PersistentProperty.RegisterString<BasicApplicationConfiguration>(
            "DefaultTheme",
            defaultValue: "Dark",
            owner => owner.defaultTheme, (x, y) => x.defaultTheme = y, true);

    public static readonly PersistentProperty<string> LastXboxHostNameProperty =
        PersistentProperty.RegisterString<BasicApplicationConfiguration>(
            "LastXboxHostName",
            defaultValue: "",
            owner => owner.lastXboxHostName, (x, y) => x.lastXboxHostName = y, false);

    public static readonly PersistentProperty<string> LastPS3HostNameProperty =
        PersistentProperty.RegisterString<BasicApplicationConfiguration>(
            "LastPS3HostName",
            defaultValue: "",
            owner => owner.lastPS3HostName, (x, y) => x.lastPS3HostName = y, false);

    public static readonly PersistentProperty<string> LastConnectionTypeUsedProperty =
        PersistentProperty.RegisterString<BasicApplicationConfiguration>(
            "LastConnectionTypeUsed",
            defaultValue: "console.xbox360.xbdm-coreimpl",
            owner => owner.lastConnectionTypeUsed, (x, y) => x.lastConnectionTypeUsed = y, false);

    public static readonly PersistentProperty<uint> StartAddressProperty =
        PersistentProperty.RegisterParsable<uint, BasicApplicationConfiguration>(
            "StartAddress",
            defaultValue: 0x82600000,
            owner => owner.startAddr, (x, y) => x.startAddr = y, false);

    public static readonly PersistentProperty<uint> ScanLengthProperty =
        PersistentProperty.RegisterParsable<uint, BasicApplicationConfiguration>(
            "ScanLength",
            defaultValue: 0x1000000,
            owner => owner.scanLength, (x, y) => x.scanLength = y, false);

    public static readonly PersistentProperty<bool> PauseConsoleDuringScanProperty =
        PersistentProperty.RegisterBool<BasicApplicationConfiguration>(
            "PauseConsoleDuringScan",
            defaultValue: true,
            owner => owner.pauseConsoleDuringScan, (x, y) => x.pauseConsoleDuringScan = y, false);

    public static readonly PersistentProperty<bool> ScanMemoryPagesProperty =
        PersistentProperty.RegisterBool<BasicApplicationConfiguration>(
            "ScanMemoryPages",
            defaultValue: true,
            owner => owner.scanMemoryPages, (x, y) => x.scanMemoryPages = y, false);

    public static readonly PersistentProperty<bool> DTInt_UseHexValueProperty =
        PersistentProperty.RegisterBool<BasicApplicationConfiguration>(
            "DTInt_UseHexValue",
            defaultValue: false,
            owner => owner.dtInt_UseHexValueProperty, (x, y) => x.dtInt_UseHexValueProperty = y, false);

    public static readonly PersistentProperty<bool> DTString_IgnoreCaseProperty =
        PersistentProperty.RegisterBool<BasicApplicationConfiguration>(
            "DTString_IgnoreCase",
            defaultValue: true,
            owner => owner.dtString_IgnoreCase, (x, y) => x.dtString_IgnoreCase = y, false);

    public static readonly PersistentProperty<FloatScanOption> DTFloat_ModeProperty =
        PersistentProperty.RegisterEnum<FloatScanOption, BasicApplicationConfiguration>(
            "DTFloat_Mode",
            defaultValue: FloatScanOption.RoundToQuery,
            owner => owner.dtFloat_Mode, (x, y) => x.dtFloat_Mode = y, useNumericValue: false, false);

    public static readonly PersistentProperty<StringType> DTString_ModeProperty =
        PersistentProperty.RegisterEnum<StringType, BasicApplicationConfiguration>(
            "DTString_Mode",
            defaultValue: StringType.ASCII,
            owner => owner.dtString_Mode, (x, y) => x.dtString_Mode = y, useNumericValue: false, false);

    public static readonly PersistentProperty<uint> RefreshRateMillisProperty =
        PersistentProperty.RegisterParsable<uint, BasicApplicationConfiguration>(
            "RefreshRateMillis",
            defaultValue: 1000,
            owner => owner.refreshRateMillis, (x, y) => x.refreshRateMillis = Math.Max(y, 250), false);

    public static readonly PersistentProperty<uint> AutoRefreshUpdatesPerSecondProperty =
        PersistentProperty.RegisterParsable<uint, BasicApplicationConfiguration>(
            "AutoRefreshUpdatesPerSecond",
            defaultValue: 12,
            owner => owner.autoRefreshUpdatesPerSecond, (x, y) => x.autoRefreshUpdatesPerSecond = Math.Clamp(y, 1, 20), false);

    public static readonly PersistentProperty<uint> MaxRowsBeforeDisableAutoRefreshProperty =
        PersistentProperty.RegisterParsable<uint, BasicApplicationConfiguration>(
            "MaxRowsBeforeDisableAutoRefresh",
            defaultValue: 400,
            owner => owner.maxRowsBeforeDisableAutoRefresh, (x, y) => x.maxRowsBeforeDisableAutoRefresh = y, false);

    public static readonly PersistentProperty<bool> IsAutoRefreshResultsEnabledProperty =
        PersistentProperty.RegisterBool<BasicApplicationConfiguration>(
            "IsAutoRefreshResultsEnabled",
            defaultValue: true,
            owner => owner.isAutoRefreshResultsEnabled, (x, y) => x.isAutoRefreshResultsEnabled = y, false);

    public static readonly PersistentProperty<bool> UseNaNInfProtectionProperty =
        PersistentProperty.RegisterBool<BasicApplicationConfiguration>(
            "UseNaNInfProtection",
            defaultValue: true,
            owner => owner.useNaNInfProtection, (x, y) => x.useNaNInfProtection = y, false);

    public static readonly PersistentProperty<double> FloatingPointEpsilonProperty =
        PersistentProperty.RegisterParsable<double, BasicApplicationConfiguration>(
            "FloatingPointEpsilon",
            defaultValue: 0.00001D,
            owner => owner.floatingPointEpsilon, (x, y) => x.floatingPointEpsilon = Math.Clamp(y, 0, 0.9999D), false);
    
    public static readonly PersistentProperty<string[]> LoadedScriptPathsProperty =
        PersistentProperty.RegisterStringArray<BasicApplicationConfiguration>(
            "LoadedScriptPaths",
            defaultValue: null,
            owner => owner.loadedScriptPaths, (x, y) => x.loadedScriptPaths = y, false);

    public static BasicApplicationConfiguration Instance => ApplicationPFX.Instance.PersistentStorageManager.GetConfiguration<BasicApplicationConfiguration>();

    private string defaultTheme = DefaultThemeProperty.DefaultValue;
    private string lastXboxHostName = LastXboxHostNameProperty.DefaultValue;
    private string lastPS3HostName = LastPS3HostNameProperty.DefaultValue;
    private string lastConnectionTypeUsed = LastConnectionTypeUsedProperty.DefaultValue;
    private uint startAddr = StartAddressProperty.DefaultValue;
    private uint scanLength = ScanLengthProperty.DefaultValue;
    private bool pauseConsoleDuringScan = PauseConsoleDuringScanProperty.DefaultValue;
    private bool scanMemoryPages = ScanMemoryPagesProperty.DefaultValue;
    private bool dtInt_UseHexValueProperty = DTInt_UseHexValueProperty.DefaultValue;
    private bool dtString_IgnoreCase = DTString_IgnoreCaseProperty.DefaultValue;
    private FloatScanOption dtFloat_Mode = DTFloat_ModeProperty.DefaultValue;
    private StringType dtString_Mode = DTString_ModeProperty.DefaultValue;
    private uint refreshRateMillis = RefreshRateMillisProperty.DefaultValue;
    private uint autoRefreshUpdatesPerSecond = AutoRefreshUpdatesPerSecondProperty.DefaultValue;
    private uint maxRowsBeforeDisableAutoRefresh = MaxRowsBeforeDisableAutoRefreshProperty.DefaultValue;
    private bool isAutoRefreshResultsEnabled = IsAutoRefreshResultsEnabledProperty.DefaultValue;
    private bool useNaNInfProtection = UseNaNInfProtectionProperty.DefaultValue;
    private double floatingPointEpsilon = FloatingPointEpsilonProperty.DefaultValue;
    private string[] loadedScriptPaths = LoadedScriptPathsProperty.DefaultValue;

    /// <summary>
    /// Gets or sets the last host name that was entered when connecting to an xbox 360. This is just a convenience feature
    /// </summary>
    public string LastXboxHostName {
        get => LastXboxHostNameProperty.GetValue(this);
        set => LastXboxHostNameProperty.SetValue(this, value);
    }

    /// <summary>
    /// Gets or sets the last host name that was entered when connecting to a PS3
    /// </summary>
    public string LastPS3HostName {
        get => LastPS3HostNameProperty.GetValue(this);
        set => LastPS3HostNameProperty.SetValue(this, value);
    }
    
    /// <summary>
    /// Gets or sets the Id of the connection that was last connected to
    /// </summary>
    public string LastConnectionTypeUsed {
        get => LastConnectionTypeUsedProperty.GetValue(this);
        set => LastConnectionTypeUsedProperty.SetValue(this, value);
    }

    /// <summary>
    /// Gets or sets the default theme loaded at app startup
    /// </summary>
    public string DefaultTheme {
        get => DefaultThemeProperty.GetValue(this);
        set => DefaultThemeProperty.SetValue(this, value);
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

    public bool DTString_IgnoreCase {
        get => DTString_IgnoreCaseProperty.GetValue(this);
        set => DTString_IgnoreCaseProperty.SetValue(this, value);
    }

    public FloatScanOption DTFloat_Mode {
        get => DTFloat_ModeProperty.GetValue(this);
        set => DTFloat_ModeProperty.SetValue(this, value);
    }

    public StringType DTString_Mode {
        get => DTString_ModeProperty.GetValue(this);
        set => DTString_ModeProperty.SetValue(this, value);
    }

    public uint RefreshRateMillis {
        get => RefreshRateMillisProperty.GetValue(this);
        set => RefreshRateMillisProperty.SetValue(this, value);
    }

    public uint AutoRefreshUpdatesPerSecond {
        get => AutoRefreshUpdatesPerSecondProperty.GetValue(this);
        set => AutoRefreshUpdatesPerSecondProperty.SetValue(this, value);
    }

    public uint MaxRowsBeforeDisableAutoRefresh {
        get => MaxRowsBeforeDisableAutoRefreshProperty.GetValue(this);
        set => MaxRowsBeforeDisableAutoRefreshProperty.SetValue(this, value);
    }

    public bool IsAutoRefreshResultsEnabled {
        get => IsAutoRefreshResultsEnabledProperty.GetValue(this);
        set => IsAutoRefreshResultsEnabledProperty.SetValue(this, value);
    }

    public bool UseNaNInfProtection {
        get => UseNaNInfProtectionProperty.GetValue(this);
        set => UseNaNInfProtectionProperty.SetValue(this, value);
    }

    public double FloatingPointEpsilon {
        get => FloatingPointEpsilonProperty.GetValue(this);
        set => FloatingPointEpsilonProperty.SetValue(this, value);
    }
    
    public string[] LoadedScriptPaths {
        get => LoadedScriptPathsProperty.GetValue(this);
        set => LoadedScriptPathsProperty.SetValue(this, value);
    }

    public BasicApplicationConfiguration() {
    }
}