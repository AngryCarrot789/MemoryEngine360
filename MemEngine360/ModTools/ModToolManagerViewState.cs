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
using PFXToolKitUI;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.ModTools;

public class ModToolManagerViewState {
    /// <summary>
    /// Gets the task sequence manager for this state
    /// </summary>
    public ModToolManager ModToolManager { get; }

    /// <summary>
    /// Gets or sets the script being viewed in the editor
    /// </summary>
    public ModTool? SelectedModTool {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.SelectedModToolChanged);
    }

    public MenuEntryGroup ModToolMenu { get; }

    private readonly Dictionary<ModTool, BaseMenuEntry> toolToMenuEntry;

    public event EventHandler<ValueChangedEventArgs<ModTool?>>? SelectedModToolChanged;

    private ModToolManagerViewState(ModToolManager modToolManager) {
        this.ModToolManager = modToolManager;
        this.ModToolManager.ToolAdded += this.ModToolManagerOnToolAdded;
        this.ModToolManager.ToolRemoved += this.ModToolManagerOnToolRemoved;
        if (this.ModToolManager.ModTools.Count > 0) {
            this.SelectedModTool = this.ModToolManager.ModTools[0];
        }

        this.toolToMenuEntry = new Dictionary<ModTool, BaseMenuEntry>();
        this.ModToolMenu = new MenuEntryGroup("Mod Tools") {
            UniqueID = "memoryengine.tools.modtools",
            Items = {
                new CommandMenuEntry("commands.modtools.ShowModToolsWindowCommand", "Mod Tools Manager"),
                new SeparatorEntry()
            }
        };
    }

    private void ModToolManagerOnToolAdded(object? sender, ItemIndexEventArgs<ModTool> e) {
        ExecuteModToolMenuEntry menuEntry = ExecuteModToolMenuEntry.GetOrCreate(e.Item);
        this.toolToMenuEntry[e.Item] = menuEntry;
        this.ModToolMenu.Items.Add(menuEntry);
    }

    private void ModToolManagerOnToolRemoved(object? sender, ItemIndexEventArgs<ModTool> e) {
        bool removed = this.toolToMenuEntry.Remove(e.Item, out BaseMenuEntry? entry);
        Debug.Assert(removed);
        
        this.ModToolMenu.Items.Remove(entry!);
        
        ReadOnlyCollection<ModTool> list = this.ModToolManager.ModTools;
        if (list.Count < 1) {
            this.SelectedModTool = null;
        }
        else if (list[e.Index] == this.SelectedModTool) {
            this.SelectedModTool = e.Index > 0 ? list[e.Index - 1] : list[e.Index];
        }
    }

    public static ModToolManagerViewState GetInstance(ModToolManager manager) {
        return ((IComponentManager) manager).GetOrCreateComponent((t) => new ModToolManagerViewState((ModToolManager) t));
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