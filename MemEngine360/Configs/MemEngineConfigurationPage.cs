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

using PFXToolKitUI.Configurations;
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Utils.Accessing;

namespace MemEngine360.Configs;

public class MemEngineConfigurationPage : ConfigurationPage, ITransferableData {
    public static readonly DataParameterNumber<uint> ValueRefreshRateParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(MemEngineConfigurationPage), nameof(ValueRefreshRate), BasicApplicationConfiguration.RefreshRateMillisProperty.DefaultValue, 500, uint.MaxValue, ValueAccessors.Reflective<uint>(typeof(MemEngineConfigurationPage), nameof(valueRefreshRate))));
    public static readonly DataParameterNumber<uint> AutoRefreshUpdatesPerSecondParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(MemEngineConfigurationPage), nameof(AutoRefreshUpdatesPerSecond), BasicApplicationConfiguration.AutoRefreshUpdatesPerSecondProperty.DefaultValue, 1, 20, ValueAccessors.Reflective<uint>(typeof(MemEngineConfigurationPage), nameof(autoRefreshUpdatesPerSecond))));
    public static readonly DataParameterNumber<uint> MaxRowsBeforeDisableAutoRefreshParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(MemEngineConfigurationPage), nameof(MaxRowsBeforeDisableAutoRefresh), BasicApplicationConfiguration.MaxRowsBeforeDisableAutoRefreshProperty.DefaultValue, ValueAccessors.Reflective<uint>(typeof(MemEngineConfigurationPage), nameof(maxRowsBeforeDisableAutoRefresh))));
    public static readonly DataParameterBool IsAutoRefreshResultsEnabledParameter = DataParameter.Register(new DataParameterBool(typeof(MemEngineConfigurationPage), nameof(IsAutoRefreshResultsEnabled), BasicApplicationConfiguration.IsAutoRefreshResultsEnabledProperty.DefaultValue, ValueAccessors.Reflective<bool>(typeof(MemEngineConfigurationPage), nameof(isValueRefreshEnabled))));

    private bool ignoreDpChange;
    private uint valueRefreshRate;
    private uint autoRefreshUpdatesPerSecond;
    private uint maxRowsBeforeDisableAutoRefresh;
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

    public TransferableData TransferableData { get; }

    public MemEngineConfigurationPage() {
        this.TransferableData = new TransferableData(this);
        this.valueRefreshRate = ValueRefreshRateParameter.GetDefaultValue(this);
        this.autoRefreshUpdatesPerSecond = AutoRefreshUpdatesPerSecondParameter.GetDefaultValue(this);
        this.maxRowsBeforeDisableAutoRefresh = MaxRowsBeforeDisableAutoRefreshParameter.GetDefaultValue(this);
        this.isValueRefreshEnabled = IsAutoRefreshResultsEnabledParameter.GetDefaultValue(this);
    }

    static MemEngineConfigurationPage() {
        DataParameter.AddMultipleHandlers(MarkModifiedForDPChanged, 
            ValueRefreshRateParameter, AutoRefreshUpdatesPerSecondParameter, 
            MaxRowsBeforeDisableAutoRefreshParameter, IsAutoRefreshResultsEnabledParameter);
    }

    private static void MarkModifiedForDPChanged(DataParameter parameter, ITransferableData owner) {
        if (!((MemEngineConfigurationPage) owner).ignoreDpChange)
            ((MemEngineConfigurationPage) owner).IsModified = true;
    }

    protected override ValueTask OnContextCreated(ConfigurationContext context) {
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnContextDestroyed(ConfigurationContext context) {
        return ValueTask.CompletedTask;
    }

    protected override void OnActiveContextChanged(ConfigurationContext? oldContext, ConfigurationContext? newContext) {
        base.OnActiveContextChanged(oldContext, newContext);
        if (newContext != null) {
            this.ignoreDpChange = true;
            this.ValueRefreshRate = BasicApplicationConfiguration.Instance.RefreshRateMillis;
            this.AutoRefreshUpdatesPerSecond = BasicApplicationConfiguration.Instance.AutoRefreshUpdatesPerSecond;
            this.MaxRowsBeforeDisableAutoRefresh = BasicApplicationConfiguration.Instance.MaxRowsBeforeDisableAutoRefresh;
            this.IsAutoRefreshResultsEnabled = BasicApplicationConfiguration.Instance.IsAutoRefreshResultsEnabled;
            this.ignoreDpChange = false;
            this.IsModified = false;
        }
    }

    public override ValueTask Apply(List<ApplyChangesFailureEntry>? errors) {
        BasicApplicationConfiguration.Instance.RefreshRateMillis = this.ValueRefreshRate;
        BasicApplicationConfiguration.Instance.AutoRefreshUpdatesPerSecond = this.AutoRefreshUpdatesPerSecond;
        BasicApplicationConfiguration.Instance.MaxRowsBeforeDisableAutoRefresh = this.MaxRowsBeforeDisableAutoRefresh;
        BasicApplicationConfiguration.Instance.IsAutoRefreshResultsEnabled = this.IsAutoRefreshResultsEnabled;
        this.IsModified = false;
        return ValueTask.CompletedTask;
    }
}