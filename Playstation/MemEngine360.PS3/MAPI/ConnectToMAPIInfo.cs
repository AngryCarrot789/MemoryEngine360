// 
// Copyright (c) 2026-2026 REghZy
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
using MemEngine360.PS3.CCAPI;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.PS3.MAPI;

public class ConnectToMAPIInfo : UserConnectionInfo {
    public string IpAddress {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IpAddressChanged);
    }

    public int Port {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, Math.Clamp(value, 0, 65535), this, this.PortChanged);
    } = 7887;

    public event EventHandler? PortChanged;

    public event EventHandler? IpAddressChanged;
    
    // private CancellationTokenSource? ctsApiRun;

    public ConsoleControlAPI? CCApi { get; set; }
    
    public ConnectToMAPIInfo() : base(ConnectionTypePS3MAPI.Instance) {
        // Try get last entered IP address. Helps with debugging and user experience ;)
        string lastIp = BasicApplicationConfiguration.Instance.LastPS3HostName;
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