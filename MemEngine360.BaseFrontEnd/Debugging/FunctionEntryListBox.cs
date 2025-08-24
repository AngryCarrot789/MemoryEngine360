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

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MemEngine360.Engine.Debugging;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;

namespace MemEngine360.BaseFrontEnd.Debugging;

public class FunctionEntryListBox : ModelBasedListBox<FunctionCallEntry> {
    protected override Type StyleKeyOverride => typeof(ListBox);
    
    public FunctionEntryListBox() : base(4) {
    }

    protected override ModelBasedListBoxItem<FunctionCallEntry> CreateItem() => new FunctionEntryListBoxItem();
}

// The items are defined in C# code since they're very simple.
// If we want a more complex list box item, we can move to XAML at some point.
public class FunctionEntryListBoxItem : ModelBasedListBoxItem<FunctionCallEntry> {
    protected override Type StyleKeyOverride => typeof(ListBoxItem);

    private readonly TextBlock tbThreadName;
    private readonly TextBlock tbFunctionSize;
    private readonly TextBlock tbUnwindInfo;

    public FunctionEntryListBoxItem() {
        this.tbThreadName = new TextBlock() { TextDecorations = TextDecorations.Underline };
        this.tbFunctionSize = new TextBlock();
        this.tbUnwindInfo = new TextBlock();

        this.Content = new StackPanel() {
            Children = {
                this.tbThreadName,
                this.tbFunctionSize,
                this.tbUnwindInfo
            }
        };

        this.HorizontalContentAlignment = HorizontalAlignment.Left;
    }

    protected override void OnAddingToList() {
    }

    protected override void OnAddedToList() {
        this.tbThreadName.Text   = this.Model!.ModuleName + "!" + this.Model!.Address.ToString("X8");
        this.tbFunctionSize.Text = "Function size: " + this.Model!.Size.ToString("X");
        this.tbUnwindInfo.Text   = "Unwind Info: " + this.Model!.unwindInfoAddressOrData.ToString("X8");
    }

    protected override void OnRemovingFromList() {
    }

    protected override void OnRemovedFromList() {
    }
}