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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using MemEngine360.Configs;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;
using PFXToolKitUI.Avalonia.Configurations.Pages;
using PFXToolKitUI.Avalonia.ToolTips;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.BaseFrontEnd;

public class MemoryEngineConfigurationPageControlV2 : BaseConfigurationPageControl {
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

    private readonly TextBox? PART_RefreshRateTextBox;
    private readonly TextBox? PART_AutoRefreshPerSecTextBox;
    private readonly TextBox? PART_UpdateCountLimit;
    private readonly TextBox? PART_FloatingEpsilonTextBox;
    private readonly CheckBox? PART_ToggleRefreshEnabled;

    public MemoryEngineConfigurationPageControlV2() {
        this.PART_RefreshRateTextBox = new TextBox() { Padding = new Thickness(4, 2), VerticalContentAlignment = VerticalAlignment.Center, [ToolTipEx.TipProperty] = "Default is 250 milliseconds" };
        this.PART_AutoRefreshPerSecTextBox = new TextBox() { Padding = new Thickness(4, 2) };
        this.PART_UpdateCountLimit = new TextBox() { Padding = new Thickness(4, 2) };
        this.PART_FloatingEpsilonTextBox = new TextBox() { Padding = new Thickness(4, 2) };
        this.PART_ToggleRefreshEnabled = new CheckBox() { [DockPanel.DockProperty] = Dock.Left, [ToolTipEx.TipProperty] = "Toggles whether auto-refreshing saved addresses and scan results is enabled", Content = "Is Enabled", VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 1) };

        StackPanel stackPanel;
        this.LogicalChildren.Add(stackPanel = new StackPanel() {
            Children = {
                new TextBlock() { Padding = new Thickness(2), Text = "Saved/Scan Results Refresh interval (in milliseconds)" },
                new TextBlock() { Padding = new Thickness(2, 0, 2, 2), Text = "Default is the minimum of 250ms (updates 4 times a second)", FontSize = 10, [!ForegroundProperty] = new DynamicResourceExtension("ABrush.Foreground.Deeper") },
                new DockPanel() {
                    Children = {
                        this.PART_ToggleRefreshEnabled,
                        this.PART_RefreshRateTextBox
                    }
                },
                new TextBlock() { Padding = new Thickness(2), Text = "Maximum number of rows before auto-refresh is disabled" },
                this.PART_UpdateCountLimit,
                new Separator(),
                new TextBlock() { Padding = new Thickness(2), Text = "Memory Viewer Auto Refresh Rate (updates per second)" },
                new TextBlock() { Padding = new Thickness(2, 0, 2, 2), Text = "Minimum is 1, maximum is 20, default is 12." },
                new TextBlock() { Padding = new Thickness(2, 0, 2, 2), Text = "Beware, higher values result in UI lag. 12 upd/s is just about tolerable." },
                this.PART_AutoRefreshPerSecTextBox,
                new Separator(),
                new TextBlock() { Padding = new Thickness(2), Text = "Floating Point Equality Epsilon" },
                this.PART_FloatingEpsilonTextBox,
                new TextBlock() { Padding = new Thickness(2, 0, 2, 2), Text = "Default is 0.000001" },
            }
        });

        this.VisualChildren.Add(stackPanel);
        
        this.refreshRateBinder.AttachControl(this.PART_RefreshRateTextBox);
        this.autoRefreshPerSecBinder.AttachControl(this.PART_AutoRefreshPerSecTextBox);
        this.updateCountLimitBinder.AttachControl(this.PART_UpdateCountLimit);
        this.floatingEpsilonBinder.AttachControl(this.PART_FloatingEpsilonTextBox);
        this.isRefreshEnabledBinder.AttachControl(this.PART_ToggleRefreshEnabled);
    }

    public override void OnConnected() {
        base.OnConnected();

        this.refreshRateBinder.AttachModel((MemoryEngineConfigurationPage) this.Page!);
        this.autoRefreshPerSecBinder.AttachModel((MemoryEngineConfigurationPage) this.Page!);
        this.updateCountLimitBinder.AttachModel((MemoryEngineConfigurationPage) this.Page!);
        this.floatingEpsilonBinder.AttachModel((MemoryEngineConfigurationPage) this.Page!);
        this.isRefreshEnabledBinder.AttachModel((MemoryEngineConfigurationPage) this.Page!);
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