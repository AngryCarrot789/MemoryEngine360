using Avalonia.Controls;
using MemEngine360.ModTools;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.BaseFrontEnd.ModTools.Commands;

public class RestartModToolCommandUsage : BaseModToolCommandUsage {
    public RestartModToolCommandUsage() : base("commands.modtools.RestartModToolCommand") {
    }

    protected override void OnModToolChanged(ModTool? oldTool, ModTool? newTool) {
        base.OnModToolChanged(oldTool, newTool);
        if (oldTool != null)
            oldTool.IsRunningChanged -= this.OnIsRunningChanged;
        if (newTool != null)
            newTool.IsRunningChanged += this.OnIsRunningChanged;
    }

    private void OnIsRunningChanged(ModTool sender) {
        this.UpdateCanExecuteLater();
    }

    protected override void OnUpdateForCanExecuteState(Executability state) {
        base.OnUpdateForCanExecuteState(state);
        if (this.Button.Control is ContentControl cc) {
            cc.Content = this.ModTool == null || !this.ModTool.IsRunning ? "Run Tool" : "Restart";
        }
    }
}