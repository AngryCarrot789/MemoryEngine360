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
using MemEngine360.ModTools.Gui;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils;

namespace MemEngine360.BaseFrontEnd.ModTools.Controls;

public class ControlMTTextBlock : TextBlock {
    public static readonly StyledProperty<MTTextBlock?> MTTextBlockProperty = AvaloniaProperty.Register<ControlMTTextBlock, MTTextBlock?>(nameof(MTTextBlock));

    public MTTextBlock? MTTextBlock {
        get => this.GetValue(MTTextBlockProperty);
        set => this.SetValue(MTTextBlockProperty, value);
    }
    
    private readonly IBinder<MTTextBlock> textBinder = new EventUpdateBinder<MTTextBlock>(nameof(MTTextBlock.TextChanged), (b) => ((ControlMTTextBlock) b.Control).Text = b.Model.Text);
    private readonly IBinder<MTTextBlock> horzAlignBinder = new EventUpdateBinder<MTTextBlock>(nameof(BaseMTElement.HorizontalAlignmentChanged), (b) => b.Control.HorizontalAlignment = (HorizontalAlignment) b.Model.HorizontalAlignment);
    private readonly IBinder<MTTextBlock> vertAlignBinder = new EventUpdateBinder<MTTextBlock>(nameof(BaseMTElement.VerticalAlignmentChanged), (b) => b.Control.VerticalAlignment = (VerticalAlignment) b.Model.VerticalAlignment);
    private readonly AttachedModelHelper<MTTextBlock> fullyLoadedHelper;

    protected override Type StyleKeyOverride => typeof(TextBlock);
    
    public ControlMTTextBlock() {
        Binders.AttachControls(this, this.textBinder, this.horzAlignBinder, this.vertAlignBinder);
        this.fullyLoadedHelper = new AttachedModelHelper<MTTextBlock>(this, this.OnIsFullyLoadedChanged);
        this.Padding = new Thickness(4, 2);
    }

    static ControlMTTextBlock() {
        MTTextBlockProperty.Changed.AddClassHandler<ControlMTTextBlock, MTTextBlock?>((s, e) => s.OnMTTextBlockChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnMTTextBlockChanged(MTTextBlock? oldValue, MTTextBlock? newValue) {
        this.fullyLoadedHelper.Model = Optionals.OfNullable(newValue);
    }
    
    private void OnIsFullyLoadedChanged(MTTextBlock tb, bool attached) {
        this.textBinder.SwitchModel(attached ? tb : null);
        this.horzAlignBinder.SwitchModel(attached ? tb : null);
        this.vertAlignBinder.SwitchModel(attached ? tb : null);
    }
}