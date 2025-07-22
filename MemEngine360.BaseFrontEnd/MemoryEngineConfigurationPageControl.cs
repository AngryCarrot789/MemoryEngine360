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

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using MemEngine360.Configs;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;
using PFXToolKitUI.Avalonia.Configurations.Pages;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.BaseFrontEnd;

public class MemoryEngineConfigurationPageControl : BaseConfigurationPageControl {
    private readonly TextBoxToDataParameterBinder<MemoryEngineConfigurationPage, uint> refreshRateBinder = new TextBoxToDataParameterBinder<MemoryEngineConfigurationPage, uint>(MemoryEngineConfigurationPage.ValueRefreshRateParameter, null, async (binder, text) => {
        if (uint.TryParse(text, out uint value))
            return value;
        await IMessageDialogService.Instance.ShowMessage("Invalid value", "Value is not an integer.");
        return default;
    });

    private readonly TextBoxToDataParameterBinder<MemoryEngineConfigurationPage, uint> autoRefreshPerSecBinder = new TextBoxToDataParameterBinder<MemoryEngineConfigurationPage, uint>(MemoryEngineConfigurationPage.AutoRefreshUpdatesPerSecondParameter, null, async (binder, text) => {
        if (uint.TryParse(text, out uint value))
            return value;
        await IMessageDialogService.Instance.ShowMessage("Invalid value", "Value is not an integer.");
        return default;
    });

    private readonly TextBoxToDataParameterBinder<MemoryEngineConfigurationPage, uint> updateCountLimitBinder = new TextBoxToDataParameterBinder<MemoryEngineConfigurationPage, uint>(MemoryEngineConfigurationPage.MaxRowsBeforeDisableAutoRefreshParameter, null, async (binder, text) => {
        if (uint.TryParse(text, out uint value))
            return value;
        await IMessageDialogService.Instance.ShowMessage("Invalid value", "Value is not an integer.");
        return default;
    });

    private readonly TextBoxToDataParameterBinder<MemoryEngineConfigurationPage, double> floatingEpsilonBinder = new TextBoxToDataParameterBinder<MemoryEngineConfigurationPage, double>(MemoryEngineConfigurationPage.FloatingPointEpsilonParameter, null, async (binder, text) => {
        if (!double.TryParse(text, out double value)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Value is not a floating point number.");
        }
        else if (value < 0.0D) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Value cannot be negative.");
        }
        else if (value > 0.9999D) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Value is too large for the epsilon value");
        }
        else {
            return value;
        }

        return default;
    });

    private readonly IBinder<MemoryEngineConfigurationPage> isRefreshEnabledBinder = new AvaloniaPropertyToDataParameterAutoBinder<MemoryEngineConfigurationPage>(CheckBox.IsCheckedProperty, MemoryEngineConfigurationPage.IsAutoRefreshResultsEnabledParameter, null, (o) => (bool?) o == true);

    private TextBox? PART_RefreshRateTextBox;
    private TextBox? PART_AutoRefreshPerSecTextBox;
    private TextBox? PART_UpdateCountLimit;
    private TextBox? PART_FloatingEpsilonTextBox;
    private CheckBox? PART_ToggleRefreshEnabled;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_RefreshRateTextBox = e.NameScope.GetTemplateChild<TextBox>("PART_RefreshRateTextBox");
        this.PART_AutoRefreshPerSecTextBox = e.NameScope.GetTemplateChild<TextBox>("PART_AutoRefreshPerSecTextBox");
        this.PART_UpdateCountLimit = e.NameScope.GetTemplateChild<TextBox>("PART_UpdateCountLimit");
        this.PART_FloatingEpsilonTextBox = e.NameScope.GetTemplateChild<TextBox>("PART_FloatingEpsilonTextBox");
        this.PART_ToggleRefreshEnabled = e.NameScope.GetTemplateChild<CheckBox>("PART_ToggleRefreshEnabled");
    }

    public override void OnConnected() {
        base.OnConnected();

        this.refreshRateBinder.Attach(this.PART_RefreshRateTextBox!, (MemoryEngineConfigurationPage) this.Page!);
        this.autoRefreshPerSecBinder.Attach(this.PART_AutoRefreshPerSecTextBox!, (MemoryEngineConfigurationPage) this.Page!);
        this.updateCountLimitBinder.Attach(this.PART_UpdateCountLimit!, (MemoryEngineConfigurationPage) this.Page!);
        this.floatingEpsilonBinder.Attach(this.PART_FloatingEpsilonTextBox!, (MemoryEngineConfigurationPage) this.Page!);
        this.isRefreshEnabledBinder.Attach(this.PART_ToggleRefreshEnabled!, (MemoryEngineConfigurationPage) this.Page!);
    }

    public override void OnDisconnected() {
        base.OnDisconnected();
        this.refreshRateBinder.Detach();
        this.autoRefreshPerSecBinder.Detach();
        this.updateCountLimitBinder.Detach();
        this.floatingEpsilonBinder.Detach();
        this.isRefreshEnabledBinder.Detach();
    }
}