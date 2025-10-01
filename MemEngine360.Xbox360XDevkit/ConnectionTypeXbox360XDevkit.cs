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

using System.Runtime.InteropServices;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Xbox360XDevkit.Views;
using PFXToolKitUI.Activities;
using XDevkit;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Xbox360XDevkit;

public class ConnectionTypeXbox360XDevkit : RegisteredConnectionType {
    public const string TheID = "console.xbox360.xdevkit-coreimpl";
    public static readonly RegisteredConnectionType Instance = new ConnectionTypeXbox360XDevkit();

    public override string DisplayName => "Xbox 360 (XDevkit)";

    public override string? FooterText => "Mostly stable";

    public override string LongDescription => $"Uses XDevkit's COM interfaces to interact with the xbox.{Environment.NewLine}" +
                                              $"Most features tested, most non-read/write operations are not async so expect freezing";

    public override Icon? Icon => SimpleIcons.CursedXbox360Icon;

    private XboxManager? xboxManager;

    private ConnectionTypeXbox360XDevkit() {
    }

    public override IEnumerable<IContextObject> GetRemoteContextOptions() {
        yield return new CommandContextEntry("commands.memengine.remote.XboxRunningProcessCommand", "Show Running process");
        yield return new SeparatorEntry();
        yield return new CommandContextEntry("commands.memengine.remote.DebugFreezeCommand", "Debug Freeze");
        yield return new CommandContextEntry("commands.memengine.remote.DebugUnfreezeCommand", "Debug Un-freeze");
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

        // %appdata%/MemoryEngine360/Options/application.xml
        BasicApplicationConfiguration.Instance.LastHostName = info.IpAddress;
        BasicApplicationConfiguration.Instance.StorageManager.SaveArea(BasicApplicationConfiguration.Instance);

        Result<XboxConsole> result = await ActivityManager.Instance.RunTask(() => {
            this.xboxManager ??= new XboxManagerClass();
            
            IActivityProgress progress = ActivityTask.Current.Progress;
            progress.Caption = "XDevkit";
            progress.Text = "Connecting to console...";
            progress.IsIndeterminate = true;

            XboxConsole console = this.xboxManager.OpenConsole(info.IpAddress);
            console.DebugTarget.ConnectAsDebugger("MemoryEngine360", XboxDebugConnectFlags.Force);
            return Task.FromResult(console);
        }, cancellation);

        if (!result.HasException) {
            return new XDevkitConsoleConnection(this.xboxManager!, result.Value);
        }

        string msg = result.Exception is COMException com ? $"COMException {com.Message}" : result.Exception!.Message;
        await IMessageDialogService.Instance.ShowMessage("Error", "Could not connect to Xbox 360: " + msg);
        return null;
    }
}