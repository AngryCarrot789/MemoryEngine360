// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemoryEngine360.
// 
// MemoryEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemoryEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemoryEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

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