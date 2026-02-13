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
using Avalonia.Interactivity;
using Avalonia.Layout;
using PFXToolKitUI.Avalonia.AvControls.ListBoxes;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.Xbox360XBDM.Views;

public class DiscoveredConsoleListBox : ModelBasedListBox<DiscoveredConsole> {
    internal OpenXbdmConnectionView? myConnectionView;

    protected override Type StyleKeyOverride => typeof(ListBox);

    public DiscoveredConsoleListBox() {
    }

    protected override ModelBasedListBoxItem<DiscoveredConsole> CreateItem() {
        return new DiscoveredConsoleListBoxItem();
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);
        this.myConnectionView = VisualTreeUtils.FindLogicalParent<OpenXbdmConnectionView>(this);
    }

    protected override void OnUnloaded(RoutedEventArgs e) {
        base.OnUnloaded(e);
        this.myConnectionView = null;
    }
}

public class DiscoveredConsoleListBoxItem : ModelBasedListBoxItem<DiscoveredConsole> {
    private readonly TextBlock tbName, tbIpAddress;

    protected override Type StyleKeyOverride => typeof(ListBoxItem);

    public DiscoveredConsoleListBoxItem() {
        this.HorizontalAlignment = HorizontalAlignment.Stretch;
        this.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        this.Content = new StackPanel() {
            Orientation = Orientation.Horizontal, Spacing = 5.0,
            Children = {
                (this.tbIpAddress = new TextBlock() { MinWidth = 100.0 }),
                (this.tbName = new TextBlock())
            }
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty && this.IsSelected) {
            this.UpdateViewIpAddressTextBox();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);
        this.UpdateViewIpAddressTextBox();
    }

    private void UpdateViewIpAddressTextBox() {
        ConnectToXboxInfo? info = ((DiscoveredConsoleListBox?) this.ListBox)?.myConnectionView?.ConnectionInfo;
        info?.IpAddress = this.tbIpAddress.Text;
    }

    protected override void OnAddingToList() {
        this.tbName.Text = this.Model!.Name;
        this.tbIpAddress.Text = this.Model!.EndPoint.Address.ToString();
    }

    protected override void OnAddedToList() {
    }

    protected override void OnRemovingFromList() {
    }

    protected override void OnRemovedFromList() {
    }
}