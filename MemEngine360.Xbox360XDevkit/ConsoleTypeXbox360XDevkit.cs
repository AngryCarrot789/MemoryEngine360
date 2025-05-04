using MemEngine360.Avalonia.Resources.Icons;
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
    
    public override string Description => "Uses XDevkit's COM interfaces to interact with the xbox";

    public override Icon? Icon => SimpleIcons.CursedXbox360Icon;

    private XboxManager? xboxManager;

    private ConsoleTypeXbox360XDevkit() {
    }

    public override IEnumerable<IContextObject> GetRemoteContextOptions() {
        yield return new CommandContextEntry("commands.memengine.remote.ListHelpCommand", "List all commands in popup");
        yield return new CommandContextEntry("commands.memengine.remote.ShowConsoleInfoCommand", "Console info");
        yield return new CommandContextEntry("commands.memengine.remote.ShowXbeInfoCommand", "Show XBE info");
        yield return new CommandContextEntry("commands.memengine.remote.MemProtectionCommand", "Show Memory Regions");
        yield return new CommandContextEntry("commands.memengine.remote.ModulesCommand", "Show Modules");
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

        await IMessageDialogService.Instance.ShowMessage("Error", "Could not connect to Xbox 360: " + (task.Exception?.Message ?? "Unknown Error"));
        return null;
    }
}