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
using Avalonia.Controls.Primitives;
using MemEngine360.Connections;
using PFXToolKitUI.Avalonia.AvControls;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.ToolTips;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.BaseFrontEnd.Services.Connectivity;

public class ConsoleTypeListBoxItem : ModelBasedListBoxItem<ConnectionTypeEntry> {
    private readonly ManualBinder<RegisteredConnectionType> iconBinder = new ManualBinder<RegisteredConnectionType>(b => {
        ((IconControl) b.Control).IsVisible = (((IconControl) b.Control).Icon = b.Model.Icon) != null;
    }, (b) => {
        ((IconControl) b.Control).Icon = null;
    });

    private readonly ManualBinder<RegisteredConnectionType> platformIconsBinder = new ManualBinder<RegisteredConnectionType>(b => {
        StackPanel sp = (StackPanel) b.Control;
        List<PlatformIconInfo> icons = b.Model.PlatformIcons.ToList();
        sp.IsVisible = icons.Count > 0;
        if (icons.Count > 0) {
            foreach (PlatformIconInfo icon in icons) {
                IconControl control = new IconControl() {
                    Icon = icon.Icon, Width = 14, Height = 14
                };

                if (!string.IsNullOrWhiteSpace(icon.Tooltip)) {
                    ToolTipEx.SetTip(control, icon.Tooltip);
                }

                sp.Children.Add(control);
            }
        }
    }, (b) => {
        StackPanel sp = (StackPanel) b.Control;
        foreach (Control c in sp.Children) {
            ((IconControl) c).Icon = null;
            ToolTipEx.SetTip(c, AvaloniaProperty.UnsetValue);
        }

        sp.IsVisible = false;
    });

    private readonly ManualBinder<RegisteredConnectionType> displayNameBinder = new ManualBinder<RegisteredConnectionType>(b => ((TextBlock) b.Control).Text = b.Model.DisplayName);
    private readonly ManualBinder<RegisteredConnectionType> footerBinder = new ManualBinder<RegisteredConnectionType>(b => ((TextBlock) b.Control).IsVisible = !string.IsNullOrEmpty(((TextBlock) b.Control).Text = b.Model.FooterText));
    private readonly IBinder<ConnectionTypeEntry> isEnabledBinder = new EventUpdateBinder<ConnectionTypeEntry>(nameof(ConnectionTypeEntry.IsEnabledChanged), b => ((ConsoleTypeListBoxItem) b.Control).IsEnabled = b.Model.IsEnabled);
    
    private IconControl? PART_IconControl;
    private TextBlock? PART_DisplayName, PART_FooterText;
    private StackPanel? PART_PlatformIcons;

    public ConsoleTypeListBoxItem() {
        ToolTipEx.SetTipType(this, typeof(ConsoleTypeToolTip));
        this.isEnabledBinder.AttachControl(this);
        this.AddBinderForModel(this.isEnabledBinder);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_IconControl = e.NameScope.GetTemplateChild<IconControl>(nameof(this.PART_IconControl));
        this.PART_DisplayName = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_DisplayName));
        this.PART_FooterText = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_FooterText));
        this.PART_PlatformIcons = e.NameScope.GetTemplateChild<StackPanel>(nameof(this.PART_PlatformIcons));
        this.iconBinder.AttachControl(this.PART_IconControl);
        this.platformIconsBinder.AttachControl(this.PART_PlatformIcons);
        this.displayNameBinder.AttachControl(this.PART_DisplayName);
        this.footerBinder.AttachControl(this.PART_FooterText);
    }

    protected override void OnAddingToList() {
        RegisteredConnectionType type = this.Model!.Type;
        this.iconBinder.AttachModel(type);
        this.platformIconsBinder.AttachModel(type);
        this.displayNameBinder.AttachModel(type);
        this.footerBinder.AttachModel(type);
    }

    protected override void OnAddedToList() {
    }

    protected override void OnRemovingFromList() {
        this.iconBinder.DetachModel();
        this.platformIconsBinder.DetachModel();
        this.displayNameBinder.DetachModel();
        this.footerBinder.DetachModel();
    }

    protected override void OnRemovedFromList() {
    }
}