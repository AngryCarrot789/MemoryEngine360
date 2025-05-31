using MemEngine360.Sequencing;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Commands;

public class ConnectToDedicatedConsoleCommandUsage : BaseSequenceCommandUsage {
    private readonly TaskSequenceEventHandler somethingChanged;
    
    public ConnectToDedicatedConsoleCommandUsage() : base("commands.sequencer.ConnectToDedicatedConsoleCommand") {
        this.somethingChanged = s => this.UpdateCanExecuteLater();
    }

    protected override void OnTaskSequenceChanged(TaskSequence? oldSeq, TaskSequence? newSeq) {
        base.OnTaskSequenceChanged(oldSeq, newSeq);
        if (oldSeq != null) {
            oldSeq.IsRunningChanged -= this.somethingChanged;
            oldSeq.UseEngineConnectionChanged -= this.somethingChanged;
        }

        if (newSeq != null) {
            newSeq.IsRunningChanged += this.somethingChanged;
            newSeq.UseEngineConnectionChanged += this.somethingChanged;
        }
    }
}