using System.Net;
using System.Text;
using MemEngine360.Connections;
using MemEngine360.Connections.Impl;
using MemEngine360.Connections.Impl.Threads;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Commands;

public class ConnectToConsoleCommand : Command {
    protected override Executability CanExecuteCore(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine)) {
            return Executability.Invalid;
        }
        
        return !engine.IsConnectionBusy ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        if (!MemoryEngine360.DataKey.TryGetContext(e.ContextData, out MemoryEngine360? engine)) {
            return;
        }

        if (engine.Connection != null) {
            if (engine.IsConnectionBusy) {
                await IMessageDialogService.Instance.ShowMessage("Busy", "Connection is currently busy. Cannot disconnect");
                return;
            }
            
            MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage("Already Connected", "Already connected to an xbox. Close existing connection and then connect", MessageBoxButton.OKCancel, MessageBoxResult.OK);
            if (result != MessageBoxResult.OK) {
                return;
            }
            
            engine.Connection.Dispose();
            engine.Connection = null;
            
            if (ILatestActivityView.DataKey.TryGetContext(e.ContextData, out ILatestActivityView? view))
                view.Activity = "Disconnected from xbox 360";
        }

        DefaultProgressTracker progressTracker = new DefaultProgressTracker();
        IConsoleConnection? connection = await ApplicationPFX.Instance.ServiceManager.GetService<ConsoleConnectionService>().OpenDialogAndConnect(progressTracker);
        if (connection != null) {
            engine.Connection = connection;
            if (ILatestActivityView.DataKey.TryGetContext(e.ContextData, out ILatestActivityView? view))
                view.Activity = "Connected to xbox 360.";

            string debugName = await connection.GetDebugName();
            ExecutionState execState = await connection.GetExecutionState();
            uint currTitleAddr = await connection.GetTitleAddress();
            uint currProcId = await connection.GetProcessID();

            StringBuilder sb = new StringBuilder();
            sb.Append("Debug Name: ").Append(debugName).AppendLine();
            sb.Append("Execution State: ").Append(execState).AppendLine();
            sb.Append("Current Title Addr: ").Append(new IPAddress(currTitleAddr)).AppendLine();
            sb.Append("Current Process ID: ").Append(currProcId.ToString("X8")).AppendLine();
            sb.AppendLine("Named Threads below");
            foreach (ConsoleThread info in await connection.GetThreadDump()) {
                if (!string.IsNullOrEmpty(info.readableName)) {
                    sb.AppendLine(info.ToString());
                }
            }
            
            
            await IMessageDialogService.Instance.ShowMessage("Information", "Console Information as follows", sb.ToString());
        }
    }
}