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
        await IMessageDialogService.Instance.ShowMessage("Invalid value", "Invalid integer value");
        return default;
    });

    private readonly TextBoxToDataParameterBinder<MemoryEngineConfigurationPage, uint> autoRefreshPerSecBinder = new TextBoxToDataParameterBinder<MemoryEngineConfigurationPage, uint>(MemoryEngineConfigurationPage.AutoRefreshUpdatesPerSecondParameter, null, async (binder, text) => {
        if (uint.TryParse(text, out uint value))
            return value;
        await IMessageDialogService.Instance.ShowMessage("Invalid value", "Invalid integer value");
        return default;
    });

    private readonly TextBoxToDataParameterBinder<MemoryEngineConfigurationPage, uint> updateCountLimitBinder = new TextBoxToDataParameterBinder<MemoryEngineConfigurationPage, uint>(MemoryEngineConfigurationPage.MaxRowsBeforeDisableAutoRefreshParameter, null, async (binder, text) => {
        if (uint.TryParse(text, out uint value))
            return value;
        await IMessageDialogService.Instance.ShowMessage("Invalid value", "Invalid integer value");
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
        this.refreshRateBinder.AttachControl(this.PART_RefreshRateTextBox = e.NameScope.GetTemplateChild<TextBox>("PART_RefreshRateTextBox"));
        this.autoRefreshPerSecBinder.AttachControl(this.PART_AutoRefreshPerSecTextBox = e.NameScope.GetTemplateChild<TextBox>("PART_AutoRefreshPerSecTextBox"));
        this.updateCountLimitBinder.AttachControl(this.PART_UpdateCountLimit = e.NameScope.GetTemplateChild<TextBox>("PART_UpdateCountLimit"));
        this.floatingEpsilonBinder.AttachControl(this.PART_FloatingEpsilonTextBox = e.NameScope.GetTemplateChild<TextBox>("PART_FloatingEpsilonTextBox"));
        this.isRefreshEnabledBinder.AttachControl(this.PART_ToggleRefreshEnabled = e.NameScope.GetTemplateChild<CheckBox>("PART_ToggleRefreshEnabled"));
    }

    public override void OnConnected() {
        base.OnConnected();
        Binders.AttachModels((MemoryEngineConfigurationPage) this.Page!, this.refreshRateBinder, this.autoRefreshPerSecBinder, this.updateCountLimitBinder, this.floatingEpsilonBinder, this.isRefreshEnabledBinder);
    }

    public override void OnDisconnected() {
        base.OnDisconnected();
        Binders.DetachModels(this.refreshRateBinder, this.autoRefreshPerSecBinder, this.updateCountLimitBinder, this.floatingEpsilonBinder, this.isRefreshEnabledBinder);
    }
}