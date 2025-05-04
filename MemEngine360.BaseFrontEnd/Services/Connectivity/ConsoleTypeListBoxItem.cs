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
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.Avalonia.AvControls;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.Avalonia.Services.Connectivity;

public class ConsoleTypeListBoxItem : ListBoxItem {
    private IconControl? PART_IconControl;
    private TextBlock? PART_DisplayName;
    private RegisteredConsoleType? myConsoleType;

    public RegisteredConsoleType? RegisteredConsoleType {
        get => this.myConsoleType;
        set {
            if (ReferenceEquals(value, this.myConsoleType)) {
                return;
            }

            if (this.myConsoleType != null) {
                if (this.PART_IconControl != null)
                    this.PART_IconControl.Icon = null; // cleans up event handlers
                this.UserConnectionInfo?.OnDestroyed();
            }

            this.myConsoleType = value;
            this.UserConnectionInfo = value?.CreateConnectionInfo(this.Engine!);
            this.UserConnectionInfo?.OnCreated();
        }
    }
    
    public MemoryEngine360? Engine { get; set; }

    public UserConnectionInfo? UserConnectionInfo { get; private set; }

    public ConsoleTypeListBoxItem() {
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_IconControl = e.NameScope.GetTemplateChild<IconControl>(nameof(this.PART_IconControl));
        this.PART_DisplayName = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_DisplayName));
        this.PART_IconControl!.Icon = this.RegisteredConsoleType?.Icon;
        this.PART_DisplayName!.Text = this.RegisteredConsoleType?.DisplayName ?? "";

        if (this.PART_IconControl.Icon == null) {
            this.PART_IconControl.IsVisible = false;
        }
    }
}