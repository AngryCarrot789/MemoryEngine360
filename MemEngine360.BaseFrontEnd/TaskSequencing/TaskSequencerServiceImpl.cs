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
using PFXToolKitUI.Avalonia.Services.Windowing;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class TaskSequencerServiceImpl : ITaskSequencerService {
    private WeakReference<TaskSequencerWindow>? currentWindow;
    
    public Task OpenOrFocusWindow(IEngineUI engine) {
        if (WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
            if (this.currentWindow == null || !this.currentWindow.TryGetTarget(out TaskSequencerWindow? existing) || existing.IsClosed) {
                TaskSequencerWindow window = new TaskSequencerWindow(engine.MemoryEngine.TaskSequenceManager);

                if (this.currentWindow == null)
                    this.currentWindow = new WeakReference<TaskSequencerWindow>(window);
                else
                    this.currentWindow.SetTarget(window);
                
                system.Register(window).Show();
            }
            else {
                existing.Activate();
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }
}