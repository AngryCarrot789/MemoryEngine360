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

public abstract class BaseSequenceManagerCommandUsage : SimpleButtonCommandUsage {
    public MemoryEngine? Engine { get; private set; }
    
    public TaskSequenceManager? TaskSequencerManager { get; private set; }

    protected BaseSequenceManagerCommandUsage(string commandId) : base(commandId) {
    }

    protected override void OnConnected() {
        this.Button.IsEnabled = false;
        base.OnConnected();
    }

    protected override void OnContextChanged() {
        base.OnContextChanged();
        TaskSequenceManager? oldManager = this.TaskSequencerManager;
        TaskSequenceManager? newManager = null;
        if (this.GetContextData() is IContextData data && TaskSequenceManager.DataKey.TryGetContext(data, out TaskSequenceManager? manager)) {
            newManager = manager;
        }

        if (oldManager != newManager) {
            MemoryEngine? oldEngine = this.Engine;
            MemoryEngine? newEngine = newManager?.MemoryEngine;
            
            this.TaskSequencerManager = newManager;
            this.OnTaskSequencerManagerChanged(oldManager, newManager);
            if (oldEngine != newEngine) {
                this.Engine = newEngine;
                this.OnEngineChanged(oldEngine, newEngine);
            }
            
            this.UpdateCanExecuteLater();
        }
    }

    protected virtual void OnTaskSequencerManagerChanged(TaskSequenceManager? oldManager, TaskSequenceManager? newManager) {
        
    }

    protected virtual void OnEngineChanged(MemoryEngine? oldEngine, MemoryEngine? newEngine) {
        
    }
}