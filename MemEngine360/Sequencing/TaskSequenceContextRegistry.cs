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

namespace MemEngine360.Sequencing;

public static class TaskSequenceContextRegistry {
    public static ContextRegistry Registry = new ContextRegistry("Task Sequence");

    static TaskSequenceContextRegistry() {
        FixedContextGroup actions = Registry.GetFixedGroup("operations");
        actions.AddHeader("General");
        actions.AddCommand("commands.sequencer.RunSequenceCommand", "Run");
        actions.AddCommand("commands.sequencer.StopSequenceCommand", "Cancel");
        
        FixedContextGroup edit = Registry.GetFixedGroup("general");
        edit.AddHeader("Edit");
        edit.AddCommand("commands.sequencer.RenameSequenceCommand", "Rename");
        edit.AddCommand("commands.sequencer.DuplicateSequenceCommand", "Duplicate");
        edit.AddCommand("commands.sequencer.DeleteSequenceSelectionCommand", "Delete Sequence(s)");
        edit.AddCommand("commands.sequencer.ConnectToDedicatedConsoleCommand", "Connect to dedicated console...");
    }
}