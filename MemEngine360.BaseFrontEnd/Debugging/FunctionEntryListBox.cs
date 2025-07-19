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

public class FunctionEntryListBoxItem : ModelBasedListBoxItem<FunctionCallEntry> {
    protected override Type StyleKeyOverride => typeof(ListBoxItem);

    private readonly TextBlock tbThreadName;
    private readonly TextBlock tbFunctionSize;

    public FunctionEntryListBoxItem() {
        this.tbThreadName = new TextBlock() { TextDecorations = TextDecorations.Underline };
        this.tbFunctionSize = new TextBlock();

        this.Content = new StackPanel() {
            Children = {
                this.tbThreadName,
                this.tbFunctionSize
            }
        };

        this.HorizontalContentAlignment = HorizontalAlignment.Left;
    }

    protected override void OnAddingToList() {
    }

    protected override void OnAddedToList() {
        this.tbThreadName.Text = this.Model!.ModuleName + "!" + this.Model!.Address.ToString("X8");
        this.tbFunctionSize.Text = "Size: " + this.Model!.Size.ToString("X");
    }

    protected override void OnRemovingFromList() {
    }

    protected override void OnRemovedFromList() {
    }
}