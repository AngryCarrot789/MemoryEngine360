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

using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Sequencing;
using PFXToolKitUI.Avalonia.CommandUsages;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public abstract class BaseSequenceCommandUsage : SimpleButtonCommandUsage {
    public MemoryEngine360? Engine { get; private set; }
    
    public TaskSequence? Sequence { get; private set; }

    protected BaseSequenceCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnConnected() {
        this.Button.IsEnabled = false;
        base.OnConnected();
    }

    protected override void OnContextChanged() {
        base.OnContextChanged();
        TaskSequence? oldSeq = this.Sequence;
        TaskSequence? newSeq = null;
        if (this.GetContextData() is IContextData data) {
            ITaskSequencerUI.TaskSequenceDataKey.TryGetContext(data, out newSeq);
        }

        if (oldSeq != newSeq) {
            MemoryEngine360? oldEngine = this.Engine;
            MemoryEngine360? newEngine = newSeq?.Manager?.Engine;
            if (oldEngine != newEngine) {
                this.Engine = newEngine;
                this.OnEngineChanged(oldEngine, newEngine);
            }
            
            this.Sequence = newSeq;
            this.OnSequenceChanged(oldSeq, newSeq);
            this.UpdateCanExecuteLater();
        }
    }

    protected virtual void OnSequenceChanged(TaskSequence? oldSeq, TaskSequence? newSeq) {
        
    }

    protected virtual void OnEngineChanged(MemoryEngine360? oldEngine, MemoryEngine360? newEngine) {
        
    }
}

public abstract class BaseSequenceIsRunningDependentCommandUsage : BaseSequenceCommandUsage {
    protected BaseSequenceIsRunningDependentCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnSequenceChanged(TaskSequence? oldSeq, TaskSequence? newSeq) {
        base.OnSequenceChanged(oldSeq, newSeq);
        if (oldSeq != null)
            oldSeq.IsRunningChanged -= this.OnIsRunningChanged;
        if (newSeq != null)
            newSeq.IsRunningChanged += this.OnIsRunningChanged;
    }

    protected virtual void OnIsRunningChanged(TaskSequence sender) {
        this.UpdateCanExecuteLater();
    }
}

public class CancelSequenceCommandUsage() : BaseSequenceIsRunningDependentCommandUsage("commands.sequencer.CancelSequenceCommand");

public class RunSequenceCommandUsage : BaseSequenceIsRunningDependentCommandUsage {
    public RunSequenceCommandUsage() : base("commands.sequencer.RunSequenceCommand") {
    }

    protected override void OnEngineChanged(MemoryEngine360? oldEngine, MemoryEngine360? newEngine) {
        base.OnEngineChanged(oldEngine, newEngine);
        if (oldEngine != null)
            oldEngine.ConnectionChanged -= this.OnConnectionChanged;
        if (newEngine != null)
            newEngine.ConnectionChanged += this.OnConnectionChanged;
    }

    private void OnConnectionChanged(MemoryEngine360 sender, IConsoleConnection? oldconnection, IConsoleConnection? newconnection, ConnectionChangeCause cause) {
        this.UpdateCanExecuteLater();
    }
}