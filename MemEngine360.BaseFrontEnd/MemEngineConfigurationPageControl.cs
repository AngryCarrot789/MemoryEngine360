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

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using MemEngine360.Configs;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Configurations.Pages;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.BaseFrontEnd;

public class MemEngineConfigurationPageControl : BaseConfigurationPageControl {
    private readonly TextBoxToDataParameterBinder<MemEngineConfigurationPage, uint> refreshRateBinder = new TextBoxToDataParameterBinder<MemEngineConfigurationPage, uint>(MemEngineConfigurationPage.ValueRefreshRateParameter, null, async (binder, text) => {
        if (uint.TryParse(text, out uint value))
            return value;
        await IMessageDialogService.Instance.ShowMessage("Invalid value", "Value is not an integer.");
        return default;
    });

    private readonly TextBoxToDataParameterBinder<MemEngineConfigurationPage, uint> autoRefreshPerSecBinder = new TextBoxToDataParameterBinder<MemEngineConfigurationPage, uint>(MemEngineConfigurationPage.AutoRefreshUpdatesPerSecondParameter, null, async (binder, text) => {
        if (uint.TryParse(text, out uint value))
            return value;
        await IMessageDialogService.Instance.ShowMessage("Invalid value", "Value is not an integer.");
        return default;
    });

    private readonly TextBoxToDataParameterBinder<MemEngineConfigurationPage, uint> updateCountLimitBinder = new TextBoxToDataParameterBinder<MemEngineConfigurationPage, uint>(MemEngineConfigurationPage.MaxRowsBeforeDisableAutoRefreshParameter, null, async (binder, text) => {
        if (uint.TryParse(text, out uint value))
            return value;
        await IMessageDialogService.Instance.ShowMessage("Invalid value", "Value is not an integer.");
        return default;
    });

    private readonly TextBoxToDataParameterBinder<MemEngineConfigurationPage, double> floatingEpsilonBinder = new TextBoxToDataParameterBinder<MemEngineConfigurationPage, double>(MemEngineConfigurationPage.FloatingPointEpsilonParameter, null, async (binder, text) => {
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

    private readonly IBinder<MemEngineConfigurationPage> isRefreshEnabledBinder = new AvaloniaPropertyToDataParameterBinder<MemEngineConfigurationPage>(CheckBox.IsCheckedProperty, MemEngineConfigurationPage.IsAutoRefreshResultsEnabledParameter, null, (o) => (bool?) o == true);

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

        this.refreshRateBinder.Attach(this.PART_RefreshRateTextBox!, (MemEngineConfigurationPage) this.Page!);
        this.autoRefreshPerSecBinder.Attach(this.PART_AutoRefreshPerSecTextBox!, (MemEngineConfigurationPage) this.Page!);
        this.updateCountLimitBinder.Attach(this.PART_UpdateCountLimit!, (MemEngineConfigurationPage) this.Page!);
        this.floatingEpsilonBinder.Attach(this.PART_FloatingEpsilonTextBox!, (MemEngineConfigurationPage) this.Page!);
        this.isRefreshEnabledBinder.Attach(this.PART_ToggleRefreshEnabled!, (MemEngineConfigurationPage) this.Page!);
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