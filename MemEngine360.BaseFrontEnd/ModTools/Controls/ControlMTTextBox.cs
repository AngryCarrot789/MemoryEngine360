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
using Avalonia.Input;
using MemEngine360.Engine;
using MemEngine360.ModTools.Gui;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.BaseFrontEnd.ModTools.Controls;

public class ControlMTTextBox : TextBox {
    public static readonly StyledProperty<MTTextBox?> MTTextBoxProperty = AvaloniaProperty.Register<ControlMTTextBox, MTTextBox?>(nameof(MTTextBox));

    public MTTextBox? MTTextBox {
        get => this.GetValue(MTTextBoxProperty);
        set => this.SetValue(MTTextBoxProperty, value);
    }

    private readonly TextBoxToEventPropertyBinder<MTTextBox> textBinder = new TextBoxToEventPropertyBinder<MTTextBox>(nameof(MTTextBox.ContentChanged), (b) => b.Model.GetReadableText(), static (b, s) => ParseAndUpdateContentFromText(b.Model, s));
    private readonly IBinder<MTTextBox> leftContentBinder = new EventUpdateBinder<MTTextBox>(nameof(MTTextBox.LeftContentChanged), (b) => ((ControlMTTextBox) b.Control).SetValue(InnerLeftContentProperty, string.IsNullOrEmpty(b.Model.LeftContent) ? AvaloniaProperty.UnsetValue : b.Model.LeftContent));
    private readonly IBinder<MTTextBox> rightContentBinder = new EventUpdateBinder<MTTextBox>(nameof(MTTextBox.RightContentChanged), (b) => ((ControlMTTextBox) b.Control).SetValue(InnerRightContentProperty, string.IsNullOrEmpty(b.Model.RightContent) ? AvaloniaProperty.UnsetValue : b.Model.RightContent));
    private readonly BaseControlBindingHelper baseBindingHelper;
    private readonly AttachedModelHelper<MTTextBox> fullyLoadedHelper;

    protected override Type StyleKeyOverride => typeof(TextBox);

    public ControlMTTextBox() {
        Binders.AttachControls(this, this.textBinder, this.leftContentBinder, this.rightContentBinder);
        this.baseBindingHelper = new BaseControlBindingHelper(this);
        this.fullyLoadedHelper = new AttachedModelHelper<MTTextBox>(this, this.OnIsFullyLoadedChanged);
        this.Padding = new Thickness(4, 2);
    }

    static ControlMTTextBox() {
        MTTextBoxProperty.Changed.AddClassHandler<ControlMTTextBox, MTTextBox?>((s, e) => s.OnMTTextBoxChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private static CancellationToken GetTokenFromControl(BaseMTElement element) {
        return element.GUI?.ModTool.Machine?.CancellationToken ?? CancellationToken.None;
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        this.MTTextBox?.OnKeyPress(true, e.KeySymbol ?? "", (int) e.Key);
    }

    protected override void OnKeyUp(KeyEventArgs e) {
        base.OnKeyUp(e);
        this.MTTextBox?.OnKeyPress(false, e.KeySymbol ?? "", (int) e.Key);
    }

    private void OnMTTextBoxChanged(MTTextBox? oldValue, MTTextBox? newValue) {
        this.fullyLoadedHelper.Model = Optionals.OfNullable(newValue);
    }

    private void OnIsFullyLoadedChanged(MTTextBox tb, bool attached) {
        this.baseBindingHelper.SetModel(attached ? tb : null);
        this.leftContentBinder.SwitchModel(attached ? tb : null);
        this.rightContentBinder.SwitchModel(attached ? tb : null);
        this.textBinder.SwitchModel(attached ? tb : null);
    }
    
    private static async Task<bool> ParseAndUpdateContentFromText(MTTextBox tb, string text) {
        switch (tb.ContentType) {
            case MTTextBox.EnumContentType.Text: {
                tb.SetText(text);
                return true;
            }
            case MTTextBox.EnumContentType.UInt32: {
                if (!AddressParsing.TryParse32(text, out uint result, out string? error, canParseAsExpression: true)) {
                    await IMessageDialogService.Instance.ShowMessage("Invalid address", error, icon: MessageBoxIcons.ErrorIcon, dialogCancellation: GetTokenFromControl(tb));
                    return false;
                }

                tb.SetUInt32(result);
                return true;
            }
            case MTTextBox.EnumContentType.Number: {
                if (!double.TryParse(text, out double value)) {
                    await IMessageDialogService.Instance.ShowMessage("Invalid number", "Invalid number", icon: MessageBoxIcons.ErrorIcon, defaultButton: MessageBoxResult.OK, dialogCancellation: GetTokenFromControl(tb));
                    return false;
                }

                tb.SetNumber(value);
                return true;
            }
            default: throw new ArgumentOutOfRangeException();
        }
    }
}