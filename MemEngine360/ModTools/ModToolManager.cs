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
using PFXToolKitUI;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.ModTools;

public class ModToolManager : IComponentManager, IUserLocalContext {
    public static readonly DataKey<ModToolManager> DataKey = DataKeys.Create<ModToolManager>(nameof(ModToolManager));

    private readonly List<ModTool> myModTools;
    private readonly MenuEntryGroup modToolMenu;
    private readonly Dictionary<ModTool, BaseMenuEntry> toolToMenuEntry;

    ComponentStorage IComponentManager.ComponentStorage => field ??= new ComponentStorage(this);

    public IMutableContextData UserContext { get; } = new ContextData();

    /// <summary>
    /// Gets the list of scripts
    /// </summary>
    public ReadOnlyCollection<ModTool> ModTools { get; }

    public MemoryEngine MemoryEngine { get; }

    public event EventHandler<ItemIndexEventArgs<ModTool>>? ToolAdded, ToolRemoved; 
    public event EventHandler<ItemMovedEventArgs<ModTool>>? ToolMoved; 

    public ModToolManager(MemoryEngine memoryEngine, MenuEntryGroup modToolMenu) {
        this.MemoryEngine = memoryEngine;
        this.myModTools = new List<ModTool>();
        this.ModTools = this.myModTools.AsReadOnly();
        this.toolToMenuEntry = new Dictionary<ModTool, BaseMenuEntry>();
        this.modToolMenu = modToolMenu;
    }

    public void AddModTool(ModTool modTool) {
        ArgumentNullException.ThrowIfNull(modTool);
        if (modTool.myManager != null)
            throw new InvalidOperationException($"Mod tool already exists in another {nameof(ModToolManager)}");

        modTool.myManager = this;

        int index = this.myModTools.Count;
        this.myModTools.Insert(index, modTool);

        ExecuteModToolMenuEntry menuEntry = ExecuteModToolMenuEntry.GetOrCreate(modTool);
        this.toolToMenuEntry[modTool] = menuEntry;
        this.modToolMenu.Items.Add(menuEntry);
        
        this.ToolAdded?.Invoke(this, new ItemIndexEventArgs<ModTool>(modTool, index));
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
        bool removed = this.toolToMenuEntry.Remove(modTool, out BaseMenuEntry? entry);
        Debug.Assert(removed);
        
        this.modToolMenu.Items.Remove(entry!);
        this.ToolRemoved?.Invoke(this, new ItemIndexEventArgs<ModTool>(modTool, index));
    }

    public void MoveModTool(int oldIndex, int newIndex) {
        ModTool item = this.myModTools[oldIndex];
        this.myModTools.MoveItem(oldIndex, newIndex);
        this.ToolMoved?.Invoke(this, new ItemMovedEventArgs<ModTool>(item, oldIndex, newIndex));
    }

    private sealed class ExecuteModToolMenuEntry : CustomMenuEntry {
        private readonly ModTool modTool;

        private ExecuteModToolMenuEntry(ModTool modTool) : base(modTool.Name ?? "", "Show this tool's window") {
            this.modTool = modTool;
            this.modTool.FilePathChanged += this.OnFilePathChanged;
        }

        public static ExecuteModToolMenuEntry GetOrCreate(ModTool tool) {
            return ((IComponentManager) tool).GetOrCreateComponent(static t => new ExecuteModToolMenuEntry((ModTool) t));
        }

        private void OnFilePathChanged(object? o, EventArgs e) {
            this.DisplayName = ((ModTool) o!).Name ?? "";
        }

        public override bool CanExecute(IContextData context) {
            return !this.modTool.IsRunning && !this.modTool.IsCompiling;
        }

        public override async Task OnExecute(IContextData context) {
            if (this.modTool.IsRunning || this.modTool.IsCompiling) {
                return;
            }

            if (!await this.modTool.StartCommand()) {
                return;
            }

            IModToolViewService service = ApplicationPFX.GetComponent<IModToolViewService>();
            await service.ShowOrFocusGui(this.modTool);
        }
    }
}