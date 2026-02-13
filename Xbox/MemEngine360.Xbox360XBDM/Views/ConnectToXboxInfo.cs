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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Avalonia.Controls.ApplicationLifetimes;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Xbox360XBDM.Consoles;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Xbox360XBDM.Views;

public class ConnectToXboxInfo : UserConnectionInfo {
    public static bool IsDiscoveryEnabled = true;

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

        if (!IsDiscoveryEnabled) {
            return;
        }

        Debug.Assert(this.refreshCts == null);
        Debug.Assert(this.lastRefreshConsolesTask == null);

        this.refreshCts = new CancellationTokenSource(3000);
        this.lastRefreshConsolesTask = ActivityManager.Instance.RunTask(async () => {
            ActivityTask activity = ActivityTask.Current;

            IEnumerable<UnicastIPAddressInformation> unicastIps =
                NetworkInterface.GetAllNetworkInterfaces().
                                 Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.Supports(NetworkInterfaceComponent.IPv4)).
                                 SelectMany(x => x.GetIPProperties().UnicastAddresses).
                                 Where(ip => ip.Address.AddressFamily != AddressFamily.InterNetworkV6 && !IPAddress.IsLoopback(ip.Address));

            byte[] sendBuffer = BitConverter.GetBytes((short) 3);
            List<Task> tasks = new List<Task>();
            
            foreach (UnicastIPAddressInformation ip in unicastIps) {
                tasks.Add(Task.Run(DiscoverConsoleOnNetworkInterface, activity.CancellationToken));
                continue;

                async Task DiscoverConsoleOnNetworkInterface() {
                    using CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                    using CancellationTokenSource actualCts = CancellationTokenSource.CreateLinkedTokenSource(activity.CancellationToken, timeoutCts.Token);
                    actualCts.Token.ThrowIfCancellationRequested();

                    byte[] dgBuf = new byte[1024];
                    // broadcast wildcard discovery packet
                    using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.EnableBroadcast = true;
                    socket.Bind(new IPEndPoint(ip.Address, 0));
                    await socket.SendToAsync(sendBuffer, new IPEndPoint(IPAddress.Broadcast, 730), actualCts.Token);

                    while (true) {
                        actualCts.Token.ThrowIfCancellationRequested();

                        SocketReceiveFromResult result;
                        try {
                            result = await socket.ReceiveFromAsync(dgBuf, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), actualCts.Token);
                        }
                        catch (OperationCanceledException e) {
                            if (e.CancellationToken == timeoutCts.Token || timeoutCts.IsCancellationRequested) {
                                // Timeout, nothing responded in enough time
                                break;
                            }
                            else {
                                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(void () => this.DiscoveredConsoles.Clear(), token: CancellationToken.None);
                                return;
                            }
                        }
                        catch {
                            return;
                        }

                        if (result.ReceivedBytes >= 2 && dgBuf[0] == 2 && dgBuf[1] + 2 == result.ReceivedBytes) {
                            socket.Close();

                            string xboxName = await GetConsoleName((IPEndPoint) result.RemoteEndPoint, actualCts.Token) ?? "";

                            // string xboxName = Encoding.ASCII.GetString(dgBuf, 2, dgBuf[1]);
                            DiscoveredConsole console = new DiscoveredConsole((IPEndPoint) result.RemoteEndPoint, xboxName);
                            await ApplicationPFX.Instance.Dispatcher.InvokeAsync(void () => this.DiscoveredConsoles.Add(console), token: CancellationToken.None);
                            return;
                        }
                    }
                }
            }

            await Task.WhenAll(tasks).WaitAsync(activity.CancellationToken);
            this.hasCompletedDiscovery = true;
        }, this.refreshCts);
    }

    public static async Task<string?> GetConsoleName(IPEndPoint endPoint, CancellationToken cancellation) {
        using TcpClient tcp = new TcpClient();
        await tcp.ConnectAsync(endPoint.Address.ToString(), 730, cancellation);
        using StreamReader sr = new StreamReader(tcp.GetStream(), leaveOpen: true);
        
        string responseText = await sr.ReadLineAsync(cancellation) ?? "";
        if (responseText != "201- connected") {
            return null;
        }
        
        await tcp.GetStream().WriteAsync("dbgname\r\n"u8.ToArray(), cancellation).ConfigureAwait(false);
        responseText = await sr.ReadLineAsync(cancellation) ?? "";
        if (XbdmResponse.TryParseFromLine(responseText, out XbdmResponse response)) {
            return response.Message;
        }

        return null;
    }

    protected override void OnHidden() {
        if (IsDiscoveryEnabled) {
            this.lastRefreshConsolesTask?.TryCancel();
            this.lastRefreshConsolesTask = null;
            this.refreshCts?.Dispose();
            this.refreshCts = null;
        }
    }
}