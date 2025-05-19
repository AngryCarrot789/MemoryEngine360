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
using MemEngine360.XboxBase.Modules;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.BaseFrontEnd.XboxBase.Modules;

public class ModuleListBoxItem : ModelBasedListBoxItem<XboxModule> {
    private readonly IBinder<XboxModule> shortNameBinder = new EventPropertyBinder<XboxModule>(nameof(XboxModule.NameChanged), (b) => ((TextBlock) b.Control).Text = b.Model.Name);
    private readonly IBinder<XboxModule> longNameBinder = new EventPropertyBinder<XboxModule>(nameof(XboxModule.FullNameChanged), (b) => ((TextBlock) b.Control).Text = b.Model.FullName);

    private TextBlock? PART_HeaderText;
    private TextBlock? PART_FooterText;

    public ModuleListBoxItem() {
        this.AddBinderForModel(this.shortNameBinder, this.longNameBinder);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);
        this.PART_HeaderText = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_HeaderText));
        this.shortNameBinder.AttachControl(this.PART_HeaderText);

        this.PART_FooterText = e.NameScope.GetTemplateChild<TextBlock>(nameof(this.PART_FooterText));
        this.longNameBinder.AttachControl(this.PART_FooterText);
    }

    protected override void OnAddingToList() {
    }

    protected override void OnAddedToList() {
    }

    protected override void OnRemovingFromList() {
    }

    protected override void OnRemovedFromList() {
    }
}