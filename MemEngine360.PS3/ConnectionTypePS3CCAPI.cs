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
using MemEngine360.Connections;
using MemEngine360.PS3.CC;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.PS3;

[SupportedOSPlatform("windows")]
public class ConnectionTypePS3CCAPI : RegisteredConnectionType {
    public const string TheID = "console.ps3.ccapi-coreimpl"; // -coreimpl suffix added to core plugins, not that we need to but eh
    public static readonly RegisteredConnectionType Instance = new ConnectionTypePS3CCAPI();

    public override string DisplayName => "PS3 (CCAPI)";

    public override string FooterText => "Untested";

    public override string LongDescription => "A connection to a PS3 using CCAPI";

    public override Icon Icon => SimpleIcons.PS3CCAPIIcon;

    public override IEnumerable<PlatformIconInfo> PlatformIcons => [new(PlatformIcon.WindowsIcon, "CCAPI is closed source, and is only implemented on windows")];

    public override bool SupportsEvents => false;

    private ConnectionTypePS3CCAPI() {
    }

    public override UserConnectionInfo? CreateConnectionInfo() {
        return new ConnectToCCAPIInfo();
    }

    public override async Task<IConsoleConnection?> OpenConnection(UserConnectionInfo? _info, IContextData additionalContext, CancellationTokenSource cancellation) {
        {
            MessageBoxInfo info1 = new MessageBoxInfo("Untested", "This feature is completely untested. Continue at your own risk!") {
                Buttons = MessageBoxButton.OKCancel,
                DefaultButton = MessageBoxResult.Cancel,
                YesOkText = "I might brick my PS3, but oh well",
                NoText = "Cancel"
            };

            MessageBoxResult msg1 = await IMessageDialogService.Instance.ShowMessage(info1);
            if (msg1 != MessageBoxResult.OK) {
                return null;
            }
        }

        {
            MessageBoxInfo info2 = new MessageBoxInfo("Untested", "Are you sure?") {
                Buttons = MessageBoxButton.OKCancel,
                DefaultButton = MessageBoxResult.Cancel,
                YesOkText = "Yes",
                NoText = "Cancel"
            };

            MessageBoxResult msg2 = await IMessageDialogService.Instance.ShowMessage(info2);
            if (msg2 != MessageBoxResult.OK) {
                return null;
            }
        }

        ConnectToCCAPIInfo info = (ConnectToCCAPIInfo) _info!;
        if (string.IsNullOrWhiteSpace(info.IpAddress)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid IP", "IP address is invalid");
            return null;
        }

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
                await IMessageDialogService.Instance.ShowMessage("Failed to connect", "Failed to connect to PS3 at " + info.IpAddress);
            }
        }
        catch (Exception e) {
            await LogExceptionHelper.ShowMessageAndPrintToLogs("CCAPI", "Failed to setup CCAPI", e);
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