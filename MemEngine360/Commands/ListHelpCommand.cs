using MemEngine360.Connections.Impl;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands;

public class ListHelpCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        return engine.Connection != null ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        if (engine.Connection != null) {
            List<string> list = await ((PhantomRTMConsoleConnection) engine.Connection).SendCommandAndReceiveLines("help");
            await IMessageDialogService.Instance.ShowMessage("Help", "Available commands", string.Join(Environment.NewLine, list));
        }
    }
}