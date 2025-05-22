// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using System.Runtime.InteropServices;
using MemEngine360.BaseFrontEnd;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Xbox360XDevkit.Views;
using XDevkit;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Xbox360XDevkit;

public class ConsoleTypeXbox360XDevkit : RegisteredConsoleType {
    public const string TheID = "console.xbox360.xdevkit-coreimpl";
    public static readonly RegisteredConsoleType Instance = new ConsoleTypeXbox360XDevkit();

    public override string DisplayName => "Xbox 360 (XDevkit)";

    public override string? FooterText => "Mostly stable";

    public override string LongDescription => $"Uses XDevkit's COM interfaces to interact with the xbox.{Environment.NewLine}" +
                                              $"Most features tested, most non-read/write operations are not async so expect freezing";

    public override Icon? Icon => SimpleIcons.CursedXbox360Icon;

    private XboxManager? xboxManager;

    private ConsoleTypeXbox360XDevkit() {
    }

    public override IEnumerable<IContextObject> GetRemoteContextOptions() {
        yield return new CommandContextEntry("commands.memengine.remote.ListHelpCommand", "List all commands in popup");
        yield return new CommandContextEntry("commands.memengine.remote.ShowConsoleInfoCommand", "Console info");
        yield return new CommandContextEntry("commands.memengine.remote.ShowXbeInfoCommand", "Show XBE info");
        yield return new CommandContextEntry("commands.memengine.remote.XboxRunningProcessCommand", "Show Running process");
        yield return new SeparatorEntry();
        yield return new CommandContextEntry("commands.memengine.remote.EjectDiskTrayCommand", "Open Disk Tray");
        yield return new CommandContextEntry("commands.memengine.remote.DebugFreezeCommand", "Debug Freeze");
        yield return new CommandContextEntry("commands.memengine.remote.DebugUnfreezeCommand", "Debug Un-freeze");
        yield return new CommandContextEntry("commands.memengine.remote.SoftRebootCommand", "Soft Reboot (restart title)");
        yield return new CommandContextEntry("commands.memengine.remote.ColdRebootCommand", "Cold Reboot");
        yield return new CommandContextEntry("commands.memengine.remote.ShutdownCommand", "Shutdown");
    }

    public override UserConnectionInfo? CreateConnectionInfo(MemoryEngine360 engine) {
        return new ConnectToXboxInfo(engine);
    }

    public override async Task<IConsoleConnection?> OpenConnection(MemoryEngine360 engine, UserConnectionInfo? _info, CancellationTokenSource cancellation) {
        ConnectToXboxInfo info = (ConnectToXboxInfo) _info!;
        if (string.IsNullOrWhiteSpace(info.IpAddress)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid address", "Address cannot be an empty string");
            return null;
        }

        // %appdata%/MemEngine360/Options/application.xml
        BasicApplicationConfiguration.Instance.LastHostName = info.IpAddress;
        BasicApplicationConfiguration.Instance.StorageManager.SaveArea(BasicApplicationConfiguration.Instance);

        ActivityTask<XboxConsole> task = ActivityManager.Instance.RunTask(() => {
            this.xboxManager ??= new XboxManagerClass();

            // TODO: figure out how to listen to events.
            // I assume the console connects to a Tcp connection on the port, and sends text with \r\n at the end of each line
            // DEBUGGER CONNECT PORT=0x<PORT_HERE> override user=<COMPUTER_NAME_HERE> name="MemEngine360"

            IActivityProgress progress = ActivityManager.Instance.GetCurrentProgressOrEmpty();
            progress.Caption = "XDevkit";
            progress.Text = "Connecting to console...";
            progress.IsIndeterminate = true;

            XboxConsole console = this.xboxManager.OpenConsole(info.IpAddress);
            console.DebugTarget.ConnectAsDebugger("MemEngine360", XboxDebugConnectFlags.Force);
            return Task.FromResult(console);
        }, cancellation);

        XboxConsole? result = await task;
        if (result != null) {
            return new Devkit360Connection(this.xboxManager!, result);
        }

        string msg = task.Exception is COMException com ? $"COMException {com.Message}" : (task.Exception?.Message ?? "(unknown error)");
        await IMessageDialogService.Instance.ShowMessage("Error", "Could not connect to Xbox 360: " + msg);
        return null;
    }
}