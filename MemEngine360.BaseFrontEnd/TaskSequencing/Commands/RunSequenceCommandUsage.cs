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

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Commands;

public abstract class BaseSequenceIsRunningDependentCommandUsage : BaseSequenceCommandUsage {
    protected BaseSequenceIsRunningDependentCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnTaskSequenceChanged(TaskSequence? oldSeq, TaskSequence? newSeq) {
        base.OnTaskSequenceChanged(oldSeq, newSeq);
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

    protected override void OnTaskSequenceChanged(TaskSequence? oldSeq, TaskSequence? newSeq) {
        base.OnTaskSequenceChanged(oldSeq, newSeq);
        if (oldSeq != null) {
            oldSeq.UseEngineConnectionChanged -= this.OnUseEngineConnectionChanged;
            oldSeq.DedicatedConnectionChanged -= this.OnDedicatedConnectionChanged;
        }

        if (newSeq != null) {
            newSeq.UseEngineConnectionChanged += this.OnUseEngineConnectionChanged;
            newSeq.DedicatedConnectionChanged += this.OnDedicatedConnectionChanged;
        }
    }

    protected override void OnEngineChanged(MemoryEngine360? oldEngine, MemoryEngine360? newEngine) {
        base.OnEngineChanged(oldEngine, newEngine);
        if (oldEngine != null)
            oldEngine.ConnectionChanged -= this.OnConnectionChanged;
        if (newEngine != null)
            newEngine.ConnectionChanged += this.OnConnectionChanged;
    }

    private void OnConnectionChanged(MemoryEngine360 sender, ulong frame, IConsoleConnection? oldC, IConsoleConnection? newC, ConnectionChangeCause cause) {
        this.UpdateCanExecuteLater();
    }
    
    private void OnUseEngineConnectionChanged(TaskSequence sender) {
        this.UpdateCanExecuteLater();
    }

    private void OnDedicatedConnectionChanged(TaskSequence sender, IConsoleConnection? olddedicatedconnection, IConsoleConnection? newdedicatedconnection) {
        this.UpdateCanExecuteLater();
    }
}