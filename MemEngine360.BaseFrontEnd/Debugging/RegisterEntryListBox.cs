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

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using MemEngine360.Engine.Debugging;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;

namespace MemEngine360.BaseFrontEnd.Debugging;

public class RegisterEntryListBox : ModelBasedListBox<RegisterEntry> {
    public static readonly StyledProperty<ConsoleDebugger?> ConsoleDebuggerProperty = AvaloniaProperty.Register<RegisterEntryListBox, ConsoleDebugger?>(nameof(ConsoleDebugger));

    public ConsoleDebugger? ConsoleDebugger {
        get => this.GetValue(ConsoleDebuggerProperty);
        set => this.SetValue(ConsoleDebuggerProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ListBox);

    public RegisterEntryListBox() : base(48) {
    }

    protected override ModelBasedListBoxItem<RegisterEntry> CreateItem() {
        return new RegisterEntryListBoxItem();
    }
}

public class RegisterEntryListBoxItem : ModelBasedListBoxItem<RegisterEntry> {
    protected override Type StyleKeyOverride => typeof(ListBoxItem);
    private readonly TextBlock tbRegisterName;
    private readonly TextBox tboxRegisterValue;

    public RegisterEntryListBoxItem() {
        this.Padding = default;
        this.tbRegisterName = new TextBlock() {
            Padding = new Thickness(3, 0),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Left,
            [Grid.ColumnProperty] = 0,
        };

        this.tboxRegisterValue = new TextBox() {
            Padding = new Thickness(3, 0),
            Background = Brushes.Transparent,
            BorderThickness = default,
            IsReadOnly = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            [Grid.ColumnProperty] = 1,
        };

        this.Content = new Grid() {
            ColumnDefinitions = new ColumnDefinitions("60,Auto"),
            Children = {
                this.tbRegisterName,
                this.tboxRegisterValue
            }
        };

        this.HorizontalContentAlignment = HorizontalAlignment.Left;
        this.AddHandler(PointerPressedEvent, this.OnPreviewPointerPressed, RoutingStrategies.Tunnel);
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e) {
        this.IsSelected = true;
    }

    protected override void OnAddingToList() {
    }

    protected override void OnAddedToList() {
        this.tbRegisterName.Text = this.Model!.Name;
        string textValue;
        switch (this.Model) {
            case RegisterEntry32 e32: textValue = e32.Value.ToString("X8"); break;
            case RegisterEntry64 e64: textValue = (e64.Value >> 32 & uint.MaxValue).ToString("X8") + (e64.Value & uint.MaxValue).ToString("X8"); break;
            case RegisterEntryDouble eDouble:
                bool isLittleEndian = ((RegisterEntryListBox?) this.ListBox)?.ConsoleDebugger?.Connection?.IsLittleEndian ?? true;
                ulong value = eDouble.Value;
                if (!isLittleEndian)
                    value = BinaryPrimitives.ReverseEndianness(value);

                textValue = new StringBuilder().Append(" = ").Append(eDouble.Value.ToString("X16")).Append(" (").Append(Unsafe.As<ulong, double>(ref value)).ToString();
                break;
            case RegisterEntryVector eVector: textValue = new StringBuilder().Append(" = ").Append(eVector.Value1.ToString("X16")).Append(", ").Append(eVector.Value2.ToString("X16")).ToString(); break;
            default:                          textValue = ""; break;
        }

        this.tboxRegisterValue.Text = textValue;
    }

    protected override void OnRemovingFromList() {
    }

    protected override void OnRemovedFromList() {
    }
}