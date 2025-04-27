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
using PFXToolKitUI.Persistence;
using PFXToolKitUI.PropertyEditing.DataTransfer;
using PFXToolKitUI.Utils.Accessing;

namespace MemEngine360.Configs;

public class MemEngineConfigurationPage : PropertyEditorConfigurationPage {
    public static readonly DataParameterNumber<uint> ValueRefreshRateParameter = DataParameter.Register(new DataParameterNumber<uint>(typeof(MemEngineConfigurationPage), nameof(ValueRefreshRate), BasicApplicationConfiguration.RefreshRateMillisProperty.DefaultValue, 500, uint.MaxValue, ValueAccessors.Reflective<uint>(typeof(MemEngineConfigurationPage), nameof(valueRefreshRate))));

    private uint valueRefreshRate;

    public uint ValueRefreshRate {
        get => this.valueRefreshRate;
        set => DataParameter.SetValueHelper(this, ValueRefreshRateParameter, ref this.valueRefreshRate, value);
    }
    
    public MemEngineConfigurationPage() {
        this.valueRefreshRate = ValueRefreshRateParameter.GetDefaultValue(this);
        
        this.PropertyEditor.Root.AddItem(new DataParameterNumberPropertyEditorSlot<uint>(ValueRefreshRateParameter, typeof(MemEngineConfigurationPage), "Refresh Rate"));
    }
    
    static MemEngineConfigurationPage() {
        AffectsModifiedState(ValueRefreshRateParameter);
    }

    protected override ValueTask OnContextCreated(ConfigurationContext context) {
        this.PropertyEditor.Root.SetupHierarchyState([this]);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnContextDestroyed(ConfigurationContext context) {
        this.PropertyEditor.Root.ClearHierarchy();
        return ValueTask.CompletedTask;
    }
    
    protected override void OnActiveContextChanged(ConfigurationContext? oldContext, ConfigurationContext? newContext) {
        base.OnActiveContextChanged(oldContext, newContext);
        if (oldContext != null)
            BasicApplicationConfiguration.RefreshRateMillisProperty.RemoveValueChangeHandler(BasicApplicationConfiguration.Instance, this.OnRefreshRateChanged);
        if (newContext != null)
            BasicApplicationConfiguration.RefreshRateMillisProperty.AddValueChangeHandler(BasicApplicationConfiguration.Instance, this.OnRefreshRateChanged);
        this.ValueRefreshRate = BasicApplicationConfiguration.Instance.RefreshRateMillis;
    }

    private void OnRefreshRateChanged(PersistentConfiguration config, PersistentProperty<uint> property, uint oldvalue, uint newvalue) {
        this.ValueRefreshRate = BasicApplicationConfiguration.Instance.RefreshRateMillis;
    }

    public override ValueTask Apply(List<ApplyChangesFailureEntry>? errors) {
        BasicApplicationConfiguration.Instance.RefreshRateMillis = this.ValueRefreshRate;
        return ValueTask.CompletedTask;
    }
}