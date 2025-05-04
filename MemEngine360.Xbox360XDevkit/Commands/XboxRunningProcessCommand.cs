using MemEngine360.Commands;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;
using XDevkit;

namespace MemEngine360.Xbox360XDevkit.Commands;

public abstract class BaseXboxDevkitCommand : BaseRemoteConsoleCommand {
    protected override async Task<bool> TryBeginExecuteAsync(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        if (!(connection is Devkit360Connection)) {
            await IMessageDialogService.Instance.ShowMessage("Not an xbox console", "This command cannot be used because we are not connected to an xbox 360");
            return false;
        }

        return true;
    }

    protected sealed override Task ExecuteRemoteCommandInActivity(MemoryEngine360 engine, IConsoleConnection connection, CommandEventArgs e) {
        return this.ExecuteRemoteCommandInActivity(engine, (Devkit360Connection) connection, e);
    }
    
    protected abstract Task ExecuteRemoteCommandInActivity(MemoryEngine360 engine, Devkit360Connection connection, CommandEventArgs e);
}

public class XboxRunningProcessCommand : BaseXboxDevkitCommand {
    protected override string ActivityText => "Getting running process";
    
    protected override async Task ExecuteRemoteCommandInActivity(MemoryEngine360 engine, Devkit360Connection connection, CommandEventArgs e) {
        XboxProcessInfo result;
        try {
            result = await Task.Run(() => connection.Console.RunningProcessInfo);
        }
        catch (OperationCanceledException) {
            return;
        }
        catch (Exception ex) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Error getting running process info", ex.GetToString());
            return;
        }

        await IMessageDialogService.Instance.ShowMessage("Process info", $"Process ID: {result.ProcessId} (0x{result.ProcessId:X8}) {Environment.NewLine}" +
                                                                         $"Program Name: {result.ProgramName}");
    }
}