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

using MemEngine360.Sequencing.View;
using PFXToolKitUI;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Utils;

namespace MemEngine360.Sequencing.Contexts;

public static class TaskSequenceContextRegistry {
    public static readonly ContextRegistry Registry = new ContextRegistry("Task Sequence");

    static TaskSequenceContextRegistry() {
        Registry.Opened += static (registry, context) => {
            if (TaskSequenceManager.DataKey.TryGetContext(context, out TaskSequenceManager? manager)) {
                TaskSequenceManagerViewState vs = TaskSequenceManagerViewState.GetInstance(manager);
                if (vs.SelectedSequences.Count > 0) {
                    if (vs.SelectedSequences.Count == 1) {
                        string first = vs.SelectedSequences.SelectedItems[0].DisplayName;
                        registry.ObjectName = string.IsNullOrWhiteSpace("(unnammed sequence)") ? null : first;
                    }
                    else {
                        registry.ObjectName = $"{vs.SelectedSequences.Count} sequence{Lang.S(vs.SelectedSequences.Count)}";
                    }

                    return;
                }
            }

            registry.ObjectName = null;
        };

        FixedContextGroup edit = Registry.GetFixedGroup("general");
        edit.AddHeader("Edit");
        edit.AddDynamicSubGroup((group, ctx, items) => {
            if (TaskSequenceManager.DataKey.TryGetContext(ctx, out TaskSequenceManager? ui)) {
                TaskSequenceManagerViewState state = TaskSequenceManagerViewState.GetInstance(ui);
                if (state.PrimarySelectedSequence != null)
                    items.Add(new CommandContextEntry("commands.sequencer.RenameSequenceCommand", "Rename", icon: StandardIcons.ABCTextIcon));
            }
        });

        edit.AddCommand("commands.sequencer.DuplicateSequenceCommand", "Duplicate");

        FixedContextGroup actions = Registry.GetFixedGroup("operations");
        actions.AddHeader("General");
        actions.AddEntry(new CommandContextEntry("commands.sequencer.RunSequenceCommand", "Run") { Icon = StandardIcons.SmallContinueActivityIconColourful, DisabledIcon = StandardIcons.SmallContinueActivityIconDisabled });
        actions.AddDynamicSubGroup((group, ctx, items) => {
            if (TaskSequenceManager.DataKey.TryGetContext(ctx, out TaskSequenceManager? ui)) {
                TaskSequenceManagerViewState state = TaskSequenceManagerViewState.GetInstance(ui);
                items.Add(new CommandContextEntry("commands.sequencer.StopSpecificSequenceCommand", "Stop") { Icon = StandardIcons.StopIconColourful, DisabledIcon = StandardIcons.StopIconDisabled });
                if (state.SelectedSequences.Count > 1) {
                    items.Add(new SeparatorEntry());
                    items.Add(new CommandContextEntry("commands.sequencer.StopSelectedSequencesCommand", "Stop All"));
                }
            }
        });

        actions.AddCommand("commands.sequencer.ConnectToDedicatedConsoleCommand", "Connect to console...", "Connect using a dedicated connection instead of using the engine's connection", icon: SimpleIcons.ConnectToConsoleDedicatedIcon);

        FixedContextGroup destruction = Registry.GetFixedGroup("destruction");

        // Hook onto context changed instead of using dynamic context entries, because they're slightly expensive and
        // also sometimes buggy with no hope of being truly fixed because i'm not smart enough to figure it out.
        // Check out the method AdvancedMenuService.RemoveItemNodesWithDynamicSupport for nightmare fuel
        destruction.AddCommand("commands.sequencer.DeleteSequenceSelectionCommand", "Delete Sequence(s)").
                    AddSimpleContextUpdate(TaskSequenceManager.DataKey, (e, ui) => {
                        e.DisplayName =
                            ui != null && TaskSequenceManagerViewState.GetInstance(ui).SelectedSequences.Count == 1
                                ? "Delete Sequence"
                                : "Delete Sequences";
                    });
    }
}