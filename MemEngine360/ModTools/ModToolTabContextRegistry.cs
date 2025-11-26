// 
// Copyright (c) 2025-2025 REghZy
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

using PFXToolKitUI;
using PFXToolKitUI.AdvancedMenuService;

namespace MemEngine360.ModTools;

public static class ModToolTabContextRegistry {
    public static readonly ContextRegistry Registry;

    static ModToolTabContextRegistry() {
        Registry = new ContextRegistry("Mod Tool");
        Registry.Opened += static (_, context) => {
            if (ModTool.DataKey.TryGetContext(context, out ModTool? tool) && !string.IsNullOrWhiteSpace(tool.Name)) {
                Registry.ObjectName = tool.Name;
            }
            else {
                Registry.ObjectName = null;
            }
        };

        FixedWeightedMenuEntryGroup general = Registry.GetFixedGroup("General");
        general.AddCommand("commands.modtools.RenameModToolCommand", "Rename", icon: StandardIcons.ABCTextIcon);
        general.AddCommand("commands.modtools.CloseModToolCommand", "Close", icon: StandardIcons.CancelActivityIcon);
        general.AddSeparator();
        general.AddEntry(new CommandMenuEntry("commands.modtools.RestartModToolCommand", "Restart").
            AddContextUpdateHandlerWithEvent(
                ModTool.DataKey,
                nameof(ModTool.IsRunningChanged),
                static (entry, e) => entry.DisplayName = e.Value == null || !e.Value.IsRunning ? "Run Tool" : "Restart"));
        general.AddEntry(new CommandMenuEntry("commands.modtools.SaveModToolCommand", "Save", icon: SimpleIcons.SaveFileIcon));
        general.AddCommand("commands.modtools.SaveModToolAsCommand", "Save As...");
        general.AddCommand("commands.modtools.SaveAllModToolsCommand", "Save All");
        general.AddSeparator();
        general.AddCommand("commands.modtools.CopyModToolFilePathCommand", "Copy File Path");
        general.AddSeparator();
        general.AddCommand("commands.modtools.ConnectModToolToConsoleCommand", "Connect to console...", "Connect using a dedicated connection instead of using the engine's connection", SimpleIcons.ConnectToConsoleDedicatedIcon);
    }
}