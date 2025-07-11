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
using Avalonia.Layout;
using Avalonia.Media;
using MemEngine360.Engine.Debugging;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;

namespace MemEngine360.BaseFrontEnd.Debugging;

public class ThreadEntryListBox : ModelBasedListBox<ThreadEntry> {
    protected override Type StyleKeyOverride => typeof(ListBox);

    public static readonly StyledProperty<ConsoleDebugger?> ConsoleDebuggerProperty = AvaloniaProperty.Register<ThreadEntryListBox, ConsoleDebugger?>(nameof(ConsoleDebugger));

    public ConsoleDebugger? ConsoleDebugger {
        get => this.GetValue(ConsoleDebuggerProperty);
        set => this.SetValue(ConsoleDebuggerProperty, value);
    }
    
    public ThreadEntryListBox() : base(48) {
    }

    protected override ModelBasedListBoxItem<ThreadEntry> CreateItem() {
        return new ThreadEntryListBoxItem();
    }
}

public class ThreadEntryListBoxItem : ModelBasedListBoxItem<ThreadEntry> {
    protected override Type StyleKeyOverride => typeof(ListBoxItem);

    private readonly TextBlock tbThreadName;
    private readonly TextBlock tbThreadId;
    private readonly TextBlock tbFooter;

    public ThreadEntryListBoxItem() {
        this.tbThreadName = new TextBlock() { TextDecorations = TextDecorations.Underline };
        this.tbThreadId = new TextBlock();
        this.tbFooter = new TextBlock();

        this.Content = new StackPanel() {
            Children = {
                new StackPanel() {
                    Orientation = Orientation.Horizontal,
                    Spacing = 3,
                    Children = {
                        this.tbThreadId,
                        this.tbThreadName
                    }
                },
                this.tbFooter
            }
        };

        this.HorizontalContentAlignment = HorizontalAlignment.Left;
    }

    protected override void OnAddingToList() {
    }

    protected override void OnAddedToList() {
        string? name = this.Model!.ThreadName;
        this.tbThreadName.Text = !string.IsNullOrWhiteSpace(name) ? name : null;
        this.tbThreadName.Opacity = string.IsNullOrWhiteSpace(name) ? 0 : 0.6;
        this.tbThreadId.Text = this.Model!.ThreadId.ToString("X8");

        this.tbFooter.Text = $"Base={this.Model.BaseAddress:X8} ({(this.Model.IsSuspended ? "Suspended" : "Running")})"; 
        
        // this.Content = $"[CPU {this.Model!.ProcessorNumber}] {this.Model!.ThreadId:X8}{(!string.IsNullOrWhiteSpace(name) ? $" ({name})" : "")}{Environment.NewLine}" +
        //                $"Base={this.Model.BaseAddress:X8} ({(this.Model.IsSuspended ? "Suspended" : "Running")})";
    }

    protected override void OnRemovingFromList() {
    }

    protected override void OnRemovedFromList() {
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);
        if (this.IsSelected && ((ThreadEntryListBox?) this.ListBox)?.ConsoleDebugger is ConsoleDebugger debug) {
            debug.UpdateLaterForSelectedThreadChanged();
        }
    }
}