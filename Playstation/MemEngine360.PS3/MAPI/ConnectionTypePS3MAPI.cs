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

using System.Net;
using System.Net.Sockets;
using MemEngine360.Connections;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.PS3.MAPI;

public class ConnectionTypePS3MAPI : RegisteredConnectionType {
    public const string TheID = "console.ps3.mapi-coreimpl"; // -coreimpl suffix added to core plugins, not that we need to but eh
    public static readonly RegisteredConnectionType Instance = new ConnectionTypePS3MAPI();

    public override string DisplayName => "PS3 (MAPI)";

    public override string FooterText => "Experimental";

    public override string LongDescription => "A connection to a PS3 using the Manager API. Reading and Writing do NOT work yet.";

    public override Icon Icon => SimpleIcons.PS3MAPIIcon;

    private ConnectionTypePS3MAPI() {
    }

    public override UserConnectionInfo CreateConnectionInfo() => new ConnectToMAPIInfo();

    public override async Task<IConsoleConnection?> OpenConnection(UserConnectionInfo? _info, IContextData additionalContext, CancellationTokenSource cancellation) {
        ConnectToMAPIInfo info = (ConnectToMAPIInfo) _info!;
        if (string.IsNullOrWhiteSpace(info.IpAddress)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid IP", "IP address is invalid", icon: MessageBoxIcons.ErrorIcon);
            return null;
        }

        IPHostEntry entry;
        try {
            if (IPAddress.TryParse(info.IpAddress, out IPAddress? ip)) {
                entry = new IPHostEntry {
                    HostName = ip.ToString(),
                    Aliases = Array.Empty<string>(),
                    AddressList = new IPAddress[] { ip }
                };
            }
            else {
                using CancellationTokenSource ctsDnsTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
                ctsDnsTimeout.CancelAfter(3000);

                entry = await Dns.GetHostEntryAsync(info.IpAddress, ctsDnsTimeout.Token);
            }
        }
        catch (Exception e) {
            await IMessageDialogService.Instance.ShowMessage("Invalid Ip", "Failed to resolve IP from host name", icon: MessageBoxIcons.ErrorIcon, dialogCancellation: cancellation.Token);
            return null;
        }

        if (entry.AddressList.Length < 1) {
            await IMessageDialogService.Instance.ShowMessage("Invalid Ip", "Hostname resolves to no IP addresses", icon: MessageBoxIcons.ErrorIcon, dialogCancellation: cancellation.Token);
            return null;
        }

        using CancellationTokenSource ctsSocketTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        ctsSocketTimeout.CancelAfter(5000);

        Socket main_sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint main_ipEndPoint = new IPEndPoint(entry.AddressList[0], info.Port);

        try {
            await main_sock.ConnectAsync(main_ipEndPoint, ctsSocketTimeout.Token);
        }
        catch (OperationCanceledException) {
            await IMessageDialogService.Instance.ShowMessage("Timeout", "Timed out trying to connect to console", icon: MessageBoxIcons.ErrorIcon, dialogCancellation: cancellation.Token);
            return null;
        }
        catch (Exception e) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Error while connecting: " + e.Message, icon: MessageBoxIcons.ErrorIcon, dialogCancellation: cancellation.Token);
            return null;
        }

        Ps3ManagerApiV2 api = new Ps3ManagerApiV2();
        api.SetMainSocket(main_sock, main_ipEndPoint);

        try {
            MapiResponse response1 = await api.ReadResponse();
            if (response1.Code != ResponseCode.PS3MAPIConnected) {
                await IMessageDialogService.Instance.ShowMessage("Error", "PS3ManagerAPI error: " + response1.Message, icon: MessageBoxIcons.ErrorIcon, dialogCancellation: cancellation.Token);
                return null;
            }

            MapiResponse response2 = await api.ReadResponse();
            if (response2.Code != ResponseCode.PS3MAPIConnectedOK) {
                await IMessageDialogService.Instance.ShowMessage("Error", "PS3ManagerAPI error: " + response1.Message, icon: MessageBoxIcons.ErrorIcon, dialogCancellation: cancellation.Token);
                return null;
            }
        }
        catch (TimeoutException e) {
            await IMessageDialogService.Instance.ShowMessage("Timeout", "Timed out reading from console", icon: MessageBoxIcons.ErrorIcon, dialogCancellation: cancellation.Token);
            return null;
        }
        catch (Exception e) {
            await IMessageDialogService.Instance.ShowMessage("Error", e.Message, icon: MessageBoxIcons.ErrorIcon, dialogCancellation: cancellation.Token);
            return null;
        }

        return new ConsoleConnectionMAPI(api);
    }
}