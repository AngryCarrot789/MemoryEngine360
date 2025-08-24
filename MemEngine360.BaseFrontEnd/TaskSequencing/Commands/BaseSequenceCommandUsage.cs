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

using MemEngine360.Engine;
using MemEngine360.Sequencing;
using PFXToolKitUI.Avalonia.CommandUsages;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Commands;

public abstract class BaseSequenceCommandUsage : SimpleButtonCommandUsage {
    public MemoryEngine? Engine { get; private set; }
    
    public TaskSequence? TaskSequence { get; private set; }

    protected BaseSequenceCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnConnected() {
        this.Button.IsEnabled = false;
        base.OnConnected();
    }

    protected override void OnContextChanged() {
        base.OnContextChanged();
        TaskSequence? oldSeq = this.TaskSequence;
        TaskSequence? newSeq = null;
        if (this.GetContextData() is IContextData data) {
            newSeq = ITaskSequenceItemUI.DataKey.TryGetContext(data, out ITaskSequenceItemUI? newSeqUI) ? newSeqUI.TaskSequence : null;
        }

        if (oldSeq != newSeq) {
            MemoryEngine? oldEngine = this.Engine;
            MemoryEngine? newEngine = newSeq?.Manager?.MemoryEngine;
            
            this.TaskSequence = newSeq;
            this.OnTaskSequenceChanged(oldSeq, newSeq);
            if (oldEngine != newEngine) {
                this.Engine = newEngine;
                this.OnEngineChanged(oldEngine, newEngine);
            }
            
            this.UpdateCanExecuteLater();
        }
    }

    protected virtual void OnTaskSequenceChanged(TaskSequence? oldSeq, TaskSequence? newSeq) {
        
    }

    /// <summary>
    /// Always called after <see cref="OnTaskSequenceChanged"/>. Invoked when the effective engine changes
    /// </summary>
    /// <param name="oldEngine">Previous engine ref</param>
    /// <param name="newEngine">New engine ref</param>
    protected virtual void OnEngineChanged(MemoryEngine? oldEngine, MemoryEngine? newEngine) {
        
    }
}