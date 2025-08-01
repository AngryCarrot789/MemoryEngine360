﻿// 
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

using System.Net.Sockets;
using MemEngine360.BaseFrontEnd;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;
using MemEngine360.Xbox360XBDM.Views;
using PFXToolKitUI;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Xbox360XBDM.Consoles;

public class ConnectionTypeXbox360Xbdm : RegisteredConnectionType {
    public const string TheID = "console.xbox360.xbdm-coreimpl"; // -coreimpl suffix added to core plugins, not that we need to but eh
    public static readonly RegisteredConnectionType Instance = new ConnectionTypeXbox360Xbdm();

    public override string DisplayName => "Xbox 360 (XBDM)";

    public override string? FooterText => "Stable";

    public override string LongDescription => "A connection to an xbox 360 via xbdm (using TCP on port 730)";

    public override Icon? Icon => SimpleIcons.Xbox360Icon;

    public override bool SupportsEvents => true;

    private ConnectionTypeXbox360Xbdm() {
    }

    public override IEnumerable<IContextObject> GetRemoteContextOptions() {
        yield return new CommandContextEntry("commands.memengine.remote.ListHelpCommand", "List all commands in popup");
        yield return new CommandContextEntry("commands.memengine.remote.ShowConsoleInfoCommand", "Console info");
        yield return new CommandContextEntry("commands.memengine.remote.ShowXbeInfoCommand", "Show XBE info");
        yield return new SeparatorEntry();
        yield return new CommandContextEntry("commands.memengine.remote.EjectDiskTrayCommand", "Open Disk Tray");
        yield return new CommandContextEntry("commands.memengine.remote.DebugFreezeCommand", "Debug Freeze");
        yield return new CommandContextEntry("commands.memengine.remote.DebugUnfreezeCommand", "Debug Un-freeze");
        yield return new CommandContextEntry("commands.memengine.remote.SoftRebootCommand", "Soft Reboot (restart title)");
        yield return new CommandContextEntry("commands.memengine.remote.ColdRebootCommand", "Cold Reboot");
        yield return new CommandContextEntry("commands.memengine.remote.ShutdownCommand", "Shutdown");
    }

    public override UserConnectionInfo? CreateConnectionInfo(IContextData context) {
        return new ConnectToXboxInfo();
    }

    public override async Task<IConsoleConnection?> OpenConnection(UserConnectionInfo? _info, CancellationTokenSource cancellation) {
        ConnectToXboxInfo info = (ConnectToXboxInfo) _info!;
        if (string.IsNullOrWhiteSpace(info.IpAddress)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid address", "Address cannot be an empty string");
            return null;
        }

        // %appdata%/MemEngine360/Options/application.xml
        BasicApplicationConfiguration.Instance.LastHostName = info.IpAddress;
        BasicApplicationConfiguration.Instance.StorageManager.SaveArea(BasicApplicationConfiguration.Instance);

        try {
            XbdmConsoleConnection? result = await ActivityManager.Instance.RunTask(async () => {
                IActivityProgress progress = ActivityManager.Instance.GetCurrentProgressOrEmpty();
                progress.Caption = "XBDM Connection";
                progress.Text = "Connecting to console...";
                progress.IsIndeterminate = true;
                TcpClient client = new TcpClient() {
                    ReceiveTimeout = 4000
                };

                try {
                    await client.ConnectAsync(info.IpAddress, 730, cancellation.Token);
                }
                catch (OperationCanceledException) {
                    return null;
                }
                catch (SocketException e) {
                    string message;
                    switch (e.SocketErrorCode) {
                        case SocketError.InvalidArgument:    message = "Console IP/hostname is invalid"; break;
                        case SocketError.TooManyOpenSockets: message = "Too many sockets open"; break;
                        case SocketError.TimedOut:           message = "Timeout while connecting. Is the console connected to the internet?"; break;
                        case SocketError.ConnectionRefused:  message = "Connection refused. Is the console running xbdm?"; break;
                        case SocketError.TryAgain:           message = "Could not identify hostname. Try again later"; break;
                        default:                             message = e.Message; break;
                    }

                    await IMessageDialogService.Instance.ShowMessage("Socket Error: " + e.SocketErrorCode, message, defaultButton: MessageBoxResult.OK);
                    return null;
                }

                progress.Text = "Connected. Waiting for acknowledgement...";
                StreamReader reader = new StreamReader(client.GetStream(), leaveOpen: true);
                string? response = (await Task.Run(() => reader.ReadLine(), cancellation.Token))?.ToLower();
                if (response == "201- connected") {
                    return new XbdmConsoleConnection(client, info.IpAddress);
                }

                await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => IMessageDialogService.Instance.ShowMessage("Error", "Received invalid response from console: " + (response ?? "")));
                return null;
            }, cancellation);

            return result;
        }
        catch (Exception e) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Could not connect to Xbox 360: " + e.Message);
            return null;
        }
    }
}