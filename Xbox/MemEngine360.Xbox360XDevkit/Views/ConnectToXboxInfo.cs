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

using MemEngine360.Configs;
using MemEngine360.Connections;
using PFXToolKitUI.DataTransfer;
using PFXToolKitUI.Utils.Accessing;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Xbox360XDevkit.Views;

public class ConnectToXboxInfo : UserConnectionInfo {
    public static readonly DataParameterString IpAddressParameter = DataParameter.Register(new DataParameterString(typeof(ConnectToXboxInfo), nameof(IpAddress), "", ValueAccessors.Reflective<string?>(typeof(ConnectToXboxInfo), nameof(ipAddress))));

    private string? ipAddress;

    public string? IpAddress {
        get => this.ipAddress;
        set => DataParameter.SetValueHelper(this, IpAddressParameter, ref this.ipAddress, value);
    }

    public bool IsLittleEndian {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsLittleEndianChanged);
    } /* = false // xbox 360 is big endian */

    public event EventHandler? IsLittleEndianChanged;

    public ConnectToXboxInfo() : base(ConnectionTypeXbox360XDevkit.Instance) {
        // Try get last entered IP address. Helps with debugging and user experience ;)
        string lastIp = BasicApplicationConfiguration.Instance.LastXboxHostName;
        if (string.IsNullOrWhiteSpace(lastIp)) {
            lastIp = "192.168.1.";
        }

        this.IpAddress = lastIp;
    }

    protected override void OnShown() {
    }

    protected override void OnHidden() {
    }
}