// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of PFXToolKitUI.
// 
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3 of the License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with PFXToolKitUI. If not, see <https://www.gnu.org/licenses/>.
// 

using Avalonia.Controls;
using MemEngine360.Engine.Events;
using MemEngine360.Engine.Events.XbdmEvents;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;

namespace MemEngine360.BaseFrontEnd.EventViewing.XbdmEvents;

public class ConsoleEventListBox : ModelBasedListBox<ConsoleSystemEventArgs> {
    protected override Type StyleKeyOverride => typeof(ListBox);

    public ConsoleEventListBox() : base(1020) {
    }

    protected override ModelBasedListBoxItem<ConsoleSystemEventArgs> CreateItem() {
        return new ConsoleEventListBoxItem();
    }
}

public class ConsoleEventListBoxItem : ModelBasedListBoxItem<ConsoleSystemEventArgs> {
    private readonly TextBlock tb;
    
    protected override Type StyleKeyOverride => typeof(ListBoxItem);

    public ConsoleEventListBoxItem() {
        this.Content = this.tb = new TextBlock();
        this.Height = 20;
    }

    static ConsoleEventListBoxItem() {
        RequestBringIntoViewEvent.AddClassHandler<ConsoleEventListBoxItem>(OnRequestBringIntoView);
    }

    private static void OnRequestBringIntoView(ConsoleEventListBoxItem arg1, RequestBringIntoViewEventArgs arg2) {
        arg2.Handled = true;
    }

    protected override void OnAddingToList() {
    }

    protected override void OnAddedToList() {
        string? newText = (this.Model as XbdmEventArgs)?.RawMessage ?? this.Model!.ToString();
        
        if (string.IsNullOrWhiteSpace(this.tb.Text) != string.IsNullOrWhiteSpace(newText) || newText != this.tb.Text)
            this.tb.Text = newText;
    }

    protected override void OnRemovingFromList() {
        // this.tb.Text = "";
    }

    protected override void OnRemovedFromList() {
    }
}