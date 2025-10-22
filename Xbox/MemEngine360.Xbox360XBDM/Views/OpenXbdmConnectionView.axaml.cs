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
using MemEngine360.BaseFrontEnd.Services.Connectivity;
using MemEngine360.Connections;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;

namespace MemEngine360.Xbox360XBDM.Views;

public partial class OpenXbdmConnectionView : UserControl, IConsoleConnectivityControl {
    private readonly IBinder<ConnectToXboxInfo> ipBinder = new TextBoxToEventPropertyBinder<ConnectToXboxInfo>(nameof(ConnectToXboxInfo.IpAddressChanged), b => b.Model.IpAddress ?? "", (b, s) => {
        b.Model.IpAddress = s;
        return Task.FromResult(true);
    });
    
    private readonly IBinder<ConnectToXboxInfo> isLittleEndianBinder = new AvaloniaPropertyToEventPropertyBinder<ConnectToXboxInfo>(CheckBox.IsCheckedProperty, nameof(ConnectToXboxInfo.IsLittleEndianChanged), b => ((CheckBox) b.Control).IsChecked = b.Model.IsLittleEndian, b => b.Model.IsLittleEndian = ((CheckBox) b.Control).IsChecked == true);

    public ConnectToXboxInfo? ConnectionInfo { get; private set; }

    public OpenXbdmConnectionView() {
        this.InitializeComponent();
    }

    public void OnConnected(OpenConnectionView dialog, UserConnectionInfo info) {
        this.ipBinder.Attach(this.PART_IpAddressTextBox, this.ConnectionInfo = (ConnectToXboxInfo) info);
        this.isLittleEndianBinder.Attach(this.PART_IsLittleEndian, this.ConnectionInfo);
        this.PART_DiscoveredConsoles.SetItemsSource(this.ConnectionInfo.DiscoveredConsoles);
    }

    public void OnDisconnected() {
        this.ipBinder.Detach();
        this.isLittleEndianBinder.Detach();
        this.PART_DiscoveredConsoles.SetItemsSource(null);
        this.ConnectionInfo = null;
    }
}