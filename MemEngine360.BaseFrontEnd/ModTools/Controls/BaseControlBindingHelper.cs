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

namespace MemEngine360.BaseFrontEnd.ModTools.Controls;

public sealed class BaseControlBindingHelper {
    private BaseMTElement? element;

    public Control Control { get; }

    private readonly IBinder<BaseMTElement> horzAlignBinder = new EventUpdateBinder<BaseMTElement>(nameof(BaseMTElement.HorizontalAlignmentChanged), b => b.Control.HorizontalAlignment = (HorizontalAlignment) b.Model.HorizontalAlignment);
    private readonly IBinder<BaseMTElement> vertAlignBinder = new EventUpdateBinder<BaseMTElement>(nameof(BaseMTElement.VerticalAlignmentChanged), b => b.Control.VerticalAlignment = (VerticalAlignment) b.Model.VerticalAlignment);
    private readonly IBinder<BaseMTElement> widthBinder = new EventUpdateBinder<BaseMTElement>(nameof(BaseMTElement.WidthChanged), b => SetDoubleProperty(b.Control, Layoutable.WidthProperty, b.Model.Width));
    private readonly IBinder<BaseMTElement> heightBinder = new EventUpdateBinder<BaseMTElement>(nameof(BaseMTElement.HeightChanged), b => SetDoubleProperty(b.Control, Layoutable.HeightProperty, b.Model.Height));
    private readonly IBinder<BaseMTElement> minWidthBinder = new EventUpdateBinder<BaseMTElement>(nameof(BaseMTElement.MinWidthChanged), b => SetDoubleProperty(b.Control, Layoutable.MinWidthProperty, b.Model.MinWidth));
    private readonly IBinder<BaseMTElement> minHeightBinder = new EventUpdateBinder<BaseMTElement>(nameof(BaseMTElement.MinHeightChanged), b => SetDoubleProperty(b.Control, Layoutable.MinHeightProperty, b.Model.MinHeight));
    private readonly IBinder<BaseMTElement> maxWidthBinder = new EventUpdateBinder<BaseMTElement>(nameof(BaseMTElement.MaxWidthChanged), b => SetDoubleProperty(b.Control, Layoutable.MaxWidthProperty, b.Model.MaxWidth));
    private readonly IBinder<BaseMTElement> maxHeightBinder = new EventUpdateBinder<BaseMTElement>(nameof(BaseMTElement.MaxHeightChanged), b => SetDoubleProperty(b.Control, Layoutable.MaxHeightProperty, b.Model.MaxHeight));

    public BaseControlBindingHelper(Control control) {
        this.Control = control;
        Binders.AttachControls(control, this.horzAlignBinder, this.vertAlignBinder, this.widthBinder, this.heightBinder, this.minWidthBinder, this.minHeightBinder, this.maxWidthBinder, this.maxHeightBinder);
    }

    public void SetModel(BaseMTElement? newElement) {
        if (this.element != newElement) {
            this.element = newElement;

            this.horzAlignBinder.SwitchModel(newElement);
            this.vertAlignBinder.SwitchModel(newElement);
            this.widthBinder.SwitchModel(newElement);
            this.heightBinder.SwitchModel(newElement);
            this.minWidthBinder.SwitchModel(newElement);
            this.minHeightBinder.SwitchModel(newElement);
            this.maxWidthBinder.SwitchModel(newElement);
            this.maxHeightBinder.SwitchModel(newElement);
        }
    }
    
    private static void SetDoubleProperty(Control c, StyledProperty<double> property, double? value) {
        if (value.HasValue) {
            c.SetCurrentValue(property, value.Value);
        }
        else {
            c.ClearValue(property);
        }
    }
}