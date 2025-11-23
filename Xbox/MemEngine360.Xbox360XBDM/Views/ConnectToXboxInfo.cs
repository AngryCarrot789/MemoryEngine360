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
using MemEngine360.Xbox360XBDM.Consoles;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Xbox360XBDM.Views;

public class ConnectToXboxInfo : UserConnectionInfo {
    public string? IpAddress {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IpAddressChanged);
    }

    public bool IsLittleEndian {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsLittleEndianChanged);
    } /* = false // xbox is BE by default */

    public ObservableList<DiscoveredConsole> DiscoveredConsoles { get; } = new ObservableList<DiscoveredConsole>();

    public event EventHandler? IpAddressChanged;
    public event EventHandler? IsLittleEndianChanged;

    private CancellationTokenSource? refreshCts;
    private ActivityTask? lastRefreshConsolesTask;
    private volatile bool hasCompletedDiscovery;

    public ConnectToXboxInfo() : base(ConnectionTypeXbox360Xbdm.Instance) {
        // Try get last entered IP address. Helps with debugging and user experience ;)
        string lastIp = BasicApplicationConfiguration.Instance.LastXboxHostName;
        if (string.IsNullOrWhiteSpace(lastIp)) {
            lastIp = "192.168.1.";
        }

        this.IpAddress = lastIp;
    }

    protected override void OnShown() {
        if (this.hasCompletedDiscovery) {
            return;
        }
        
        // Debug.Assert(this.refreshCts == null);
        // Debug.Assert(this.lastRefreshConsolesTask == null);
        //
        // this.refreshCts = new CancellationTokenSource();
        // this.lastRefreshConsolesTask = ActivityManager.Instance.RunTask(async () => {
        //     ActivityTask activity = ActivityTask.Current;
        //
        //     using UdpClient client = new UdpClient();
        //     client.EnableBroadcast = true;
        //     await client.SendAsync(new byte[] { 0x03, 0x00 }, new IPEndPoint(IPAddress.Broadcast, 730), activity.CancellationToken);
        //     while (true) {
        //         using CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(2000));
        //         using CancellationTokenSource actualCts = CancellationTokenSource.CreateLinkedTokenSource(activity.CancellationToken, timeoutCts.Token);
        //
        //         UdpReceiveResult result;
        //         try {
        //             result = await client.ReceiveAsync(actualCts.Token);
        //         }
        //         catch (OperationCanceledException e) {
        //             if (e.CancellationToken == timeoutCts.Token || timeoutCts.IsCancellationRequested) {
        //                 // Timeout, nothing responded in enough time
        //                 break;
        //             }
        //             else {
        //                 await ApplicationPFX.Instance.Dispatcher.InvokeAsync(void () => this.DiscoveredConsoles.Clear(), token: CancellationToken.None);
        //                 return;
        //             }
        //         }
        //         catch {
        //             return;
        //         }
        //
        //         if (DiscoveredConsole.TryParse(result.Buffer, result.RemoteEndPoint, out DiscoveredConsole? console)) {
        //             await ApplicationPFX.Instance.Dispatcher.InvokeAsync(void () => this.DiscoveredConsoles.Add(console), token: CancellationToken.None);
        //         }
        //     }
        //
        //     this.hasCompletedDiscovery = true;
        // }, this.refreshCts);
    }

    protected override void OnHidden() {
        // this.lastRefreshConsolesTask?.TryCancel();
        // this.lastRefreshConsolesTask = null;
        // this.refreshCts?.Dispose();
        // this.refreshCts = null;
    }
}