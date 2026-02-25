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

using System.Collections.ObjectModel;
using System.Diagnostics;
using MemEngine360.Engine;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.ModTools;

public class ModToolManager : IComponentManager, IUserLocalContext {
    public static readonly DataKey<ModToolManager> DataKey = DataKeys.Create<ModToolManager>(nameof(ModToolManager));

    private readonly List<ModTool> myModTools;

    ComponentStorage IComponentManager.ComponentStorage => field ??= new ComponentStorage(this);

    public IMutableContextData UserContext { get; } = new ContextData();

    /// <summary>
    /// Gets the list of scripts
    /// </summary>
    public ReadOnlyCollection<ModTool> ModTools { get; }

    public MemoryEngine MemoryEngine { get; }

    public event EventHandler<ItemAddOrRemoveEventArgs<ModTool>>? ToolAdded, ToolRemoved; 
    public event EventHandler<ItemMoveEventArgs<ModTool>>? ToolMoved; 

    public ModToolManager(MemoryEngine memoryEngine) {
        this.MemoryEngine = memoryEngine;
        this.myModTools = new List<ModTool>();
        this.ModTools = this.myModTools.AsReadOnly();
    }

    public void AddModTool(ModTool modTool) {
        ArgumentNullException.ThrowIfNull(modTool);
        if (modTool.myManager != null)
            throw new InvalidOperationException($"Mod tool already exists in another {nameof(ModToolManager)}");

        modTool.myManager = this;

        int index = this.myModTools.Count;
        this.myModTools.Insert(index, modTool);
        
        this.ToolAdded?.Invoke(this, new ItemAddOrRemoveEventArgs<ModTool>(index, modTool));
    }

    public void RemoveModTool(ModTool modTool) {
        ArgumentNullException.ThrowIfNull(modTool);
        if (modTool.myManager != this)
            throw new InvalidOperationException($"Mod tool does not exist in this {nameof(ModToolManager)}");
        if (modTool.IsRunning)
            throw new InvalidOperationException("Mod tool is running. It must be stopped first.");
        if (modTool.IsCompiling)
            throw new InvalidOperationException("Mod tool is compiling. It must be cancelled first.");
        
        int index = this.myModTools.IndexOf(modTool);
        if (index == -1) {
            Debug.Fail("Impossible");
            return;
        }

        this.myModTools.RemoveAt(index);

        modTool.myManager = null;
        this.ToolRemoved?.Invoke(this, new ItemAddOrRemoveEventArgs<ModTool>(index, modTool));
    }

    public void MoveModTool(int oldIndex, int newIndex) {
        ModTool item = this.myModTools[oldIndex];
        this.myModTools.MoveItem(oldIndex, newIndex);
        this.ToolMoved?.Invoke(this, new ItemMoveEventArgs<ModTool>(oldIndex, newIndex, item));
    }
}