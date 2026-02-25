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

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using MemEngine360.ModTools.Gui;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils;

namespace MemEngine360.BaseFrontEnd.ModTools.Controls;

public class ControlMTButton : Button {
    public static readonly StyledProperty<MTButton?> MTButtonProperty = AvaloniaProperty.Register<ControlMTButton, MTButton?>(nameof(MTButton));

    public MTButton? MTButton {
        get => this.GetValue(MTButtonProperty);
        set => this.SetValue(MTButtonProperty, value);
    }

    private readonly IBinder<MTButton> textBinder = new EventUpdateBinder<MTButton>(nameof(MTButton.TextChanged), (b) => ((Button) b.Control).Content = b.Model.Text);
    private readonly IBinder<MTButton> horzAlignBinder = new EventUpdateBinder<MTButton>(nameof(BaseMTElement.HorizontalAlignmentChanged), (b) => b.Control.HorizontalAlignment = (HorizontalAlignment) b.Model.HorizontalAlignment);
    private readonly IBinder<MTButton> vertAlignBinder = new EventUpdateBinder<MTButton>(nameof(BaseMTElement.VerticalAlignmentChanged), (b) => b.Control.VerticalAlignment = (VerticalAlignment) b.Model.VerticalAlignment);
    private readonly BaseControlBindingHelper baseBindingHelper;
    private readonly AttachedModelHelper<MTButton> fullyLoadedHelper;
    private bool isPressed;

    protected override Type StyleKeyOverride => typeof(Button);

    public ControlMTButton() {
        Binders.AttachControls(this, this.textBinder, this.horzAlignBinder, this.vertAlignBinder);
        this.baseBindingHelper = new BaseControlBindingHelper(this);
        this.fullyLoadedHelper = new AttachedModelHelper<MTButton>(this, this.OnIsFullyLoadedChanged);
        this.Padding = new Thickness(4, 2);
    }

    static ControlMTButton() {
        MTButtonProperty.Changed.AddClassHandler<ControlMTButton, MTButton?>((s, e) => s.OnMTButtonChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) {
            return;
        }

        e.Handled = true;
        this.isPressed = true;
        if (this.fullyLoadedHelper.TryGetModel(out MTButton? model)) {
            model.OnPressed();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e) {
        base.OnPointerReleased(e);
        if (!this.isPressed || e.InitialPressMouseButton != MouseButton.Left) {
            return;
        }

        e.Handled = true;
        this.isPressed = false;
        if (this.fullyLoadedHelper.TryGetModel(out MTButton? model)) {
            // Unlike avalonia, we don't care if the user un-pressed their LMB after moving the
            // cursor away from the bounds, because we need to notify the lua machine to stop
            // looping the hold function
            // if (this.GetVisualsAt(e.GetPosition(this)).Any(c => this == c || this.IsVisualAncestorOf(c))) {
            model.OnReleased();
            // }
        }
    }

    private void OnMTButtonChanged(MTButton? oldValue, MTButton? newValue) {
        this.fullyLoadedHelper.Model = Optionals.OfNullable(newValue);
    }

    private void OnIsFullyLoadedChanged(MTButton button, bool attached) {
        this.textBinder.SwitchModel(attached ? button : null);
        this.baseBindingHelper.SetModel(attached ? button : null);
        if (!attached && this.isPressed) {
            this.isPressed = false;
            button.OnReleased();
        }
    }
}