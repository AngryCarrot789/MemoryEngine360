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

namespace MemEngine360.Engine;

public delegate void MemoryEngineUIEventHandler(MemoryEngineManager manager, IEngineUI engineUI);

/// <summary>
/// Manages memory engine instances
/// </summary>
public abstract class MemoryEngineManager {
    private readonly List<IEngineUI> engines;

    /// <summary>
    /// Gets all opened engine views. This list is read-only
    /// </summary>
    public IList<IEngineUI> Engines { get; }
    
    /// <summary>
    /// A global event fired when any mem engine view opens
    /// </summary>
    public event MemoryEngineUIEventHandler? EngineOpened;

    /// <summary>
    /// A global event fired when any mem engine view closes
    /// </summary>
    public event MemoryEngineUIEventHandler? EngineClosed;
    
    public MemoryEngineManager() {
        this.Engines = (this.engines = new List<IEngineUI>(1)).AsReadOnly();
    }

    protected void OnEngineOpened(IEngineUI engineUI) {
        if (this.engines.Contains(engineUI))
            throw new InvalidOperationException("Engine already opened");

        this.engines.Add(engineUI);
        this.EngineOpened?.Invoke(this, engineUI);
    }

    protected void OnEngineClosed(IEngineUI engineUI) {
        if (!this.engines.Remove(engineUI))
            throw new InvalidOperationException("Engine not opened");
        
        this.EngineClosed?.Invoke(this, engineUI);
    }
}