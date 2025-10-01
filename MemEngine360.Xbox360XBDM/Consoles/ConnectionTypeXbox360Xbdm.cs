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

using System.Net.Sockets;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using MemEngine360.Engine;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;
using MemEngine360.Xbox360XBDM.Views;
using PFXToolKitUI.Activities;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

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
        // yield return new SeparatorEntry();
        // yield return new CaptionEntry("JRPC2 Commands");
        // yield return new CommandContextEntry("commands.memengine.remote.GetCPUKeyCommand", "Get CPU Key");
        // yield return new CommandContextEntry("commands.memengine.remote.GetDashboardVersionCommand", "Get Dashboard Version");
        // yield return new CommandContextEntry("commands.memengine.remote.GetTemperaturesCommand", "Get Temperatures");
        // yield return new CommandContextEntry("commands.memengine.remote.GetTitleIDCommand", "Get Current TitleID");
        // yield return new CommandContextEntry("commands.memengine.remote.GetMoBoTypeCommand", "Get Motherboard Type");
        // yield return new CommandContextEntry("commands.memengine.remote.TestRPCCommand", "SV_SetConfigString on MW3 (TU24)");

        yield return new DynamicGroupPlaceholderContextObject(new DynamicContextGroup((group, ctx, items) => {
            IConsoleConnection? connection;
            if (!MemoryEngine.EngineDataKey.TryGetContext(ctx, out MemoryEngine? engine))
                return;
            if ((connection = engine.Connection) == null || !connection.HasFeature<IFeatureXboxJRPC2>())
                return;

            items.Add(new CaptionEntry("JRPC2 Commands"));
            items.Add(new CommandContextEntry("commands.memengine.remote.GetCPUKeyCommand", "Get CPU Key"));
            items.Add(new CommandContextEntry("commands.memengine.remote.GetDashboardVersionCommand", "Get Dashboard Version"));
            items.Add(new CommandContextEntry("commands.memengine.remote.GetTemperaturesCommand", "Get Temperatures"));
            items.Add(new CommandContextEntry("commands.memengine.remote.GetTitleIDCommand", "Get Current TitleID"));
            items.Add(new CommandContextEntry("commands.memengine.remote.GetMoBoTypeCommand", "Get Motherboard Type"));
            items.Add(new CommandContextEntry("commands.memengine.remote.TestRPCCommand", "SV_SetConfigString on MW3 (TU24)"));
        }));
    }

    public override UserConnectionInfo? CreateConnectionInfo() {
        return new ConnectToXboxInfo();
    }

    public override async Task<IConsoleConnection?> OpenConnection(UserConnectionInfo? _info, IContextData context, CancellationTokenSource cancellation) {
        ConnectToXboxInfo info = (ConnectToXboxInfo) _info!;
        if (string.IsNullOrWhiteSpace(info.IpAddress)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid address", "Address cannot be an empty string");
            return null;
        }

        bool isOpeningFromNormalDialog = IOpenConnectionView.IsConnectingFromViewDataKey.TryGetContext(context, out bool isFromView) && isFromView;

        // %appdata%/MemEngine360/Options/application.xml
        BasicApplicationConfiguration.Instance.LastHostName = info.IpAddress;
        BasicApplicationConfiguration.Instance.StorageManager.SaveArea(BasicApplicationConfiguration.Instance);

        ActivityTask<XbdmConsoleConnection> activity = ActivityManager.Instance.RunTask(async () => {
            IActivityProgress progress = ActivityTask.Current.Progress;
            progress.Caption = "XBDM Connection";
            progress.Text = "Connecting to console...";
            progress.IsIndeterminate = true;

            TcpClient client = new TcpClient();
            client.ReceiveTimeout = 4000;

            await client.ConnectAsync(info.IpAddress, 730, cancellation.Token);

            progress.Text = "Connected. Waiting for acknowledgement...";
            StreamReader reader = new StreamReader(client.GetStream(), leaveOpen: true);
            string? response = (await Task.Run(() => reader.ReadLine(), cancellation.Token))?.ToLower();
            if (response != "201- connected") {
                throw new Exception("Received invalid response from console: " + (response ?? ""));
            }

            XbdmConsoleConnection connection = new XbdmConsoleConnection(client, info.IpAddress);
            try {
                await connection.DetectDynamicFeatures();
            }
            catch (Exception e) when (e is IOException || e is TimeoutException) {
                throw new Exception("Network error while detecting dynamic features");
            }
            catch {
                throw new Exception("Unexpected error detecting dynamic features");
            }

            return connection;
        }, cancellation);

        if (isOpeningFromNormalDialog && IForegroundActivityService.TryGetInstance(out IForegroundActivityService? service)) {
            ITopLevel? topLevel = TopLevelContextUtils.GetTopLevelFromContext();
            if (topLevel != null) {
                await service.DelayedWaitForActivity(topLevel, activity, 500);
            }
        }

        Result<XbdmConsoleConnection> result = await activity;
        if (result.Exception is SocketException ex) {
            string message;
            switch (ex.SocketErrorCode) {
                case SocketError.InvalidArgument:    message = "Console IP/hostname is invalid"; break;
                case SocketError.TooManyOpenSockets: message = "Too many sockets open"; break;
                case SocketError.TimedOut:           message = "Timeout while connecting. Is the console connected to the internet?"; break;
                case SocketError.ConnectionRefused:  message = "Connection refused. Is the console running xbdm?"; break;
                case SocketError.TryAgain:           message = "Could not identify hostname. Try again later"; break;
                default:                             message = ex.Message; break;
            }

            await IMessageDialogService.Instance.ShowMessage("Socket Error: " + ex.SocketErrorCode, message, defaultButton: MessageBoxResult.OK);
            return null;
        }
        else if (result.Exception != null && !(result.Exception is OperationCanceledException)) {
            await IMessageDialogService.Instance.ShowMessage("Error", result.Exception.Message, defaultButton: MessageBoxResult.OK);
            return null;
        }

        return result.GetValueOrDefault();
    }
}