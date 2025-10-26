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

using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using MemEngine360.BaseFrontEnd.ModTools.Controls;
using MemEngine360.ModTools;
using MemEngine360.ModTools.Commands;
using MemEngine360.ModTools.Gui;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;

namespace MemEngine360.BaseFrontEnd.ModTools;

public partial class ModToolView : UserControl {
    public static readonly StyledProperty<ModTool?> ModToolProperty = AvaloniaProperty.Register<ModToolView, ModTool?>(nameof(ModTool));

    public ModTool? ModTool {
        get => this.GetValue(ModToolProperty);
        set => this.SetValue(ModToolProperty, value);
    }

    private IDesktopWindow? myWindow;

    public ModToolView() {
        this.InitializeComponent();
    }

    static ModToolView() {
        ModToolProperty.Changed.AddClassHandler<ModToolView, ModTool?>((s, e) => s.OnModToolChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnModToolChanged(ModTool? oldValue, ModTool? newValue) {
        if (oldValue != null)
            oldValue.Gui.RootPanelChanged -= this.OnGuiRootPanelChanged;

        if (newValue != null)
            newValue.Gui.RootPanelChanged += this.OnGuiRootPanelChanged;

        this.Content = CreateControl(newValue?.Gui.RootPanel);
    }

    private void OnGuiRootPanelChanged(ModToolGUI sender, BaseMTPanel? oldRootPanel, BaseMTPanel? newRootPanel) {
        this.Content = CreateControl(newRootPanel);
    }

    public void OnWindowOpened(IDesktopWindow sender) {
        this.myWindow = sender;
    }

    public async Task<bool> ShouldCancelWindowClosing(bool forced) {
        ModTool? tool = this.ModTool;
        if (tool == null || !tool.IsRunning || await RestartModToolCommand.StopModTool(tool, false)) {
            return false;
        }

        return true;

    }

    public void OnWindowClosed() {
        this.myWindow = null;
        this.ModTool = null;
    }

    [return: NotNullIfNotNull("element")]
    public static Control? CreateControl(BaseMTElement? element) {
        if (element == null)
            return null;

        switch (element) {
            case MTDockPanel e:  return new ControlMTDockPanel() { MTDockPanel = e };
            case MTStackPanel e: return new ControlMTStackPanel() { MTStackPanel = e };
            case MTGridPanel e:  return new ControlMTGridPanel() { MTGridPanel = e };
            case MTButton e:     return new ControlMTButton() { MTButton = e };
            case MTTextBlock e:  return new ControlMTTextBlock() { MTTextBlock = e };
            default:             throw new ArgumentOutOfRangeException(nameof(element));
        }
    }
}