using MemEngine360.Connections.Impl;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.Messaging;

namespace MemEngine360.Commands;

public class MemProtectionCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine360 engine, CommandEventArgs e) {
        return engine.Connection != null ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override async Task ExecuteCommandAsync(MemoryEngine360 engine, CommandEventArgs e) {
        await engine.BeginBusyOperationActivityAsync(async (t, c) => {
            List<MemoryRegion> results = await c.GetMemoryRegions();
            List<string> lines = results.Select(x => $"Base: {x.BaseAddress:X8} Size: {x.Size:X8}, Protection: {x.Protection:X8}, PhysicalAddress: {x.PhysicalAddress:X8}").ToList();
            await IMessageDialogService.Instance.ShowMessage("Memory Regions", string.Join(Environment.NewLine, lines));
        });
    }
}