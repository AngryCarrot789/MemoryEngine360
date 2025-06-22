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

using PFXToolKitUI.Configurations;
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Utils.Accessing;

namespace MemEngine360.Configs;

public class MemoryEngineConfigurationPage : ConfigurationPage {
    public static readonly DataParameterNumber<uint> ValueRefreshRateParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(MemoryEngineConfigurationPage), nameof(ValueRefreshRate), BasicApplicationConfiguration.RefreshRateMillisProperty.DefaultValue, 250, uint.MaxValue, ValueAccessors.Reflective<uint>(typeof(MemoryEngineConfigurationPage), nameof(valueRefreshRate))));
    public static readonly DataParameterNumber<uint> AutoRefreshUpdatesPerSecondParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(MemoryEngineConfigurationPage), nameof(AutoRefreshUpdatesPerSecond), BasicApplicationConfiguration.AutoRefreshUpdatesPerSecondProperty.DefaultValue, 1, 20, ValueAccessors.Reflective<uint>(typeof(MemoryEngineConfigurationPage), nameof(autoRefreshUpdatesPerSecond))));
    public static readonly DataParameterNumber<uint> MaxRowsBeforeDisableAutoRefreshParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(MemoryEngineConfigurationPage), nameof(MaxRowsBeforeDisableAutoRefresh), BasicApplicationConfiguration.MaxRowsBeforeDisableAutoRefreshProperty.DefaultValue, ValueAccessors.Reflective<uint>(typeof(MemoryEngineConfigurationPage), nameof(maxRowsBeforeDisableAutoRefresh))));
    public static readonly DataParameterNumber<double> FloatingPointEpsilonParameter = DataParameter.Register(new DataParameterNumber<double>(typeof(MemoryEngineConfigurationPage), nameof(FloatingPointEpsilon), BasicApplicationConfiguration.FloatingPointEpsilonProperty.DefaultValue, 0, 0.9999D, ValueAccessors.Reflective<double>(typeof(MemoryEngineConfigurationPage), nameof(floatingPointEpsilon))));
    public static readonly DataParameterBool IsAutoRefreshResultsEnabledParameter = DataParameter.Register(new DataParameterBool(typeof(MemoryEngineConfigurationPage), nameof(IsAutoRefreshResultsEnabled), BasicApplicationConfiguration.IsAutoRefreshResultsEnabledProperty.DefaultValue, ValueAccessors.Reflective<bool>(typeof(MemoryEngineConfigurationPage), nameof(isValueRefreshEnabled))));

    private uint valueRefreshRate;
    private uint autoRefreshUpdatesPerSecond;
    private uint maxRowsBeforeDisableAutoRefresh;
    private double floatingPointEpsilon;
    private bool isValueRefreshEnabled;

    public uint ValueRefreshRate {
        get => this.valueRefreshRate;
        set => DataParameter.SetValueHelper(this, ValueRefreshRateParameter, ref this.valueRefreshRate, value);
    }

    public uint AutoRefreshUpdatesPerSecond {
        get => this.autoRefreshUpdatesPerSecond;
        set => DataParameter.SetValueHelper(this, AutoRefreshUpdatesPerSecondParameter, ref this.autoRefreshUpdatesPerSecond, value);
    }

    public uint MaxRowsBeforeDisableAutoRefresh {
        get => this.maxRowsBeforeDisableAutoRefresh;
        set => DataParameter.SetValueHelper(this, MaxRowsBeforeDisableAutoRefreshParameter, ref this.maxRowsBeforeDisableAutoRefresh, value);
    }

    public bool IsAutoRefreshResultsEnabled {
        get => this.isValueRefreshEnabled;
        set => DataParameter.SetValueHelper(this, IsAutoRefreshResultsEnabledParameter, ref this.isValueRefreshEnabled, value);
    }

    public double FloatingPointEpsilon {
        get => this.floatingPointEpsilon;
        set => DataParameter.SetValueHelper(this, FloatingPointEpsilonParameter, ref this.floatingPointEpsilon, value);
    }

    public MemoryEngineConfigurationPage() {
    }

    static MemoryEngineConfigurationPage() {
        AffectsIsModified(ValueRefreshRateParameter, AutoRefreshUpdatesPerSecondParameter, MaxRowsBeforeDisableAutoRefreshParameter, IsAutoRefreshResultsEnabledParameter);
    }

    protected override ValueTask OnContextCreated(ConfigurationContext context) {
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnContextDestroyed(ConfigurationContext context) {
        return ValueTask.CompletedTask;
    }

    protected override void OnIsViewingChanged(ConfigurationContext? oldContext, ConfigurationContext? newContext) {
        base.OnIsViewingChanged(oldContext, newContext);
        if (newContext != null) {
            this.ValueRefreshRate = BasicApplicationConfiguration.Instance.RefreshRateMillis;
            this.AutoRefreshUpdatesPerSecond = BasicApplicationConfiguration.Instance.AutoRefreshUpdatesPerSecond;
            this.MaxRowsBeforeDisableAutoRefresh = BasicApplicationConfiguration.Instance.MaxRowsBeforeDisableAutoRefresh;
            this.IsAutoRefreshResultsEnabled = BasicApplicationConfiguration.Instance.IsAutoRefreshResultsEnabled;
            this.FloatingPointEpsilon = BasicApplicationConfiguration.Instance.FloatingPointEpsilon;
            this.IsModified = false;
        }
    }

    public override ValueTask Apply(List<ApplyChangesFailureEntry>? errors) {
        BasicApplicationConfiguration.Instance.RefreshRateMillis = this.ValueRefreshRate;
        BasicApplicationConfiguration.Instance.AutoRefreshUpdatesPerSecond = this.AutoRefreshUpdatesPerSecond;
        BasicApplicationConfiguration.Instance.MaxRowsBeforeDisableAutoRefresh = this.MaxRowsBeforeDisableAutoRefresh;
        BasicApplicationConfiguration.Instance.IsAutoRefreshResultsEnabled = this.IsAutoRefreshResultsEnabled;
        BasicApplicationConfiguration.Instance.FloatingPointEpsilon = this.FloatingPointEpsilon;
        this.IsModified = false;
        return ValueTask.CompletedTask;
    }
}