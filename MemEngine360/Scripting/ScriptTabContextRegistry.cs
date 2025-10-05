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

using PFXToolKitUI.AdvancedMenuService;

namespace MemEngine360.Scripting;

public static class ScriptTabContextRegistry {
    public static readonly ContextRegistry Registry;

    static ScriptTabContextRegistry() {
        Registry = new ContextRegistry("Script");
        FixedContextGroup general = Registry.GetFixedGroup("General");
        general.AddCommand("commands.scripting.RenameScriptCommand", "Rename");
        general.AddCommand("commands.scripting.CloseScriptCommand", "Close");
        general.AddSeparator();
        general.AddCommand("commands.scripting.RunScriptCommand", "Run");
        general.AddCommand("commands.scripting.StopScriptCommand", "Stop");
        general.AddCommand("commands.scripting.SaveScriptCommand", "Save");
        general.AddCommand("commands.scripting.SaveScriptAsCommand", "Save As...");
        general.AddCommand("commands.scripting.SaveAllScriptsCommand", "Save All");
        general.AddSeparator();
        general.AddCommand("commands.scripting.ConnectScriptToConsoleCommand", "Connect to Console", "Connect using a dedicated connection instead of using the engine's connection");
    }
}