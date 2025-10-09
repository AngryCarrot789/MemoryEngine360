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

using PFXToolKitUI;
using PFXToolKitUI.AdvancedMenuService;

namespace MemEngine360.Scripting;

public static class ScriptTabContextRegistry {
    public static readonly ContextRegistry Registry;

    static ScriptTabContextRegistry() {
        Registry = new ContextRegistry("Script");
        Registry.Opened += static (registry, context) => {
            if (Script.DataKey.TryGetContext(context, out Script? script) && !string.IsNullOrWhiteSpace(script.Name)) {
                registry.ObjectName = script.Name;
            }
            else {
                registry.ObjectName = null;
            }
        };
        
        FixedContextGroup general = Registry.GetFixedGroup("General");
        general.AddCommand("commands.scripting.RenameScriptCommand", "Rename", icon: StandardIcons.ABCTextIcon);
        general.AddCommand("commands.scripting.CloseScriptCommand", "Close", icon: StandardIcons.CancelActivityIcon);
        general.AddSeparator();
        general.AddEntry(new CommandContextEntry("commands.scripting.RunScriptCommand", "Run", icon: StandardIcons.SmallContinueActivityIconColourful) { DisabledIcon = StandardIcons.SmallContinueActivityIconDisabled });
        general.AddEntry(new CommandContextEntry("commands.scripting.StopScriptCommand", "Stop", icon: StandardIcons.StopIconColourful) { DisabledIcon = StandardIcons.StopIconDisabled });
        general.AddEntry(new CommandContextEntry("commands.scripting.SaveScriptCommand", "Save", icon: SimpleIcons.SaveFileIcon));
        general.AddCommand("commands.scripting.SaveScriptAsCommand", "Save As...");
        general.AddCommand("commands.scripting.SaveAllScriptsCommand", "Save All");
        general.AddSeparator();
        general.AddCommand("commands.scripting.ConnectScriptToConsoleCommand", "Connect to console...", "Connect using a dedicated connection instead of using the engine's connection", SimpleIcons.ConnectToConsoleDedicatedIcon);
    }
}