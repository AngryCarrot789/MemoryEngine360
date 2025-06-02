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

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using MemEngine360.Connections;
using PFXToolKitUI.Avalonia.AvControls;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.BaseFrontEnd.Services.Connectivity;

public class ConsoleTypeListBoxItem : ListBoxItem {
    private readonly ManualBinder<RegisteredConnectionType> iconBinder = new ManualBinder<RegisteredConnectionType>(b => {
        ((IconControl) b.Control).IsVisible = (((IconControl) b.Control).Icon = b.Model.Icon) != null;
    }, (b) => {
        ((IconControl) b.Control).Icon = null;
    });

    private readonly ManualBinder<RegisteredConnectionType> displayNameBinder = new ManualBinder<RegisteredConnectionType>(b => ((TextBlock) b.Control).Text = b.Model.DisplayName);
    private readonly ManualBinder<RegisteredConnectionType> footerBinder = new ManualBinder<RegisteredConnectionType>(b => ((TextBlock) b.Control).IsVisible = !string.IsNullOrEmpty(((TextBlock) b.Control).Text = b.Model.FooterText));
    private IconControl? PART_IconControl;
    private TextBlock? PART_DisplayName, PART_FooterText;

    public RegisteredConnectionType RegisteredConsoleType { get; }

    internal IContextData? ContextData { get; set; }

    public UserConnectionInfo? UserConnectionInfo { get; private set; }

    [Unstable("Throws. Use the other CTOR")]
    public ConsoleTypeListBoxItem() {
        if (!Design.IsDesignMode)
            throw new InvalidOperationException("Use the other constructor");
    }

    public ConsoleTypeListBoxItem(RegisteredConnectionType type, IContextData context) {
        this.ContextData = context;
        this.RegisteredConsoleType = type;
        this.UserConnectionInfo = type.CreateConnectionInfo(this.ContextData ?? EmptyContext.Instance);

        this.iconBinder.AttachModel(type);
        this.displayNameBinder.AttachModel(type);
        this.footerBinder.AttachModel(type);
        ToolTip.SetTip(this, type.RegisteredId);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_IconControl = e.NameScope.GetTemplateChild<IconControl>(nameof(this.PART_IconControl));
        this.PART_DisplayName = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_DisplayName));
        this.PART_FooterText = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_FooterText));
        this.iconBinder.AttachControl(this.PART_IconControl);
        this.displayNameBinder.AttachControl(this.PART_DisplayName);
        this.footerBinder.AttachControl(this.PART_FooterText);
    }

    public void OnRemoving() {
        this.iconBinder.DetachModel();
        this.displayNameBinder.DetachModel();
        this.footerBinder.DetachModel();
    }
}