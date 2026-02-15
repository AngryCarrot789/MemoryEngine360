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

using System.Runtime.Versioning;
using MemEngine360.Configs;
using MemEngine360.Connections;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.PS3.CCAPI;

[SupportedOSPlatform("windows")]
public class ConnectionTypePS3CCAPI : RegisteredConnectionType {
    public const string TheID = "console.ps3.ccapi-coreimpl"; // -coreimpl suffix added to core plugins, not that we need to but eh
    public static readonly RegisteredConnectionType Instance = new ConnectionTypePS3CCAPI();

    public override string DisplayName => "PS3 (CCAPI)";

    public override string FooterText => "Semi-stable";

    public override string LongDescription => "A connection to a PS3 using CCAPI. Basic memory read/write works.";

    public override Icon Icon => SimpleIcons.PS3CCAPIIcon;

    public override IEnumerable<PlatformIconInfo> PlatformIcons => [new(PlatformIcon.WindowsIcon, "CCAPI is closed source, and is only implemented on windows")];

    private ConnectionTypePS3CCAPI() {
    }

    public override IEnumerable<IMenuEntry> GetRemoteContextOptions() {
        yield return new CommandMenuEntry("commands.ps3.SetProcessToActiveGameCommand", "Attach to Game", "Find active game PID and attach CCAPI to it");
        yield return new CommandMenuEntry("commands.ps3.SetProcessCommand", "Attach to process...");
        yield return new CommandMenuEntry("commands.ps3.ListAllProcessesCommand", "List all processes");
    }

    protected override string GetStatusBarTextCore(IConsoleConnection connection) {
        IPs3ConsoleConnection conn = (IPs3ConsoleConnection) connection;
        string text = base.GetStatusBarTextCore(connection);
        text += $" - Attached PID 0x{conn.AttachedProcess:X8}";
        return text;
    }

    public override UserConnectionInfo? CreateConnectionInfo() {
        return new ConnectToCCAPIInfo();
    }

    public override async Task<IConsoleConnection?> OpenConnection(UserConnectionInfo? _info, IContextData additionalContext, CancellationTokenSource cancellation) {
        ConnectToCCAPIInfo info = (ConnectToCCAPIInfo) _info!;
        if (string.IsNullOrWhiteSpace(info.IpAddress)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid IP", "IP address is invalid", icon: MessageBoxIcons.ErrorIcon);
            return null;
        }

        // %appdata%/MemEngine360/Options/application.xml
        BasicApplicationConfiguration.Instance.LastPS3HostName = info.IpAddress;
        BasicApplicationConfiguration.Instance.StorageManager.SaveArea(BasicApplicationConfiguration.Instance);

        if (!File.Exists("CCAPI.dll")) {
            ITopLevel? topLevel = TopLevelContextUtils.GetTopLevelFromContext();
            ActivityTask<bool> activity = ActivityManager.Instance.RunTask(() => ConnectToCCAPIInfo.TryDownloadCCApi(topLevel, false, ActivityTask.Current.CancellationToken), true);

            if (topLevel != null && IForegroundActivityService.TryGetInstance(out IForegroundActivityService? service)) {
                await service.WaitForActivity(topLevel, activity);
            }

            await activity;
        }

        if (!File.Exists("CCAPI.dll")) {
            await IMessageDialogService.Instance.ShowMessage("No CCAPI", "Cannot connect to the PS3 without CCAPI.");
            return null;
        }

        ConsoleControlAPI? api = null;
        try {
            api = await ConsoleControlAPI.Run();
            if (await api.ConnectToConsole(info.IpAddress)) {
                return new ConsoleConnectionCCAPI(api);
            }
            else {
                await IMessageDialogService.Instance.ShowMessage("Failed to connect", "CCAPI could not connect to PS3 at " + info.IpAddress);
            }
        }
        catch (Exception e) {
            await IMessageDialogService.Instance.ShowExceptionMessage("CCAPI", "Failed to setup CCAPI: " + e.Message, e);
        }

        if (api != null) {
            try {
                api.Dispose();
            }
            catch (Exception e) {
                AppLogger.Instance.WriteLine("Exception disposing ConsoleControl API object");
                AppLogger.Instance.WriteLine(e.GetToString());
            }
        }

        return null;
    }
}